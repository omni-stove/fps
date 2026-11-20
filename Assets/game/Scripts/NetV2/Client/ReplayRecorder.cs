using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public enum ReplayRecorderState
    {
        Waiting,
        Recording,
        Finished,
        Abandoned,
    }

    // MatchClient と本物の通信路の間に挟み、届いた知らせをそのまま渡しながら tick 付きで残す。
    // MatchClient から読み取らずに通信路の段で残すのは、再生でも同じ MatchClient に承認から順に読ませれば、
    // 試合中と同じ部品がそのまま動くため (送る側は再生しないので、届いた側だけ残す)。
    // ファイルは承認 (ConnectAcceptedMessage) が届いてから書き始める。マップ・自分の Id・tick の速さが承認で決まり、
    // 承認されなかった接続は残しても再生できないため。それまでの知らせは届いた時刻と一緒に覚えておく。
    // IConnectionLoss を持つのは、挟んだことで中の通信路が測るロスを MatchClient から読めなくしないため。中が測れなければ false を返す。
    public sealed class ReplayRecorder : IMatchTransport, IConnectionLoss
    {
        public const int DefaultMaxPendingEvents = 512;

        private readonly IMatchTransport _inner;
        private readonly Stream _output;
        private readonly string _clientVersion;
        private readonly Func<double> _seconds;
        private readonly Func<DateTime> _utcNow;
        private readonly Action<string> _log;
        private readonly int _maxPendingEvents;
        private readonly MessageReader _reader = new MessageReader(Array.Empty<byte>());
        private readonly ConnectAcceptedMessage _accepted = new ConnectAcceptedMessage();
        private readonly List<Pending> _pending = new List<Pending>();
        private ReplayWriter _writer;
        private double _startSeconds = double.NaN;
        private DateTime _startedAtUtc;
        private bool _outputClosed;
        private bool _disposed;

        public ReplayRecorder(IMatchTransport inner, Stream output, string clientVersion, Action<string> log)
            : this(inner, output, clientVersion, log, null, null, DefaultMaxPendingEvents)
        {
        }

        // 時刻を関数で受けるのは、テストで届いた時刻を決めて tick を確かめられるようにするため。
        public ReplayRecorder(
            IMatchTransport inner,
            Stream output,
            string clientVersion,
            Action<string> log,
            Func<double> seconds,
            Func<DateTime> utcNow,
            int maxPendingEvents = DefaultMaxPendingEvents)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _output = output ?? throw new ArgumentNullException(nameof(output));
            if (!output.CanWrite)
            {
                throw new ArgumentException("the replay output must be writable", nameof(output));
            }

            _clientVersion = clientVersion ?? throw new ArgumentNullException(nameof(clientVersion));
            if (clientVersion.Length > ReplayFile.MaxTextLength)
            {
                throw new ArgumentException("client version is too long for a replay", nameof(clientVersion));
            }

            _log = log ?? throw new ArgumentNullException(nameof(log));
            if (maxPendingEvents < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPendingEvents), maxPendingEvents, "maxPendingEvents must be at least 1");
            }

            _maxPendingEvents = maxPendingEvents;
            _seconds = seconds ?? StopwatchSeconds();
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
        }

        public ReplayRecorderState State { get; private set; } = ReplayRecorderState.Waiting;

        // ファイルに何か書けたか。false のまま終わったファイルは再生できないので、呼ぶ側が消す。
        public bool HasRecording => _writer != null;

        public ReplayHeader Header => _writer?.Header;

        public int RecordedEvents => _writer?.Count ?? 0;

        public bool IsActive => _inner.IsActive;

        public void Listen(string address, ushort port)
        {
            throw new InvalidOperationException("a replay recorder records a client's connection; it does not listen");
        }

        public void Connect(string address, ushort port)
        {
            StartClock();
            _inner.Connect(address, port);
        }

        public void Disconnect(ConnectionId connection, string reason)
        {
            _inner.Disconnect(connection, reason);
        }

        public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
        {
            _inner.Send(connection, channel, payload);
        }

        public int GetMaxPayloadSize(TransportChannel channel)
        {
            return _inner.GetMaxPayloadSize(channel);
        }

        public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
        {
            return _inner.TryGetRoundTripTime(connection, out milliseconds);
        }

        public bool TryGetLossPercent(ConnectionId connection, out int percent)
        {
            percent = 0;
            return _inner is IConnectionLoss loss && loss.TryGetLossPercent(connection, out percent);
        }

        public bool TryReceive(out TransportEvent transportEvent)
        {
            if (!_inner.TryReceive(out transportEvent))
            {
                return false;
            }

            Record(transportEvent);
            return true;
        }

        public void Shutdown(string reason)
        {
            Finish();
            _inner.Shutdown(reason);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                Finish();
            }
            finally
            {
                _inner.Dispose();
            }
        }

        // 試合を抜ける前に残したものを閉じたいとき (メニューに戻る前など) に呼ぶ。何度呼んでもよい。
        public void Finish()
        {
            switch (State)
            {
                case ReplayRecorderState.Waiting:
                    _pending.Clear();
                    State = ReplayRecorderState.Abandoned;
                    _log(ReplayLog.RecordingAbandoned("the match ended before the server accepted the connection"));
                    CloseOutput();
                    return;
                case ReplayRecorderState.Recording:
                    State = ReplayRecorderState.Finished;
                    try
                    {
                        var duration = Math.Max(_writer.LastTick, TickNow());
                        _writer.Finish(duration);
                        _log(ReplayLog.RecordingFinished(_writer.Count, duration, _writer.Header.TickRate));
                    }
                    catch (Exception e) when (IsWriteFailure(e))
                    {
                        _log(ReplayLog.RecordingFailed(e.Message));
                    }

                    CloseOutput();
                    return;
                default:
                    CloseOutput();
                    return;
            }
        }

        private void Record(TransportEvent transportEvent)
        {
            if (State == ReplayRecorderState.Finished || State == ReplayRecorderState.Abandoned)
            {
                return;
            }

            StartClock();
            var elapsed = Math.Max(0d, _seconds() - _startSeconds);
            if (State == ReplayRecorderState.Waiting)
            {
                if (_pending.Count >= _maxPendingEvents)
                {
                    _pending.Clear();
                    State = ReplayRecorderState.Abandoned;
                    _log(ReplayLog.RecordingAbandoned(
                        _maxPendingEvents.ToString(CultureInfo.InvariantCulture) + " events arrived before the server accepted the connection"));
                    CloseOutput();
                    return;
                }

                _pending.Add(new Pending(elapsed, transportEvent));
                if (transportEvent.Kind == TransportEventKind.Data && IsAccepted(transportEvent.Payload))
                {
                    Begin();
                }
                else if (transportEvent.Kind == TransportEventKind.Disconnected)
                {
                    Finish();
                }

                return;
            }

            Write(TickAt(elapsed), transportEvent.Kind, transportEvent.Connection, transportEvent.Channel, PayloadOf(transportEvent));
            if (transportEvent.Kind == TransportEventKind.Disconnected)
            {
                Finish();
            }
        }

        private void Begin()
        {
            ReplayHeader header;
            try
            {
                header = new ReplayHeader(
                    MatchProtocol.Version, _clientVersion, _accepted.Map, _startedAtUtc, _accepted.TickRate, _accepted.PlayerId);
                _writer = new ReplayWriter(_output, header, true);
            }
            catch (Exception e) when (IsWriteFailure(e) || e is ArgumentException)
            {
                Fail(e);
                return;
            }

            State = ReplayRecorderState.Recording;
            _log(ReplayLog.RecordingStarted(header, _pending.Count));
            foreach (var pending in _pending)
            {
                Write(TickAt(pending.Seconds), pending.Kind, pending.Connection, pending.Channel, new ArraySegment<byte>(pending.Payload));
                if (State != ReplayRecorderState.Recording)
                {
                    break;
                }
            }

            _pending.Clear();
        }

        private void Write(int tick, TransportEventKind kind, ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
        {
            if (State != ReplayRecorderState.Recording)
            {
                return;
            }

            try
            {
                _writer.Append(Math.Max(_writer.LastTick, tick), ToRecordKind(kind), connection.Value, (byte)channel, payload);
            }
            catch (Exception e) when (IsWriteFailure(e))
            {
                Fail(e);
            }
        }

        private void Fail(Exception e)
        {
            _log(ReplayLog.RecordingFailed(e.Message));
            State = ReplayRecorderState.Abandoned;
            _pending.Clear();
            CloseOutput();
        }

        private bool IsAccepted(ArraySegment<byte> payload)
        {
            try
            {
                _reader.Reset(payload);
                if (_reader.ReadMessageId() != MessageIds.ConnectAccepted)
                {
                    return false;
                }

                _reader.ReadMessageBody(_accepted);
                _reader.ExpectEnd();
                return true;
            }
            catch (MessageReadException)
            {
                // MatchClient も読めない承認は捨てるので、記録も始めない
                return false;
            }
        }

        private void StartClock()
        {
            if (!double.IsNaN(_startSeconds))
            {
                return;
            }

            _startSeconds = _seconds();
            _startedAtUtc = _utcNow().ToUniversalTime();
        }

        private int TickNow()
        {
            return TickAt(Math.Max(0d, _seconds() - _startSeconds));
        }

        private int TickAt(double seconds)
        {
            var rate = _writer != null ? _writer.Header.TickRate : _accepted.TickRate;
            var ticks = Math.Floor(seconds * rate);
            return ticks >= int.MaxValue ? int.MaxValue : (int)Math.Max(0d, ticks);
        }

        private void CloseOutput()
        {
            if (_outputClosed)
            {
                return;
            }

            _outputClosed = true;
            try
            {
                _output.Dispose();
            }
            catch (Exception e) when (IsWriteFailure(e))
            {
                _log(ReplayLog.RecordingFailed(e.Message));
            }
        }

        private static ArraySegment<byte> PayloadOf(TransportEvent transportEvent)
        {
            switch (transportEvent.Kind)
            {
                case TransportEventKind.Data:
                    return transportEvent.Payload;
                case TransportEventKind.Disconnected:
                    return new ArraySegment<byte>(ReasonBytes(transportEvent.Reason));
                default:
                    return new ArraySegment<byte>(Array.Empty<byte>());
            }
        }

        private static byte[] ReasonBytes(string reason)
        {
            try
            {
                return TransportReason.Encode(reason);
            }
            catch (ArgumentException)
            {
                // 通信路が読み取った理由は収まるはずだが、収まらなければ理由の無い切断として残す (再生では DisconnectNotice が分ける)
                return Array.Empty<byte>();
            }
        }

        private static ReplayRecordKind ToRecordKind(TransportEventKind kind)
        {
            switch (kind)
            {
                case TransportEventKind.Connected:
                    return ReplayRecordKind.Connected;
                case TransportEventKind.Disconnected:
                    return ReplayRecordKind.Disconnected;
                default:
                    return ReplayRecordKind.Data;
            }
        }

        private static bool IsWriteFailure(Exception e)
        {
            return e is IOException || e is UnauthorizedAccessException || e is ObjectDisposedException || e is NotSupportedException;
        }

        private static Func<double> StopwatchSeconds()
        {
            var stopwatch = Stopwatch.StartNew();
            return () => stopwatch.Elapsed.TotalSeconds;
        }

        private readonly struct Pending
        {
            public Pending(double seconds, TransportEvent transportEvent)
            {
                Seconds = seconds;
                Kind = transportEvent.Kind;
                Connection = transportEvent.Connection;
                Channel = transportEvent.Channel;

                // 通信路の受信用の配列は次の TryReceive で上書きされるので、承認まで待つ間は写しを持つ
                var payload = PayloadOf(transportEvent);
                Payload = payload.Count == 0 ? Array.Empty<byte>() : payload.AsSpan().ToArray();
            }

            public double Seconds { get; }

            public TransportEventKind Kind { get; }

            public ConnectionId Connection { get; }

            public TransportChannel Channel { get; }

            public byte[] Payload { get; }
        }
    }
}
