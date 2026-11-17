using System.Numerics;

namespace Shooter.NetV2
{
    public interface IClientProjectileView
    {
        void Move(Vector3 position, Vector3 velocity);

        void Stick(Vector3 position);

        void Remove();
    }
}
