using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
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

        public RoundSpec Round { get; }

        public ImpactSpec Impact { get; }

        public float ChargeFraction { get; }

        public int? LockTargetId { get; }

        public float Tick { get; }

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
