namespace Fps.Core
{
    public enum ChargerPhase
    {
        Ready,
        WindingUp,
        Charging,
        Stunned,
        Recovering,
    }

    public enum ChargeEnd
    {
        None,
        Hit,
        Obstacle,
        Edge,
        Spent,
        Cancelled,
    }
}
