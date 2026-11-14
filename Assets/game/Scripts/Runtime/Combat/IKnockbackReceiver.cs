using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 速度を求めるのを受け手でなく Knockback に任せるのは、倒れた後も同じ Spec と重さでラグドールへ同じ力を渡すため。
    public interface IKnockbackReceiver
    {
        KnockbackSpec KnockbackSpec { get; }

        float KnockbackWeight { get; }

        bool IsKnockedBack { get; }

        bool Launch(Vector3 velocity, DamageKind kind);
    }
}
