using System.Numerics;

namespace Fps.Server
{
    /// <summary>世界の当たりのほかに視線を遮る物 (煙など)。</summary>
    internal interface ISightBlocker
    {
        /// <summary>from から to への視線を遮るなら true。</summary>
        bool Blocks(Vector3 from, Vector3 to);
    }
}
