using UnityEngine;

namespace Shooter.Runtime
{
    // コライダーで遮らないのは、煙のように弾は通すが視線だけ遮る物を置けるようにするため。
    public interface IVisionBlocker
    {
        bool Blocks(Vector3 eye, Vector3 target);
    }
}
