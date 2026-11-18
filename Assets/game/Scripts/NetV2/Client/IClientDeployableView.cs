using System.Numerics;

namespace Shooter.NetV2
{
    public interface IClientDeployableView
    {
        void Move(Vector3 position, float yaw);

        void SetHealth(int health);

        void Remove();
    }
}
