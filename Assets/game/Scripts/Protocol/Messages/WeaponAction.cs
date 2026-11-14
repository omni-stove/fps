namespace Fps.Protocol
{
    /// <summary><see cref="WeaponActionRequestMessage"/> で頼む、今の武器への操作。</summary>
    public enum WeaponAction
    {
        Reload,
        /// <summary>リロード中にアクティブリロードの窓を狙って押した。</summary>
        ActiveReload,
    }
}
