namespace Fps.Core
{
    /// <summary>アクティブリロードの判定の結果。</summary>
    public enum ActiveReloadResult
    {
        /// <summary>判定の幅の中で押した。その場でリロードを終える。</summary>
        Success,

        /// <summary>判定の幅の外で押した。リロードが <see cref="WeaponSpec.ActiveReloadFailSeconds"/> だけ遅れる。</summary>
        Fail,
    }
}
