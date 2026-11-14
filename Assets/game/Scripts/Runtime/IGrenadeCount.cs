namespace Fps.Runtime
{
    /// <summary>残りのグレネードの数の表示。ルールでグレネードを使わない間は隠す。</summary>
    public interface IGrenadeCount
    {
        /// <summary>thrower の残りを出し始める。前に出していたものは手放す。null なら出すのをやめる。</summary>
        void Show(GrenadeThrower thrower);
    }
}
