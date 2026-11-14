namespace Fps.Core
{
    /// <summary>
    /// プレイヤーの姿勢。後ろほど低い。寸法は <see cref="StanceGeometry.Shape(PlayerStance)"/> で引く。
    /// </summary>
    public enum PlayerStance
    {
        /// <summary>立っている。</summary>
        Standing,

        /// <summary>しゃがんでいる。</summary>
        Crouching,

        /// <summary>伏せている。</summary>
        Prone,
    }
}
