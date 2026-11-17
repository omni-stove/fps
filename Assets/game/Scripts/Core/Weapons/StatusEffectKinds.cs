namespace Shooter.Core
{
    // 種類を enum にしないのは、ゲームが Core を変えずに種類を足せ、通信でもそのまま Id で送れるようにするため。
    public static class StatusEffectKinds
    {
        public const string Burn = "burn";

        public const string Blind = "blind";
    }
}
