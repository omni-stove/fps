namespace Shooter.Runtime
{
    public interface IAbilityFollowUp
    {
        bool TryFollowUp(AbilityContext context);

        // HUD の枠の行に添える案内は、文を持たせずに UI がこれから作る。
        bool TryGetFollowUp(AbilityContext context, out AbilityFollowUp followUp);
    }
}
