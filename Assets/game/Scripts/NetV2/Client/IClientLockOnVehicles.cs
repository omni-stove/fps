using System.Numerics;

namespace Shooter.NetV2
{
    // 乗り物の見せている姿とフレアの効き目はシーンの乗り物 (NetV2Vehicles) にしか無いので、ロックオンの相手を集める側にはこの口だけを渡す。
    public interface IClientLockOnVehicles
    {
        // 今見せている姿の車体の中ほど。サーバー (ServerVehicles.TryRewindCenter) が確かめる点と同じ所を指す。見せていない乗り物は false。
        bool TryGetShownCenter(int vehicle, out Vector3 center);

        // 撒いたフレアが弾を誘う角度 (CountermeasureSpec.DecoyConeDegrees)。フレアを積んでいない乗り物は null。
        float? DecoyConeDegrees(int vehicle);
    }
}
