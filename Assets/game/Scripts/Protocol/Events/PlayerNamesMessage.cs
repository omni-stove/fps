using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 差分ではなくいつも全員分を送るのは、VoiceRosterMessage と同じく出入りが少なく小さいうえ、途中から入った人にも同じ 1 通で済むため。
    public sealed class PlayerNamesMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public const int MaxEntries = NetMatchSettings.MaxMaxPlayers;

        private readonly int[] _playerIds = new int[MaxEntries];
        private readonly string[] _names = new string[MaxEntries];
        private readonly HashSet<int> _seen = new HashSet<int>();

        public ushort Id => MessageIds.PlayerNames;

        public byte Version => CurrentVersion;

        public int Count { get; private set; }

        public int PlayerId(int index)
        {
            CheckIndex(index);
            return _playerIds[index];
        }

        public string Name(int index)
        {
            CheckIndex(index);
            return _names[index];
        }

        public void Clear()
        {
            Count = 0;
            _seen.Clear();
        }

        public void Add(int playerId, string name)
        {
            if (playerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "a player id must not be negative");
            }

            // 受け手は読むときにも同じ決まりで断るので、送る前に止めて名簿ごと読めなくならないようにする
            if (!PlayerDisplayName.IsValid(name, NetMatchSettings.MaxMaxDisplayNameLength))
            {
                throw new ArgumentException("'" + name + "' is not a display name that can be sent", nameof(name));
            }

            if (Count == MaxEntries)
            {
                throw new InvalidOperationException("a name roster holds at most " + MaxEntries + " players");
            }

            if (!_seen.Add(playerId))
            {
                throw new ArgumentException("player " + playerId + " is already in the roster", nameof(playerId));
            }

            _playerIds[Count] = playerId;
            _names[Count] = name;
            Count++;
        }

        public void Write(MessageWriter writer)
        {
            writer.WriteByte((byte)Count);
            for (var i = 0; i < Count; i++)
            {
                writer.WriteInt32(_playerIds[i]);
                writer.WriteString(_names[i], PlayerDisplayName.MaxBytes);
            }
        }

        public void Read(MessageReader reader)
        {
            Clear();
            var count = reader.ReadByteBelow(MaxEntries + 1, "name roster count");
            for (var i = 0; i < count; i++)
            {
                var playerId = reader.ReadInt32AtLeast(0, "name roster player id");
                var name = reader.ReadString(PlayerDisplayName.MaxBytes, _names[i]);

                // サーバーの設定の上限は受け手に届かないので、どの設定でも送られうる長さまでを認める
                if (!PlayerDisplayName.IsValid(name, NetMatchSettings.MaxMaxDisplayNameLength))
                {
                    throw new MessageReadException("name roster entry " + i + " is not a valid display name");
                }

                // 1 人に 2 つの名前を結び付けると、どちらを出すかが決まらないため
                if (!_seen.Add(playerId))
                {
                    throw new MessageReadException("name roster entry " + i + " repeats player " + playerId);
                }

                _playerIds[i] = playerId;
                _names[i] = name;
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
