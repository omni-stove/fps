using System;

namespace Fps.Core
{
    // 弾薬 (HeatAmmo) から熱だけを切り出すのは、固定銃座のように熱が上がった・冷めたの切り替わりを知りたい側が、弾倉の扱いを経ずに読めるようにするため。
    public sealed class WeaponHeat
    {
        // 0.1 を 10 回足すと float では 1 にわずかに届かないので、この差までは上限に達したとみなす。
        private const float _fullTolerance = 1e-4f;

        private float _sinceShot;
        private float _overheatRemaining;

        public WeaponHeat(HeatSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _sinceShot = spec.CoolDelaySeconds;
        }

        public HeatSpec Spec { get; }

        public float Fraction { get; private set; }

        public bool IsOverheated { get; private set; }

        public bool CanFire => !IsOverheated;

        public float OverheatRemainingSeconds => IsOverheated ? _overheatRemaining : 0f;

        // 上限に届いて撃てなくなった 1 発なら true。
        public bool AddShot()
        {
            if (!CanFire)
            {
                throw new InvalidOperationException("cannot fire while overheated");
            }

            Fraction = Math.Min(1f, Fraction + Spec.HeatPerShot);
            _sinceShot = 0f;
            if (Fraction < 1f - _fullTolerance)
            {
                return false;
            }

            Fraction = 1f;
            IsOverheated = true;
            _overheatRemaining = Spec.OverheatSeconds;
            return true;
        }

        // 冷めきってまた撃てるようになった tick なら true。
        public bool Tick(float deltaSeconds)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            float cooling;
            if (IsOverheated)
            {
                var held = Math.Min(deltaSeconds, _overheatRemaining);
                _overheatRemaining -= held;
                cooling = deltaSeconds - held;
            }
            else
            {
                var before = _sinceShot;
                _sinceShot = Math.Min(Spec.CoolDelaySeconds, _sinceShot + deltaSeconds);
                cooling = deltaSeconds - (_sinceShot - before);
            }

            Fraction = Math.Max(0f, Fraction - (Spec.CoolPerSecond * cooling));
            if (!IsOverheated || Fraction > 0f)
            {
                return false;
            }

            IsOverheated = false;
            return true;
        }

        public void Reset()
        {
            Fraction = 0f;
            IsOverheated = false;
            _overheatRemaining = 0f;
            _sinceShot = Spec.CoolDelaySeconds;
        }
    }
}
