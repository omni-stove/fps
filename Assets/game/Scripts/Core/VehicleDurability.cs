using System;

namespace Shooter.Core
{
    // 耐久そのものを持たないのは、Damageable が持つ体力と二重に持って食い違わないようにするため。
    public sealed class VehicleDurability
    {
        private double _burnCarry;

        public VehicleDurability(VehicleDurabilitySpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public VehicleDurabilitySpec Spec { get; private set; }

        public VehicleDamageStage Stage { get; private set; } = VehicleDamageStage.Intact;

        public bool IsDestroyed => Stage == VehicleDamageStage.Destroyed;

        public void Configure(VehicleDurabilitySpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _burnCarry = 0d;
        }

        public bool Observe(int health, int maxHealth)
        {
            if (IsDestroyed)
            {
                return false;
            }

            var next = Spec.StageFor(health, maxHealth);
            if (next == Stage)
            {
                return false;
            }

            Stage = next;
            if (next != VehicleDamageStage.Burning)
            {
                _burnCarry = 0d;
            }

            return true;
        }

        public bool MarkDestroyed()
        {
            if (IsDestroyed)
            {
                return false;
            }

            Stage = VehicleDamageStage.Destroyed;
            _burnCarry = 0d;
            return true;
        }

        // 1 に満たない端数を持ち越すのは、毎フレームの減りを切り捨てると高いフレームレートほど燃え方が遅くなるため。
        public int TickBurn(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (Stage != VehicleDamageStage.Burning)
            {
                return 0;
            }

            _burnCarry += (double)Spec.BurnDamagePerSecond * deltaSeconds;
            var whole = Math.Floor(_burnCarry);
            _burnCarry -= whole;
            return whole >= int.MaxValue ? int.MaxValue : (int)whole;
        }
    }
}
