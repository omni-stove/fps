using System.Numerics;
using Fps.Core;

namespace Fps.NetV2
{
    /// <summary>他のプレイヤーの、ある tick の見た目の姿。</summary>
    public readonly struct RemotePose
    {
        public RemotePose(Vector3 position, float yaw, PlayerStance stance, string weaponId)
        {
            Position = position;
            Yaw = yaw;
            Stance = stance;
            WeaponId = weaponId ?? "";
        }

        /// <summary>本体 (ルートの Transform) の位置。サーバーの移動の <c>CharacterMotorState.Position</c> と同じもの。</summary>
        public Vector3 Position { get; }

        /// <summary>向き (Y 軸まわり、ラジアン)。</summary>
        public float Yaw { get; }

        public PlayerStance Stance { get; }

        public bool IsCrouched => Stance == PlayerStance.Crouching;

        public bool IsProne => Stance == PlayerStance.Prone;

        /// <summary>持っている武器の Id。持っていなければ空。</summary>
        public string WeaponId { get; }

        /// <summary>行の姿。伏せとしゃがみはサーバーが同時に立てないので、伏せを先に見る。</summary>
        public static RemotePose From(in Fps.Protocol.PlayerSnapshot row)
        {
            var stance = row.IsProne ? PlayerStance.Prone : row.IsCrouched ? PlayerStance.Crouching : PlayerStance.Standing;
            return new RemotePose(row.Position, row.Yaw, stance, row.WeaponId);
        }
    }
}
