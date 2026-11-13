using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> のうち、乗り越えの処理。
    /// </summary>
    public sealed partial class PlayerController
    {
        /// <summary>乗り越えの最中か。乗り越えの動きを入れるまでは常に false。</summary>
        public bool IsVaulting { get; private set; }

        /// <summary>
        /// 乗り越えの最中なら、この <see cref="Tick"/> で動かす変位を displacement に入れ、overridden を true にする。
        /// 回避より先に呼ばれる (<see cref="TryOverrideMove"/>)。
        /// </summary>
        partial void TryOverrideMoveForVault(
            Vector2 move, bool jumpPressed, float deltaSeconds, ref bool overridden, ref Vector3 displacement);
    }
}
