using System;

namespace Fps.Core
{
    /// <summary><see cref="PlayerActionState"/> の、その瞬間に立っている状態の組み合わせ。</summary>
    [Flags]
    public enum PlayerActionConditions
    {
        None = 0,

        /// <summary>ダッシュ中。</summary>
        Sprinting = 1 << 0,

        /// <summary>しゃがんでいる (頭上が塞がって立てないときも含む、実際の姿勢)。</summary>
        Crouching = 1 << 1,

        /// <summary>回避の最中。</summary>
        Dodging = 1 << 2,

        /// <summary>乗り越えの最中。</summary>
        Vaulting = 1 << 3,

        /// <summary>接地している。</summary>
        Grounded = 1 << 4,

        /// <summary>遮蔽物に張り付いている (寄せている途中を含む)。</summary>
        InCover = 1 << 5,

        /// <summary>今撃てばブラインドファイアになる (カバー中で覗いていない)。</summary>
        BlindFiring = 1 << 6,

        /// <summary>覗き込んでいる (ADS)。</summary>
        Aiming = 1 << 7,

        /// <summary>撃っている (最後の 1 発から <see cref="PlayerActionState.IsFiringAfter"/> の間)。</summary>
        Firing = 1 << 8,

        /// <summary>リロード中。</summary>
        Reloading = 1 << 9,

        /// <summary>ダウンしている。</summary>
        Downed = 1 << 10,
    }
}
