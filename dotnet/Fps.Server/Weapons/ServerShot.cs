using System;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// サーバーが受け入れた射撃 1 回。弾の届け方 (<see cref="ServerShotTrace"/>・<see cref="ServerProjectiles"/>・<see cref="ServerBeams"/>・
    /// <see cref="ServerSpray"/>) と着弾 (<see cref="ServerImpact"/>) に、撃った人・武器・選んだ弾薬の種類の値・溜めを渡す。
    /// </summary>
    internal readonly struct ServerShot
    {
        public ServerShot(
            int shooterId,
            WeaponSpec weapon,
            RoundSpec round,
            ImpactSpec impact,
            float chargeFraction,
            int? lockTargetId,
            float tick,
            Vector3 shooterPosition,
            TriggerEdge trigger)
        {
            ShooterId = shooterId;
            Weapon = weapon ?? throw new ArgumentNullException(nameof(weapon));
            Round = round ?? throw new ArgumentNullException(nameof(round));
            Impact = impact ?? throw new ArgumentNullException(nameof(impact));
            ChargeFraction = chargeFraction;
            LockTargetId = lockTargetId;
            Tick = tick;
            ShooterPosition = shooterPosition;
            Trigger = trigger;
        }

        public int ShooterId { get; }

        public WeaponSpec Weapon { get; }

        /// <summary>弾薬の種類で上書きした後の弾の値。</summary>
        public RoundSpec Round { get; }

        /// <summary>弾薬の種類で上書きした後の着弾の値。</summary>
        public ImpactSpec Impact { get; }

        /// <summary>サーバーが受け入れた溜め (0〜1)。溜めの無い武器は 0。</summary>
        public float ChargeFraction { get; }

        /// <summary>サーバーが確かめたロックオンの相手。いなければ null。</summary>
        public int? LockTargetId { get; }

        /// <summary>撃った人が見ていた tick (補間の割合を含む)。巻き戻して判定する届け方だけが使う。</summary>
        public float Tick { get; }

        /// <summary>撃った人のサーバーの知る位置。被弾の向きの元に使う。</summary>
        public Vector3 ShooterPosition { get; }

        public TriggerEdge Trigger { get; }

        public ServerShot WithDamageMultiplier(float multiplier)
        {
            return multiplier == 1f
                ? this
                : new ServerShot(
                    ShooterId, Weapon, Round, Impact.WithDamageMultiplier(multiplier), ChargeFraction, LockTargetId, Tick, ShooterPosition, Trigger);
        }
    }
}
