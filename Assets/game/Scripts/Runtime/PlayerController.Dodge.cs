using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> のうち、回避の処理。
    /// </summary>
    public sealed partial class PlayerController
    {
        /// <summary>回避の最中か。回避の動きを入れるまでは常に false。</summary>
        public bool IsDodging { get; private set; }

        /// <summary>
        /// 回避の最中なら、この <see cref="Tick"/> で動かす変位を displacement に入れ、overridden を true にする。
        /// 呼ばれるのは乗り越えが移動を差し替えなかったときだけ (<see cref="TryOverrideMove"/>)。
        /// </summary>
        partial void TryOverrideMoveForDodge(
            Vector2 move, bool jumpPressed, float deltaSeconds, ref bool overridden, ref Vector3 displacement);
    }
}
