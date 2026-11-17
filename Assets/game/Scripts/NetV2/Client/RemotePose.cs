using System.Numerics;
using Shooter.Core;

namespace Shooter.NetV2
{
    public readonly struct RemotePose
    {
        public RemotePose(Vector3 position, float yaw, PlayerStance stance, string weaponId)
            : this(position, yaw, stance, weaponId, 0f, false, WallRunSide.None)
        {
        }

        public RemotePose(Vector3 position, float yaw, PlayerStance stance, string weaponId, float lean, bool swimming, WallRunSide wallRun)
        {
            Position = position;
            Yaw = yaw;
            Stance = stance;
            WeaponId = weaponId ?? "";

            // 見た目の傾け (PlayerMockPose.TraversalLean) は範囲の外と NaN で投げるので、届いた値はここで収める
            Lean = float.IsNaN(lean) ? 0f : (lean < -1f ? -1f : (lean > 1f ? 1f : lean));
            Swimming = swimming;
            WallRun = wallRun <= WallRunSide.Right ? wallRun : WallRunSide.None;
        }

        public Vector3 Position { get; }

        public float Yaw { get; }

        public PlayerStance Stance { get; }

        public bool IsCrouched => Stance == PlayerStance.Crouching;

        public bool IsProne => Stance == PlayerStance.Prone;

        public string WeaponId { get; }

        public float Lean { get; }

        public bool Swimming { get; }

        public WallRunSide WallRun { get; }

        // 伏せとしゃがみはサーバーが同時に立てないので、伏せを先に見る。
        public static RemotePose From(in Shooter.Protocol.PlayerSnapshot row)
        {
            var stance = row.IsProne ? PlayerStance.Prone : row.IsCrouched ? PlayerStance.Crouching : PlayerStance.Standing;
            return new RemotePose(row.Position, row.Yaw, stance, row.WeaponId, row.Lean, row.Swimming, (WallRunSide)(byte)row.WallRunSide);
        }
    }
}
