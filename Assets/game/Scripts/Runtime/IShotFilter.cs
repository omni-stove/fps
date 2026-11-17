using UnityEngine;

namespace Shooter.Runtime
{
    public interface IShotFilter
    {
        void Filter(ref Vector3 origin, ref Vector3 direction);
    }
}
