using System;

namespace Fps.Core
{
    public sealed class HeatAmmo : IAmmoModel
    {
        private readonly MagazineAmmo _magazine;

        public HeatAmmo(HeatSpec heat, MagazineAmmo magazine)
        {
            Spec = heat ?? throw new ArgumentNullException(nameof(heat));
            _magazine = magazine ?? throw new ArgumentNullException(nameof(magazine));
        }

        public event Action Reloaded
        {
            add => _magazine.Reloaded += value;
            remove => _magazine.Reloaded -= value;
        }

        public HeatSpec Spec { get; }

        public int AmmoInMagazine => _magazine.AmmoInMagazine;

        public int ReserveAmmo => _magazine.ReserveAmmo;

        public int MaxReserveAmmo => _magazine.MaxReserveAmmo;

        public int ReserveAmmoRoom => _magazine.ReserveAmmoRoom;

        public bool HasInfiniteReserve => _magazine.HasInfiniteReserve;

        public bool HasActiveReload => _magazine.HasActiveReload;

        public bool HasJudgedActiveReload => _magazine.HasJudgedActiveReload;

        public bool IsReloading => _magazine.IsReloading;

        public float ReloadProgress => _magazine.ReloadProgress;

        public float? HeatFraction => _magazine.HeatFraction;

        public bool CanFire => _magazine.CanFire;

        public void Consume()
        {
            _magazine.Consume();
        }

        public int AddReserveAmmo(int amount)
        {
            return _magazine.AddReserveAmmo(amount);
        }

        public bool TryStartReload()
        {
            return _magazine.TryStartReload();
        }

        public bool TryActiveReload(out ActiveReloadResult result)
        {
            return _magazine.TryActiveReload(out result);
        }

        public void CancelReload()
        {
            _magazine.CancelReload();
        }

        public void Unload()
        {
            _magazine.Unload();
        }

        public void Tick(float deltaSeconds)
        {
            _magazine.Tick(deltaSeconds);
        }
    }
}
