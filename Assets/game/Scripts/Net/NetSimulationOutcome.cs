namespace Fps.Net
{
    /// <summary><c>-net-latency</c> / <c>-net-loss</c> / <c>-net-jitter</c> を transport に効かせたか (<see cref="NetBootstrap.DecideNetSimulation"/>)。</summary>
    public enum NetSimulationOutcome
    {
        /// <summary>どの引数も付いていない (すべて 0)。</summary>
        NotRequested,

        /// <summary>UnityTransport のシミュレーターに入れる。</summary>
        Applied,

        /// <summary>製品ビルドなので無視する。</summary>
        IgnoredInReleaseBuild,

        /// <summary>UnityTransport 以外 (EOS) なので無視する。</summary>
        IgnoredForTransport,
    }
}
