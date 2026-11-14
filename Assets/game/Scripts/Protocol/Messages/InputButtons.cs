using System;

namespace Fps.Protocol
{
    /// <summary><see cref="InputFrame"/> で押されているボタン。</summary>
    [Flags]
    public enum InputButtons : byte
    {
        None = 0,
        Sprint = 1,
        /// <summary>この tick にジャンプが押された。</summary>
        Jump = 2,
        Crouch = 4,
        /// <summary>この tick に Cover が押された (カバーに入る・出る)。</summary>
        Cover = 8,
        /// <summary>覗き込んでいる。他の人の構えとサーバーの弾のばらつきにも使う。</summary>
        Aim = 16,
        /// <summary>この tick に Dodge が押された。</summary>
        Dodge = 32,
        All = Sprint | Jump | Crouch | Cover | Aim | Dodge,
    }
}
