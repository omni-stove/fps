using System.Numerics;
using Shooter.Core;

namespace Shooter.NetV2
{
    public readonly struct RemotePose
    {
        public RemotePose(Vector3 position, float yaw, PlayerStance stance, string weaponId)
        {
            Position = position;
            Yaw = yaw;
            Stance = stance;
            WeaponId = weaponId ?? "";
        }

        public Vector3 Position { get; }

        public float Yaw { get; }

        public PlayerStance Stance { get; }

        public bool IsCrouched => Stance == PlayerStance.Crouching;

        public bool IsProne => Stance == PlayerStance.Prone;

        public string WeaponId { get; }

        // 伏せとしゃがみはサーバーが同時に立てないので、伏せを先に見る。
        public static RemotePose From(in Shooter.Protocol.PlayerSnapshot row)
        {
            var stance = row.IsProne ? PlayerStance.Prone : row.IsCrouched ? PlayerStance.Crouching : PlayerStance.Standing;
            return new RemotePose(row.Position, row.Yaw, stance, row.WeaponId);
        }
    }
}
