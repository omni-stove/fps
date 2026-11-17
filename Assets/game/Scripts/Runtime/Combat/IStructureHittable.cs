using UnityEngine;

namespace Shooter.Runtime
{
    public interface IStructureHittable
    {
        void ApplyStructureHit(Collider collider, Vector3 point, int damage);
    }
}
