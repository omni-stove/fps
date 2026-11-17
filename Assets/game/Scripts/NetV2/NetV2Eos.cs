namespace Shooter.NetV2
{
    /// <summary>
    /// クライアントの EOS の通信路とロビーの作り方。Shooter.NetEos (プラグインがあるビルドでだけコンパイルされる) が起動時に入れ、
    /// 無いビルドでは null のまま <c>-transport eos</c> を断る。
    /// </summary>
    public static class NetV2Eos
    {
        public static IEosClientTransportFactory Factory { get; set; }
    }
}
