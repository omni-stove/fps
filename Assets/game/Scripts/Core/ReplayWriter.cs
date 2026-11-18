using System;
using System.IO;

namespace Shooter.Core
{
    public sealed class ReplayWriter : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private readonly long _headerStart;
        private readonly byte[] _recordHeader = new byte[ReplayFile.RecordHeaderBytes];
        private bool _finished;
        private bool _disposed;

        public ReplayWriter(Stream stream, ReplayHeader header, bool leaveOpen = false)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Header = header ?? throw new ArgumentNullException(nameof(header));
            if (!stream.CanWrite)
            {
                throw new ArgumentException("the replay stream must be writable", nameof(stream));
            }

            _leaveOpen = leaveOpen;
            _headerStart = stream.CanSeek ? stream.Position : -1;
            ReplayFile.WriteHeader(stream, header.WithDuration(ReplayHeader.UnknownDuration));
        }

        public ReplayHeader Header { get; }

        public int LastTick { get; private set; }

        public int Count { get; private set; }

        public void Append(int tick, ReplayRecordKind kind, ulong connection, byte channel, ArraySegment<byte> payload)
        {
            ThrowIfFinished();
            if (tick < LastTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tick), tick, "records go in tick order; the last one was tick " + ReplayFile.Text(LastTick));
            }

            if (!Enum.IsDefined(typeof(ReplayRecordKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "kind must be a defined ReplayRecordKind");
            }

            if (connection == 0)
            {
                throw new ArgumentException("a record needs a connection", nameof(connection));
            }

            var count = payload.Array == null ? 0 : payload.Count;
            if (count > ReplayFile.MaxPayloadBytes)
            {
                throw new ArgumentException(
                    "payload is " + ReplayFile.Text(count) + " bytes; at most " + ReplayFile.Text(ReplayFile.MaxPayloadBytes) + " fit in a record",
                    nameof(payload));
            }

            ReplayFile.WriteRecordHeader(_recordHeader, tick, kind, connection, channel, count);
            _stream.Write(_recordHeader, 0, _recordHeader.Length);
            if (count > 0)
            {
                _stream.Write(payload.Array, payload.Offset, count);
            }

            LastTick = tick;
            Count++;
        }

        // 書き戻せない (シークできない) 流れでは長さを不明のまま残し、読む側が最後の記録から数える。
        public void Finish(int durationTicks)
        {
            if (_finished)
            {
                return;
            }

            if (durationTicks < LastTick)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationTicks), durationTicks, "the replay lasts at least until its last record (tick " + ReplayFile.Text(LastTick) + ")");
            }

            _finished = true;
            if (_headerStart >= 0)
            {
                ReplayFile.WriteDuration(_stream, _headerStart, durationTicks);
            }

            _stream.Flush();
        }

        public void Flush()
        {
            ThrowIfFinished();
            _stream.Flush();
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
                Finish(LastTick);
            }
            finally
            {
                if (!_leaveOpen)
                {
                    _stream.Dispose();
                }
            }
        }

        private void ThrowIfFinished()
        {
            if (_finished)
            {
                throw new InvalidOperationException("the replay is already finished");
            }
        }
    }
}
