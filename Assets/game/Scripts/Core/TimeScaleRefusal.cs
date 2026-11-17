namespace Shooter.Core
{
    public enum TimeScaleRefusal
    {
        None,

        RuleOff,

        // ネット対戦では全員の時間が変わるので、ルールが On でも断る。
        NetworkSession,
    }
}
