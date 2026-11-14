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

        /// <summary>近接攻撃の最中。</summary>
        Meleeing = 1 << 11,

        /// <summary>被弾して怯んでいる。</summary>
        Staggered = 1 << 12,

        /// <summary>スライディングの最中。</summary>
        Sliding = 1 << 13,

        /// <summary>段や崖の縁によじ登っている最中。</summary>
        Mantling = 1 << 14,

        /// <summary>伏せている。</summary>
        Prone = 1 << 15,

        /// <summary>はしごに掴まっている。</summary>
        OnLadder = 1 << 16,

        /// <summary>ジップラインに掴まって移っている。</summary>
        Ziplining = 1 << 17,

        /// <summary>パラシュートを開いて降りている。</summary>
        Parachuting = 1 << 18,

        /// <summary>乗り物に乗っている。</summary>
        InVehicle = 1 << 19,

        /// <summary>能力による移動 (引き寄せなど) の最中。</summary>
        AbilityMoving = 1 << 20,
    }
}
