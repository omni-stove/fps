namespace Shooter.Protocol
{
    public enum ExtractionOutcome
    {
        // まだ出撃中か、出撃していない
        None,

        Extracted,

        Killed,

        TimedOut,
    }
}
