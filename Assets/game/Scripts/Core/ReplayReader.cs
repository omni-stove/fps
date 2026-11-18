using System;
using System.Buffers.Binary;
using System.IO;

namespace Shooter.Core
{
    // 記録の途中でアプリが落ちたファイルは最後の記録が欠けるので、欠けた記録は投げずに終わりとして扱い IsTruncated で知らせる。
    // 中身の値がおかしい (知らない種類・tick が戻る・長すぎる) ものは書いた側の誤りか別のファイルなので InvalidDataException を投げる。
    public sealed class ReplayReader : IDisposable
    {
        private readonly Stream _stream;
        private readonly bool _leaveOpen;
        private readonly byte[] _recordHeader = new byte[ReplayFile.RecordHeaderBytes];
        private byte[] _payload = new byte[256];
        private bool _ended;
        private bool _disposed;

        public ReplayReader(Stream stream, bool leaveOpen = false)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (!stream.CanRead)
            {
                throw new ArgumentException("the replay stream must be readable", nameof(stream));
            }

            _leaveOpen = leaveOpen;
            try
            {
                Header = ReplayFile.ReadHeader(stream);
            }
            catch
            {
                // 渡された流れを持つと決めたので、読めなかったときも閉じてファイルを掴んだままにしない
                if (!leaveOpen)
                {
                    stream.Dispose();
                }

                throw;
            }
        }

        public ReplayHeader Header { get; }

        public int LastTick { get; private set; }

        public int Count { get; private set; }

        public bool IsEnded => _ended;

        public bool IsTruncated { get; private set; }

        public bool TryRead(out ReplayRecord record)
        {
            record = default;
            if (!TryReadRecordHeader(out var tick, out var kind, out var connection, out var channel, out var length))
            {
                return false;
            }

            if (_payload.Length < length)
            {
                _payload = new byte[Math.Max(length, _payload.Length * 2)];
            }

            if (ReplayFile.Fill(_stream, _payload, 0, length) != length)
            {
                EndTruncated();
                return false;
            }

            record = new ReplayRecord(tick, kind, connection, channel, new ArraySegment<byte>(_payload, 0, length));
            Accept(tick);
            return true;
        }

        // 一覧の長さを数えるためだけに読むので、シークできる流れでは中身を読まずに飛ばす。
        public int SkipToEnd()
        {
            while (TryReadRecordHeader(out var tick, out _, out _, out _, out var length))
            {
                if (!Skip(length))
                {
                    EndTruncated();
                    break;
                }

                Accept(tick);
            }

            return LastTick;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!_leaveOpen)
            {
                _stream.Dispose();
            }
        }

        private bool TryReadRecordHeader(out int tick, out ReplayRecordKind kind, out ulong connection, out byte channel, out int length)
        {
            tick = 0;
            kind = default;
            connection = 0;
            channel = 0;
            length = 0;
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ReplayReader));
            }

            if (_ended)
            {
                return false;
            }

            var read = ReplayFile.Fill(_stream, _recordHeader, 0, _recordHeader.Length);
            if (read == 0)
            {
                _ended = true;
                return false;
            }

            if (read != _recordHeader.Length)
            {
                EndTruncated();
                return false;
            }

            var span = new ReadOnlySpan<byte>(_recordHeader);
            tick = BinaryPrimitives.ReadInt32LittleEndian(span);
            var kindByte = span[4];
            connection = BinaryPrimitives.ReadUInt64LittleEndian(span.Slice(5));
            channel = span[13];
            length = BinaryPrimitives.ReadInt32LittleEndian(span.Slice(14));
            var at = "record " + ReplayFile.Text(Count + 1);
            if (tick < LastTick)
            {
                throw new InvalidDataException(at + " goes back to tick " + ReplayFile.Text(tick) + " after tick " + ReplayFile.Text(LastTick));
            }

            if (!Enum.IsDefined(typeof(ReplayRecordKind), kindByte))
            {
                throw new InvalidDataException(at + " has an unknown kind " + ReplayFile.Text(kindByte));
            }

            if (connection == 0)
            {
                throw new InvalidDataException(at + " has no connection");
            }

            if (length < 0 || length > ReplayFile.MaxPayloadBytes)
            {
                throw new InvalidDataException(
                    at + " is " + ReplayFile.Text(length) + " bytes; at most " + ReplayFile.Text(ReplayFile.MaxPayloadBytes) + " are read");
            }

            kind = (ReplayRecordKind)kindByte;
            return true;
        }

        private bool Skip(int length)
        {
            if (_stream.CanSeek)
            {
                if (_stream.Length - _stream.Position < length)
                {
                    return false;
                }

                _stream.Seek(length, SeekOrigin.Current);
                return true;
            }

            if (_payload.Length < length)
            {
                _payload = new byte[length];
            }

            return ReplayFile.Fill(_stream, _payload, 0, length) == length;
        }

        private void Accept(int tick)
        {
            LastTick = tick;
            Count++;
        }

        private void EndTruncated()
        {
            _ended = true;
            IsTruncated = true;
        }
    }
}
