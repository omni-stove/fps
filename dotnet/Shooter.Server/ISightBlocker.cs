using System.Numerics;

namespace Shooter.Server
{
    internal interface ISightBlocker
    {
        bool Blocks(Vector3 from, Vector3 to);
    }
}
