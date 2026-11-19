namespace Shooter.Runtime
{
    // IVehicleMotor に入れず別にするのは、上限の無い動き方を足すゲームに、使わない値を書かせないため。書き出しはこれが無ければ既定の上限を使う。
    public interface IVehicleTopSpeed
    {
        float TopSpeed { get; }
    }
}
