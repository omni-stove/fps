namespace Shooter.NetV2
{
    // Shooter.NetEos はプラグインがあるビルドでだけコンパイルされるので、起動時にここへ入れてもらい、無いビルドでは null のまま -transport eos を断る。
    public static class NetV2Eos
    {
        public static IEosClientTransportFactory Factory { get; set; }
    }
}
