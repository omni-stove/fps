using System;
using System.Globalization;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // ReplayRecorder が残したファイルを、残した tick の順に、再生の時計 (Advance) が着いた分だけ知らせとして返す。
    // MatchClient にそのまま渡すと、試合中と同じく承認・スナップショット・イベントを読んで部品を動かす。
    // 送られたもの (承認の求め・入力) は相手がいないので捨てる。
    public sealed class ReplayTransport : IMatchTransport
    {
        public const string EndedReason = "replay ended";

        public const string DamagedReason = "replay damaged";

        public const float MaxSpeed = 16f;

        private readonly ReplayReader _reader;
        private readonly Action<string> _log;
        private ReplayRecord _next;
        private bool _hasNext;
        private bool _readFailed;
        private bool _endSent;
        private ConnectionId _connection = new ConnectionId(1);
        private float _speed = 1f;
        private bool _disposed;

        // ヘッダーはここで読むので、壊れたファイルや版の違うファイルは試合の画面を読む前に InvalidDataException で断れる。
        public ReplayTransport(Stream input, Action<string> log)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            _log = log ?? throw new ArgumentNullException(nameof(log));
            _reader = new ReplayReader(input);
            try
            {
                CheckProtocol(_reader.Header);
            }
            catch
            {
                _reader.Dispose();
                throw;
            }
        }

        public ReplayHeader Header => _reader.Header;

        public bool IsActive { get; private set; }

        public bool IsStarted { get; private set; }

        public bool IsEnded { get; private set; }

        public double PlaybackTick { get; private set; }

        public float PlaybackSeconds => (float)(PlaybackTick / Header.TickRate);

        public int PlayedEvents { get; private set; }

        public bool Paused { get; set; }

        public float Speed
        {
            get => _speed;
            set
            {
                if (!(value > 0f) || value > MaxSpeed)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value), value, "speed must be above 0 and at most " + MaxSpeed.ToString(CultureInfo.InvariantCulture));
                }

                _speed = value;
            }
        }

        public static void CheckProtocol(ReplayHeader header)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (header.ProtocolVersion != MatchProtocol.Version)
            {
                throw new InvalidDataException(
                    "the replay was recorded with protocol " + header.ProtocolVersion.ToString(CultureInfo.InvariantCulture)
                    + " (client " + header.ClientVersion + ") but this build reads protocol "
                    + MatchProtocol.Version.ToString(CultureInfo.InvariantCulture));
            }
        }

        public void Listen(string address, ushort port)
        {
            throw new InvalidOperationException("a replay plays back a client's connection; it does not listen");
        }

        // 行き先は使わない。ファイルの中の接続を最初から流し直す。
        public void Connect(string address, ushort port)
        {
            ThrowIfDisposed();
            if (IsStarted)
            {
                throw new InvalidOperationException("a replay plays only once; open the file again to watch it again");
            }

            IsStarted = true;
            IsActive = true;
            PlaybackTick = 0d;
            _log(ReplayLog.PlaybackStarted(Header));
        }

        public void Disconnect(ConnectionId connection, string reason)
        {
            Stop(reason);
        }

        public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
        {
        }

        public int GetMaxPayloadSize(TransportChannel channel)
        {
            if (!Enum.IsDefined(typeof(TransportChannel), channel))
            {
                throw new ArgumentOutOfRangeException(nameof(channel), channel, "unknown channel");
            }

            return MessageWriter.DefaultMaxLength;
        }

        public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
        {
            milliseconds = 0;
            return false;
        }

        public void Advance(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be zero or more and finite");
            }

            if (!IsActive || Paused)
            {
                return;
            }

            PlaybackTick += deltaSeconds * (double)_speed * Header.TickRate;
        }

        public bool TryReceive(out TransportEvent transportEvent)
        {
            transportEvent = default;
            if (!IsActive)
            {
                return false;
            }

            if (!Peek())
            {
                return SendEnd(out transportEvent);
            }

            if (_next.Tick > PlaybackTick)
            {
                return false;
            }

            _hasNext = false;
            PlayedEvents++;
            var connection = new ConnectionId(_next.Connection);
            _connection = connection;
            switch (_next.Kind)
            {
                case ReplayRecordKind.Connected:
                    transportEvent = TransportEvent.Connected(connection);
                    return true;
                case ReplayRecordKind.Disconnected:
                    _endSent = true;
                    transportEvent = TransportEvent.Disconnected(connection, TransportReason.Decode(_next.Payload));
                    Finish();
                    return true;
                default:
                    if (!Enum.IsDefined(typeof(TransportChannel), (int)_next.Channel))
                    {
                        Damaged("record " + PlayedEvents.ToString(CultureInfo.InvariantCulture) + " uses an unknown channel");
                        return SendEnd(out transportEvent);
                    }

                    transportEvent = TransportEvent.Data(connection, (TransportChannel)_next.Channel, _next.Payload);
                    return true;
            }
        }

        public void Shutdown(string reason)
        {
            Stop(reason);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop(null);
            _disposed = true;
            _reader.Dispose();
        }

        private bool Peek()
        {
            if (_hasNext)
            {
                return true;
            }

            if (_readFailed)
            {
                return false;
            }

            try
            {
                _hasNext = _reader.TryRead(out _next);
            }
            catch (Exception e) when (e is InvalidDataException || e is IOException || e is ObjectDisposedException)
            {
                Damaged(e.Message);
            }

            return _hasNext;
        }

        // 記録した人が自分で抜けた試合は切断の知らせが残らないので、終わりに着いたら MatchClient に切れたと知らせて試合を閉じさせる。
        private bool SendEnd(out TransportEvent transportEvent)
        {
            transportEvent = default;
            if (_endSent)
            {
                Finish();
                return false;
            }

            _endSent = true;
            transportEvent = TransportEvent.Disconnected(_connection, _readFailed ? DamagedReason : EndedReason);
            Finish();
            return true;
        }

        private void Damaged(string message)
        {
            _readFailed = true;
            _hasNext = false;
            _log(ReplayLog.PlaybackFailed(message));
        }

        private void Finish()
        {
            if (IsEnded)
            {
                return;
            }

            IsEnded = true;
            IsActive = false;
            _log(ReplayLog.PlaybackEnded(PlayedEvents, _reader.IsTruncated));
        }

        private void Stop(string reason)
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            _log(ReplayLog.PlaybackStopped(reason));
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ReplayTransport));
            }
        }
    }
}
