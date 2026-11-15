namespace Fps.Core
{
    public interface ITriggerModel
    {
        float MinShotSpacingSeconds { get; }

        bool BlocksAds { get; }

        float ChargeFraction { get; }

        int Press();

        int Release();

        int Tick(float deltaSeconds);

        void Cancel();
    }
}
