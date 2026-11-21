namespace Shooter.Core
{
    // Core はプロトコルを参照しないので、送るときの DeployableKind とは別に、データの側の種類をここに持つ。
    public enum DeployRole
    {
        Turret,
        Shield,
        Decoy,

        Beacon,

        Drone,

        Mine,

        RemoteCharge,
    }
}
