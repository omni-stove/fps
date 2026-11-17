namespace Shooter.Runtime
{
    // 回数・待ち時間・効果が出るまでの秒数は枠 (Shooter.Core.AbilitySlot) が持つので、ここでは持たない。
    public interface IAbility
    {
        bool CanUse(AbilityContext context);

        void Begin(AbilityContext context);

        bool Tick(AbilityContext context, float deltaSeconds);

        void End(AbilityContext context, bool interrupted);
    }
}
