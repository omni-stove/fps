using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 差分ではなくいつも全員分を送るのは、出入りが少なく小さいうえ、途中から入った人にも同じ 1 通で済むため。
    public sealed class VoiceRosterMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public const int MaxEntries = NetMatchSettings.MaxMaxPlayers;

        private readonly int[] _playerIds = new int[MaxEntries];
        private readonly string[] _productUserIds = new string[MaxEntries];
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ushort Id => MessageIds.VoiceRoster;

        public byte Version => CurrentVersion;

        public int Count { get; private set; }

        public int PlayerId(int index)
        {
            CheckIndex(index);
            return _playerIds[index];
        }

        public string ProductUserId(int index)
        {
            CheckIndex(index);
            return _productUserIds[index];
        }

        public void Clear()
        {
            Count = 0;
            _seen.Clear();
        }

        public void Add(int playerId, string productUserId)
        {
            if (playerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "a player id must not be negative");
            }

            if (!NetLaunchOptions.IsProductUserId(productUserId))
            {
                throw new ArgumentException("'" + productUserId + "' is not an EOS product user id", nameof(productUserId));
            }

            if (Count == MaxEntries)
            {
                throw new InvalidOperationException("a voice roster holds at most " + MaxEntries + " players");
            }

            if (!_seen.Add(productUserId))
            {
                throw new ArgumentException("product user id " + productUserId + " is already in the roster", nameof(productUserId));
            }

            _playerIds[Count] = playerId;
            _productUserIds[Count] = productUserId;
            Count++;
        }

        public void Write(MessageWriter writer)
        {
            writer.WriteByte((byte)Count);
            for (var i = 0; i < Count; i++)
            {
                writer.WriteInt32(_playerIds[i]);
                writer.WriteString(_productUserIds[i], ConnectionExtras.MaxProductUserIdLength);
            }
        }

        public void Read(MessageReader reader)
        {
            Clear();
            var count = reader.ReadByteBelow(MaxEntries + 1, "voice roster count");
            for (var i = 0; i < count; i++)
            {
                var playerId = reader.ReadInt32AtLeast(0, "voice roster player id");
                var productUserId = reader.ReadString(ConnectionExtras.MaxProductUserIdLength, _productUserIds[i]);
                if (!NetLaunchOptions.IsProductUserId(productUserId))
                {
                    throw new MessageReadException("voice roster entry " + i + " is not an EOS product user id");
                }

                // 1 人の声を 2 人に結び付けると、どちらのチームとして受けるかが決まらないため
                if (!_seen.Add(productUserId))
                {
                    throw new MessageReadException("voice roster entry " + i + " repeats a product user id");
                }

                _playerIds[i] = playerId;
                _productUserIds[i] = productUserId;
                Count = i + 1;
            }
        }

        private void CheckIndex(int index)
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + Count);
            }
        }
    }
}
