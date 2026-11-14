namespace Fps.NetV2
{
    /// <summary>
    /// v2 のクライアントの EOS の通信路の作り方。Fps.NetEos (プラグインと Netcode があるビルドでだけコンパイルされる) が起動時に入れ、
    /// 無いビルドでは null のまま <c>-transport eos</c> を断る。
    /// </summary>
    public static class NetV2Eos
    {
        public static IEosClientTransportFactory Factory { get; set; }
    }
}
