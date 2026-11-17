namespace Shooter.Core
{
    /// <summary>クライアント (プレイヤー) をビルドする先。コマンドライン引数 <c>-clientPlatform windows|macos|webgl</c> に対応する。</summary>
    public enum ClientPlatform
    {
        /// <summary>Windows x86_64 (StandaloneWindows64)。</summary>
        Windows,

        /// <summary>macOS (StandaloneOSX)。</summary>
        MacOS,

        /// <summary>ブラウザ (WebGL)。UDP を使えないのでオンライン対戦はできない。</summary>
        WebGL,
    }
}
