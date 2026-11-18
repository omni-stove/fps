using System;

namespace Shooter.Core
{
    // オフラインでは種類ごとの値を DeployAbilityDefinition (Unity のアセット) が持つ。Unity 抜きのサーバーはアセットを読めないので、
    // 同じ値をデータから読んでここに持つ。
    public sealed class DeployType
    {
        public DeployType(string id, DeployRole role, DeploySpec deploy, TurretSpec turret = null, string weaponId = null, DecoySpec decoy = null)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (!Enum.IsDefined(typeof(DeployRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role), role, "role is not a defined DeployRole");
            }

            if (role == DeployRole.Turret)
            {
                if (turret == null)
                {
                    throw new ArgumentException("a Turret needs a turret spec", nameof(turret));
                }

                if (string.IsNullOrEmpty(weaponId))
                {
                    throw new ArgumentException("a Turret needs the id of the weapon it fires", nameof(weaponId));
                }
            }
            else if (turret != null || weaponId != null)
            {
                throw new ArgumentException("only a Turret has a turret spec and a weapon", turret != null ? nameof(turret) : nameof(weaponId));
            }

            if (role == DeployRole.Decoy)
            {
                if (decoy == null)
                {
                    throw new ArgumentException("a Decoy needs a decoy spec", nameof(decoy));
                }
            }
            else if (decoy != null)
            {
                throw new ArgumentException("only a Decoy has a decoy spec", nameof(decoy));
            }

            Id = id;
            Role = role;
            Deploy = deploy ?? throw new ArgumentNullException(nameof(deploy));
            Turret = turret;
            WeaponId = weaponId;
            Decoy = decoy;
        }

        public string Id { get; }

        public DeployRole Role { get; }

        public DeploySpec Deploy { get; }

        public TurretSpec Turret { get; }

        public string WeaponId { get; }

        public DecoySpec Decoy { get; }

        // DeploySpec.Default は盾の形と数なので、タレットと囮は各 Spec の既定の形と数に置き換える (DeployAbilityDefinition の派生と同じ)。
        public static DeploySpec DefaultDeploy(DeployRole role)
        {
            switch (role)
            {
                case DeployRole.Turret:
                    return Shaped(TurretSpec.DefaultMaxActive, TurretSpec.DefaultMaxHealth, TurretSpec.DefaultWidth, TurretSpec.DefaultHeight, TurretSpec.DefaultDepth);
                case DeployRole.Decoy:
                    return Shaped(DecoySpec.DefaultMaxActive, DecoySpec.DefaultMaxHealth, DecoySpec.DefaultWidth, DecoySpec.DefaultHeight, DecoySpec.DefaultDepth);
                case DeployRole.Shield:
                    return DeploySpec.Default;
                default:
                    throw new ArgumentOutOfRangeException(nameof(role), role, "role is not a defined DeployRole");
            }
        }

        private static DeploySpec Shaped(int maxActive, int maxHealth, float width, float height, float depth)
        {
            return new DeploySpec(
                maxActive,
                DeploySpec.DefaultOverflow,
                maxHealth,
                DeploySpec.DefaultMaxRange,
                DeploySpec.DefaultMaxSlopeDegrees,
                DeploySpec.DefaultMaxStepHeight,
                width,
                height,
                depth);
        }
    }
}
