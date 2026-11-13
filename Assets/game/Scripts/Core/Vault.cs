namespace Fps.Core
{
    /// <summary>
    /// 低い障害物を乗り越えられるか。
    /// </summary>
    public enum Vault
    {
        /// <summary>乗り越えられない。</summary>
        Off,

        /// <summary><see cref="MotorSpec.VaultMaxHeight"/> までの障害物を乗り越えられる。</summary>
        On,
    }
}
