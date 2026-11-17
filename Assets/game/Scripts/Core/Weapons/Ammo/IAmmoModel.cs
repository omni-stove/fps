using System;

namespace Shooter.Core
{
    public interface IAmmoModel
    {
        event Action Reloaded;

        int AmmoInMagazine { get; }

        int ReserveAmmo { get; }

        int MaxReserveAmmo { get; }

        int ReserveAmmoRoom { get; }

        int StartingReserveAmmo { get; }

        bool HasInfiniteReserve { get; }

        bool HasActiveReload { get; }

        bool HasJudgedActiveReload { get; }

        bool IsReloading { get; }

        float ReloadProgress { get; }

        // 熱で撃つ弾薬だけが値を持つ。弾倉の弾薬に 0 を返させると、HUD が空の熱のゲージを出してしまう。
        float? HeatFraction { get; }

        bool CanFire { get; }

        void Consume();

        int AddReserveAmmo(int amount);

        bool UsePool(IAmmoPool pool);

        bool TryStartReload();

        bool TryActiveReload(out ActiveReloadResult result);

        void CancelReload();

        void Unload();

        void Tick(float deltaSeconds);
    }
}
