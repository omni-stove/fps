using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 差分ではなくいつも全員分を送るのは、出入りが少なく小さいうえ、途中から入った人にも同じ 1 通で済むため。
    public sealed class VoiceRosterMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        public const int MaxEntries = NetMatchSettings.MaxMaxPlayers;

        private readonly int[] _playerIds = new int[MaxEntries];
        private readonly string[] _productUserIds = new string[MaxEntries];
        private readonly bool[] _verified = new bool[MaxEntries];
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

        // 確かめていない Id は誰でも名乗れるので、ほかの人の Id を名乗った相手を止めたときにその人まで試合をまたいで止めないよう、確かめたかを分けて送る。
        public bool Verified(int index)
        {
            CheckIndex(index);
            return _verified[index];
        }

        public void Clear()
        {
            Count = 0;
            _seen.Clear();
        }

        public void Add(int playerId, string productUserId, bool verified)
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
            _verified[Count] = verified;
            Count++;
        }

        public void Write(MessageWriter writer)
        {
            writer.WriteByte((byte)Count);
            for (var i = 0; i < Count; i++)
            {
                writer.WriteInt32(_playerIds[i]);
                writer.WriteString(_productUserIds[i], ConnectionExtras.MaxProductUserIdLength);
                writer.WriteBool(_verified[i]);
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
                var verified = reader.ReadBool();
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
                _verified[i] = verified;
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
