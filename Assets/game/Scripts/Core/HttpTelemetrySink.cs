using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Shooter.Core
{
    // 1 行ずつ送ると試合中に何度も通信するので、ためた行を Flush でまとめて JSON Lines の本文 1 つで送る。
    // 送るのは別のスレッドで、同時に送るのは 1 本だけにする。返事を待つ間にゲームの更新を止めないため。
    public sealed class HttpTelemetrySink : ITelemetrySink
    {
        public const string ContentType = "application/x-ndjson";

        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(10);

        private static readonly UTF8Encoding _encoding = new UTF8Encoding(false);

        private readonly object _gate = new object();
        private readonly LinkedList<string> _pending = new LinkedList<string>();
        private readonly HttpClient _client;
        private readonly Action<string> _log;
        private readonly TimeSpan _timeout;
        private Task _sending;
        private long _pendingBytes;
        private int _dropped;
        private bool _disposed;

        public HttpTelemetrySink(Uri endpoint, int maxPendingBytes, Action<string> log, HttpMessageHandler handler = null, TimeSpan? timeout = null)
        {
            if (endpoint == null)
            {
                throw new ArgumentNullException(nameof(endpoint));
            }

            if (!endpoint.IsAbsoluteUri || endpoint.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("endpoint must be an absolute https URL: '" + endpoint + "'", nameof(endpoint));
            }

            if (maxPendingBytes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPendingBytes), maxPendingBytes, "maxPendingBytes must be positive");
            }

            _timeout = timeout ?? DefaultTimeout;
            if (_timeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(timeout), _timeout, "timeout must be positive");
            }

            Endpoint = endpoint;
            MaxPendingBytes = maxPendingBytes;
            _log = log ?? throw new ArgumentNullException(nameof(log));
            // テストから渡す送り口は呼んだ側が持つので、ここでは閉じない
            _client = handler == null ? new HttpClient() : new HttpClient(handler, false);
            // 時間切れは 1 回の送信ごとに CancellationTokenSource で決めるので、HttpClient の既定 (100 秒) では切らせない
            _client.Timeout = Timeout.InfiniteTimeSpan;
        }

        public Uri Endpoint { get; }

        public int MaxPendingBytes { get; }

        public string Target => Endpoint.ToString();

        public int Pending
        {
            get
            {
                lock (_gate)
                {
                    return _pending.Count;
                }
            }
        }

        public bool Write(string line)
        {
            if (line == null)
            {
                throw new ArgumentNullException(nameof(line));
            }

            lock (_gate)
            {
                if (_disposed)
                {
                    return false;
                }

                Enqueue(line, false);
                ReportDropped();
                return true;
            }
        }

        public void Flush(bool wait)
        {
            Task sending;
            var startedHere = false;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                sending = _sending;
                if (sending == null || sending.IsCompleted)
                {
                    sending = StartSending();
                    startedHere = true;
                }
            }

            if (!wait || sending == null)
            {
                return;
            }

            WaitFor(sending);
            if (startedHere)
            {
                return;
            }

            // 前から送っていた分を待っただけなら、Flush を呼んだ時点でたまっていた行がまだ残っているので続けて送る。
            // 自分で送った分が失敗して戻した行は、ここで送り直さず次の Flush に回す。続けて叩いても同じ答えが返りやすいため。
            lock (_gate)
            {
                sending = _disposed ? null : StartSending();
            }

            if (sending != null)
            {
                WaitFor(sending);
            }
        }

        public void Dispose()
        {
            Task sending;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _pending.Clear();
                _pendingBytes = 0;
                sending = _sending;
            }

            // 送っている途中の本文は送り切らせる。途中で閉じると、受け手に半分だけ届くことがある
            if (sending != null)
            {
                WaitFor(sending);
            }

            _client.Dispose();
        }

        // _gate の中で呼ぶ
        private Task StartSending()
        {
            if (_pending.Count == 0)
            {
                return null;
            }

            var batch = new List<string>(_pending);
            _pending.Clear();
            _pendingBytes = 0;
            _sending = Task.Run(() => Send(batch));
            return _sending;
        }

        private void Send(List<string> batch)
        {
            var body = new StringBuilder();
            foreach (var line in batch)
            {
                body.Append(line).Append('\n');
            }

            string failure;
            bool keep;
#pragma warning disable CA1031 // 送り口の例外は種類が多く (名前解決・TLS・取り消し・対応していない機種)、どれでも記録を失うだけでゲームは続けるため
            try
            {
                using (var cancel = new CancellationTokenSource(_timeout))
                using (var content = new StringContent(body.ToString(), _encoding, ContentType))
                using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = content })
                using (var response = _client.SendAsync(request, cancel.Token).GetAwaiter().GetResult())
                {
                    if (response.IsSuccessStatusCode)
                    {
                        return;
                    }

                    var status = (int)response.StatusCode;
                    failure = "HTTP " + status.ToString(CultureInfo.InvariantCulture);
                    keep = IsTransient(response.StatusCode);
                }
            }
            catch (Exception e)
            {
                failure = e.GetType().Name + ": " + e.Message;
                keep = !(e is PlatformNotSupportedException);
            }
#pragma warning restore CA1031

            lock (_gate)
            {
                if (_disposed)
                {
                    keep = false;
                }
                else if (keep)
                {
                    // 後から書かれた行より前に戻し、受け手に届く順を書いた順に保つ
                    for (var i = batch.Count - 1; i >= 0; i--)
                    {
                        Enqueue(batch[i], true);
                    }
                }
            }

            _log(TelemetryLog.PostFailed(Target, failure, batch.Count, keep));
            lock (_gate)
            {
                ReportDropped();
            }
        }

        // 受け手が混んでいるか一時的に落ちているときだけ送り直す。それ以外の 4xx は送り直しても同じ答えが返るので捨てる
        private static bool IsTransient(HttpStatusCode status)
        {
            var code = (int)status;
            return code >= 500 || code == 408 || code == 429;
        }

        // _gate の中で呼ぶ。上限を超えたら古い行から捨てる。新しい行の方が、落ちる直前の様子に近いため
        private void Enqueue(string line, bool atFront)
        {
            var bytes = _encoding.GetByteCount(line) + 1;
            if (atFront)
            {
                if (_pendingBytes + bytes > MaxPendingBytes && _pending.Count > 0)
                {
                    _dropped++;
                    return;
                }

                _pending.AddFirst(line);
                _pendingBytes += bytes;
                return;
            }

            while (_pending.Count > 0 && _pendingBytes + bytes > MaxPendingBytes)
            {
                _pendingBytes -= _encoding.GetByteCount(_pending.First.Value) + 1;
                _pending.RemoveFirst();
                _dropped++;
            }

            _pending.AddLast(line);
            _pendingBytes += bytes;
        }

        // _gate の中で呼ぶ
        private void ReportDropped()
        {
            if (_dropped == 0)
            {
                return;
            }

            var dropped = _dropped;
            _dropped = 0;
            _log(TelemetryLog.Dropped(Target, dropped));
        }

        private void WaitFor(Task sending)
        {
            // 送る処理の中で時間切れを決めているので、ここは少し長めに待ち、送り口が時間切れを守らなくても止まり続けない
            try
            {
                sending.Wait(_timeout + _timeout);
            }
            catch (AggregateException)
            {
                // Send は失敗をログにして抜けるので、ここに来るのはスレッドの取り消しなどで、記録を失うだけで続ける
            }
        }
    }
}
