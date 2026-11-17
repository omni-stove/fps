namespace Shooter.Core
{
    public interface ITriggerModel
    {
        float MinShotSpacingSeconds { get; }

        bool BlocksAds { get; }

        float ChargeFraction { get; }

        float SpinFraction { get; }

        int Press();

        int Release();

        int Tick(float deltaSeconds);

        void Cancel();

        // Cancel は撃てない間に毎フレーム呼ばれ回転などを徐々に落とすので、持ち替えでしまって時間が止まる分は別に戻す。
        void Stow();
    }
}
