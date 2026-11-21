using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 向きを度で送らず脅威の位置を送るのは、自分の向きを知っているのは受け取ったクライアントで、送った後に振り向いても向きを出し直せるようにするため。
    // 誘導弾の Id を添えるのは、飛んでくる間はスナップショットの弾の位置を追って向きを更新できるようにするため。
    public sealed class LockOnWarningMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public const int NoProjectile = -1;

        public ushort Id => MessageIds.LockOnWarning;

        public byte Version => CurrentVersion;

        public LockOnWarningStage Stage { get; set; }

        public int ProjectileId { get; set; } = NoProjectile;

        public Vector3 Source { get; set; }

        public void Write(MessageWriter writer)
        {
            if (Stage != LockOnWarningStage.None && ProjectileId < NoProjectile)
            {
                throw new InvalidOperationException("projectile id must be -1 or more");
            }

            var warning = Stage != LockOnWarningStage.None;
            writer.WriteByte((byte)Stage);
            writer.WriteInt32(warning ? ProjectileId : NoProjectile);
            writer.WriteVector3(warning ? Source : Vector3.Zero);
        }

        public void Read(MessageReader reader)
        {
            Stage = (LockOnWarningStage)reader.ReadByteBelow(EnumRange<LockOnWarningStage>.Count, "lock-on warning stage");
            ProjectileId = reader.ReadInt32AtLeast(NoProjectile, "lock-on warning projectile id");
            Source = reader.ReadFiniteVector3("lock-on warning source");
        }
    }
}
