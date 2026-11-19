namespace Shooter.Core
{
    public sealed class RedeploySpec
    {
        public const int DefaultCharges = 1;

        // 縮むエリアの段階の番号 (0 始まり)。既定は 2 つ目の円まで。
        public const int DefaultLastPhaseIndex = 1;

        public const float DefaultDelaySeconds = 5f;

        public static RedeploySpec Default { get; } = new RedeploySpec();

        // 戻らない試合 (キットの既定のセッションや、調整値で切ったとき) に使う。
        public static RedeploySpec None { get; } = new RedeploySpec(charges: 0);

        public RedeploySpec(
            int charges = DefaultCharges,
            int lastPhaseIndex = DefaultLastPhaseIndex,
            float delaySeconds = DefaultDelaySeconds)
        {
            Charges = SpecGuard.NonNegative(charges, nameof(charges));
            LastPhaseIndex = SpecGuard.NonNegative(lastPhaseIndex, nameof(lastPhaseIndex));
            DelaySeconds = SpecGuard.NonNegativeFinite(delaySeconds, nameof(delaySeconds));
        }

        // 1 人が 1 試合で戻れる回数。
        public int Charges { get; }

        // この段階の間に倒されたら戻れる。次の段階に入ってから倒されたら戻らない。
        public int LastPhaseIndex { get; }

        // 倒されてから空に出るまで。
        public float DelaySeconds { get; }

        public bool IsEnabled => Charges > 0;
    }
}
