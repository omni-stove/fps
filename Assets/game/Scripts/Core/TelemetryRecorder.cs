using System;
using System.Collections.Generic;
using System.IO;

namespace Shooter.Core
{
    // 同意が無いうちは送り先を作らない。作るとファイルが空でも置かれ、同意していない人の手元に記録の跡が残るため。
    // クラッシュはどのスレッドからも届く (Unity のログの受け口・タスクの例外) ので、中身はすべて _gate の中で触る。
    public sealed class TelemetryRecorder : IDisposable
    {
        // 毎フレーム同じ例外が出ると同じ行で上限を埋めて前の記録を押し出すので、同じ形のクラッシュはセッションに 1 件だけ残す。
        public const int MaxCrashSignatures = 64;

        private readonly object _gate = new object();
        private readonly HashSet<string> _crashSignatures = new HashSet<string>(StringComparer.Ordinal);
        private readonly Func<ITelemetrySink> _createSink;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<string> _log;
        private ITelemetrySink _sink;
        private bool _consent;
        private bool _sinkFailed;
        private bool _crashLimitLogged;
        private float _sinceFlush;
        private bool _disposed;

        public TelemetryRecorder(
            TelemetrySpec spec,
            TelemetryOrigin origin,
            bool consent,
            Func<ITelemetrySink> createSink,
            Func<DateTime> utcNow,
            Func<double> random,
            Action<string> log)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Origin = origin ?? throw new ArgumentNullException(nameof(origin));
            _createSink = createSink ?? throw new ArgumentNullException(nameof(createSink));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            // 1 件ごとではなくセッションごとに選ぶのは、選ばれたセッションの要約を欠けずに読めるようにするため
            SampledIn = spec.SampleRate >= 1f || random() < spec.SampleRate;
            _consent = consent;
            _log(IsRecording ? TelemetryLog.Recording(spec.Sink, DescribeTarget()) : TelemetryLog.NotRecording(consent, spec));
            if (IsRecording && !SampledIn)
            {
                _log(TelemetryLog.SampledOut(spec.SampleRate));
            }
        }

        public TelemetrySpec Spec { get; }

        public TelemetryOrigin Origin { get; }

        public bool SampledIn { get; }

        public bool Consent
        {
            get
            {
                lock (_gate)
                {
                    return _consent;
                }
            }
        }

        public bool IsRecording
        {
            get
            {
                lock (_gate)
                {
                    return _consent && !_disposed && !Spec.IsOff;
                }
            }
        }

        public bool HasSink
        {
            get
            {
                lock (_gate)
                {
                    return _sink != null;
                }
            }
        }

        // 同意を外したら、送り先にたまっている行も送らずに捨てる。外したあとに届くと、外した意味が無くなるため。
        public void SetConsent(bool consent)
        {
            ITelemetrySink closing = null;
            lock (_gate)
            {
                if (_disposed || _consent == consent)
                {
                    return;
                }

                _consent = consent;
                _sinkFailed = false;
                if (!consent)
                {
                    closing = _sink;
                    _sink = null;
                    _crashSignatures.Clear();
                    _crashLimitLogged = false;
                }
            }

            if (closing != null)
            {
                // Dispose は送信中の POST を待つので、呼んだスレッド (クライアントではメイン) を止めないよう別スレッドで閉じる
                var toClose = closing;
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        toClose.Dispose();
                    }
#pragma warning disable CA1031 // 同意を外したあとの後始末の失敗は、記録せず捨てる。どの例外でも呼び出し元には返せない
                    catch (Exception)
                    {
                    }
#pragma warning restore CA1031
                });
            }

            _log(TelemetryLog.ConsentChanged(consent));
        }

        public bool RecordCrash(Exception exception, bool waitForDelivery = false)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            // ToString は内側の例外まで含むので、型と文だけでなく、元になった例外まで追える。
            // 見分けには文を含まない型とスタックだけを使う (文に値が入る例外で別物と数えないため)
            var signature = new System.Text.StringBuilder();
            for (var e = exception; e != null; e = e.InnerException)
            {
                signature.Append(e.GetType().FullName).Append('\n').Append(e.StackTrace).Append('\n');
            }

            return RecordCrash(exception.GetType().FullName, exception.Message, exception.ToString(), waitForDelivery, signature.ToString());
        }

        public bool RecordCrash(string exceptionType, string message, string stackTrace, bool waitForDelivery = false, string signatureSource = null)
        {
            ITelemetrySink sink;
            lock (_gate)
            {
                if (!_consent || _disposed || Spec.IsOff || !Spec.CrashReports)
                {
                    return false;
                }

                var signature = (exceptionType ?? string.Empty) + "\n" + (signatureSource ?? stackTrace ?? string.Empty);
                if (_crashSignatures.Contains(signature))
                {
                    return false;
                }

                if (_crashSignatures.Count >= MaxCrashSignatures)
                {
                    if (!_crashLimitLogged)
                    {
                        _crashLimitLogged = true;
                        _log(TelemetryLog.CrashLimitReached(MaxCrashSignatures));
                    }

                    return false;
                }

                var line = TelemetryLog.Crash(_utcNow(), Origin, exceptionType, message, stackTrace);
                if (!TryWrite(line, out sink))
                {
                    return false;
                }

                _crashSignatures.Add(signature);
            }

            // 落ちる直前かもしれないので、ためずにすぐ送り出す。待つかどうかは、プロセスがこのあと終わるかを知っている呼び手が決める
            sink.Flush(waitForDelivery);
            return true;
        }

        public bool RecordMatchSummary(TelemetryMatchSummary summary)
        {
            if (summary == null)
            {
                throw new ArgumentNullException(nameof(summary));
            }

            lock (_gate)
            {
                if (!_consent || _disposed || Spec.IsOff || !SampledIn)
                {
                    return false;
                }

                return TryWrite(TelemetryLog.MatchSummary(_utcNow(), Origin, summary), out _);
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            ITelemetrySink sink;
            lock (_gate)
            {
                _sinceFlush += deltaSeconds;
                if (_sinceFlush < Spec.FlushIntervalSeconds)
                {
                    return;
                }

                _sinceFlush = 0f;
                sink = _sink;
            }

            sink?.Flush(false);
        }

        public void Flush(bool wait)
        {
            ITelemetrySink sink;
            lock (_gate)
            {
                sink = _sink;
            }

            sink?.Flush(wait);
        }

        // 終わるときは同意がある間に書いた行を送り切ってから閉じる
        public void Dispose()
        {
            ITelemetrySink sink;
            lock (_gate)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                sink = _sink;
                _sink = null;
            }

            if (sink == null)
            {
                return;
            }

            sink.Flush(true);
            sink.Dispose();
        }

        // _gate の中で呼ぶ
        private bool TryWrite(string line, out ITelemetrySink sink)
        {
            sink = _sink;
            if (sink == null)
            {
                if (_sinkFailed)
                {
                    return false;
                }

                try
                {
                    sink = _createSink();
                }
                catch (Exception e) when (e is IOException || e is ArgumentException || e is UnauthorizedAccessException || e is NotSupportedException || e is UriFormatException)
                {
                    // 作れない送り先は何度試しても同じなので、同意を入れ直すまで作り直さない
                    _sinkFailed = true;
                    _log(TelemetryLog.CannotCreateSink(Spec.Sink, e.Message));
                    return false;
                }

                if (sink == null)
                {
                    return false;
                }

                _sink = sink;
            }

            return sink.Write(line);
        }

        private string DescribeTarget()
        {
            return Spec.Sink == TelemetrySink.Http ? Spec.Endpoint : Spec.FileName;
        }
    }
}
