using System;

namespace Fps.Core
{
    public sealed class HeatAmmo : IAmmoModel
    {
        // 0.1 を 10 回足すと float では 1 にわずかに届かないので、この差までは上限に達したとみなす。
        private const float _fullTolerance = 1e-4f;

        private readonly MagazineAmmo _magazine;
        private float _heat;
        private float _sinceShot;
        private float _overheatRemaining;

        public HeatAmmo(HeatSpec heat, MagazineAmmo magazine)
        {
            Spec = heat ?? throw new ArgumentNullException(nameof(heat));
            _magazine = magazine ?? throw new ArgumentNullException(nameof(magazine));
            _sinceShot = heat.CoolDelaySeconds;
        }

        public event Action Reloaded
        {
            add { }
            remove { }
        }

        public HeatSpec Spec { get; }

        public bool IsOverheated { get; private set; }

        // 弾倉は減らさないので、HUD やセーブには作ったときの値をそのまま返す。
        public int AmmoInMagazine => _magazine.AmmoInMagazine;

        public int ReserveAmmo => _magazine.ReserveAmmo;

        public int MaxReserveAmmo => _magazine.MaxReserveAmmo;

        // 弾薬箱が熱の武器に弾を配っても撃てる量は変わらないので、拾える余地を 0 にして弾を取らせない。
        public int ReserveAmmoRoom => 0;

        public bool HasInfiniteReserve => _magazine.HasInfiniteReserve;

        public bool HasActiveReload => false;

        public bool HasJudgedActiveReload => false;

        public bool IsReloading => false;

        public float ReloadProgress => 0f;

        public float? HeatFraction => _heat;

        public bool CanFire => !IsOverheated;

        public void Consume()
        {
            if (!CanFire)
            {
                throw new InvalidOperationException("cannot fire while overheated");
            }

            _heat = Math.Min(1f, _heat + Spec.HeatPerShot);
            _sinceShot = 0f;
            if (_heat < 1f - _fullTolerance)
            {
                return;
            }

            _heat = 1f;
            IsOverheated = true;
            _overheatRemaining = Spec.OverheatSeconds;
        }

        public int AddReserveAmmo(int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            return 0;
        }

        public bool TryStartReload()
        {
            return false;
        }

        public bool TryActiveReload(out ActiveReloadResult result)
        {
            result = ActiveReloadResult.Fail;
            return false;
        }

        public void CancelReload()
        {
        }

        public void Unload()
        {
            _magazine.Unload();
        }

        public void Tick(float deltaSeconds)
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

            _heat = Math.Max(0f, _heat - (Spec.CoolPerSecond * cooling));
            if (IsOverheated && _heat <= 0f)
            {
                IsOverheated = false;
            }
        }
    }
}
