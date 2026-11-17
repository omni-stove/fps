using System;
using System.Globalization;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Protocol
{
    // ペレット・貫通・跳弾で 1 回の射撃が何本にも分かれても 1 つで送るよう、弾の区間を並び (Segment) で持つ。
    // Origin から ImpactZone までが最初の区間を読み書きするのは、1 本だけを撃つ呼び出し側が区間の並びを知らずに済むようにするため。
    public sealed class ShotMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        public const int MaxSegments = 64;

        private ShotSegment[] _segments = new ShotSegment[1];

        public ushort Id => MessageIds.Shot;

        public byte Version => CurrentVersion;

        public bool HasShooter { get; set; }

        public int ShooterId { get; set; }

        public string WeaponId { get; set; } = "";

        public int SegmentCount { get; private set; } = 1;

        public Vector3 Origin
        {
            get => _segments[0].Origin;
            set => _segments[0].Origin = value;
        }

        public Vector3 Direction
        {
            get => _segments[0].Direction;
            set => _segments[0].Direction = value;
        }

        public bool HasImpact
        {
            get => _segments[0].HasImpact;
            set => _segments[0].HasImpact = value;
        }

        public Vector3 ImpactPoint
        {
            get => _segments[0].ImpactPoint;
            set => _segments[0].ImpactPoint = value;
        }

        public Vector3 ImpactNormal
        {
            get => _segments[0].ImpactNormal;
            set => _segments[0].ImpactNormal = value;
        }

        public string ImpactMaterialId
        {
            get => _segments[0].ImpactMaterialId ?? "";
            set => _segments[0].ImpactMaterialId = value;
        }

        public bool ImpactIsCharacter
        {
            get => _segments[0].ImpactIsCharacter;
            set => _segments[0].ImpactIsCharacter = value;
        }

        public bool HasImpactZone
        {
            get => _segments[0].HasImpactZone;
            set => _segments[0].HasImpactZone = value;
        }

        public HitZone ImpactZone
        {
            get => _segments[0].ImpactZone;
            set => _segments[0].ImpactZone = value;
        }

        public ref ShotSegment Segment(int index)
        {
            if ((uint)index >= (uint)SegmentCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + Text(SegmentCount));
            }

            return ref _segments[index];
        }

        public ref ShotSegment AddSegment()
        {
            if (SegmentCount == MaxSegments)
            {
                throw new InvalidOperationException("at most " + Text(MaxSegments) + " segments fit in a shot");
            }

            Grow(SegmentCount + 1);
            SegmentCount++;
            _segments[SegmentCount - 1] = default;
            return ref _segments[SegmentCount - 1];
        }

        public void ResetSegments()
        {
            SegmentCount = 1;
            _segments[0] = default;
        }

        public void Write(MessageWriter writer)
        {
            if (HasShooter && ShooterId < 0)
            {
                throw new InvalidOperationException("shooter id must not be negative");
            }

            writer.WriteInt32(HasShooter ? ShooterId : PlayerIds.None);
            writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
            writer.WriteByte((byte)SegmentCount);
            for (var i = 0; i < SegmentCount; i++)
            {
                _segments[i].Write(writer);
            }
        }

        public void Read(MessageReader reader)
        {
            var shooter = reader.ReadInt32AtLeast(PlayerIds.None, "shooter");
            HasShooter = shooter != PlayerIds.None;
            ShooterId = HasShooter ? shooter : 0;
            WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
            var count = reader.ReadByteBelow(MaxSegments + 1, "shot segment count");
            if (count == 0)
            {
                throw new MessageReadException("a shot must carry at least one segment");
            }

            Grow(count);
            for (var i = 0; i < count; i++)
            {
                _segments[i].Read(reader);
            }

            SegmentCount = count;
        }

        private void Grow(int count)
        {
            if (count > _segments.Length)
            {
                Array.Resize(ref _segments, Math.Max(count, Math.Min(MaxSegments, _segments.Length * 2)));
            }
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
