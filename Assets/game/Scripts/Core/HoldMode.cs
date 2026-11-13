namespace Fps.Core
{
    /// <summary>
    /// ボタンで有効・無効を切り替える操作 (<see cref="HoldAction"/>) の入力の仕方。
    /// </summary>
    public enum HoldMode
    {
        /// <summary>押している間だけ有効。</summary>
        Hold,

        /// <summary>押すたびに有効と無効を切り替える。</summary>
        Toggle,
    }
}
