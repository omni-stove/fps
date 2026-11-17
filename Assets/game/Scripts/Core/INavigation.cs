using System.Numerics;

namespace Shooter.Core
{
    // NavMesh を直に呼ばず差し込み口にするのは、Unity 抜きのサーバーでは自前の経路のデータで答えるため。
    public interface INavigation
    {
        bool SamplePosition(Vector3 position, float maxDistance, int areaMask, out Vector3 nearest);

        bool CalculatePath(Vector3 from, Vector3 destination, int areaMask, NavPath path);
    }
}
