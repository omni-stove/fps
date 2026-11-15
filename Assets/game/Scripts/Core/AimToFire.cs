namespace Fps.Core
{
    /// <summary>
    /// 覗き込んで構えている間しか撃てないか (腰撃ちを禁じるか)。
    /// </summary>
    public enum AimToFire
    {
        /// <summary>構えなくても撃てる。</summary>
        Off,

        /// <summary>構えている間だけ撃てる。ブラインドファイアは構えずに撃てる。</summary>
        On,
    }
}
