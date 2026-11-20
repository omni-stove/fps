using System.Numerics;

namespace Shooter.Core
{
    // Physics を直に呼ばず差し込み口にするのは、Unity 抜きのサーバーでは自前の当たりで答えるため。
    public interface IWorldCollision
    {
        bool Raycast(Vector3 origin, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit);

        bool RaycastCollider(int colliderId, Vector3 origin, Vector3 direction, float maxDistance, out WorldHit hit);

        int OverlapCapsule(Vector3 point0, Vector3 point1, float radius, int layerMask, int[] results);

        bool CapsuleCast(
            Vector3 point0, Vector3 point1, float radius, Vector3 direction, float maxDistance, int layerMask, out WorldHit hit);
    }
}
