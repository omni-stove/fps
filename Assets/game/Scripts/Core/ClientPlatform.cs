namespace Fps.Core
{
    /// <summary>クライアント (プレイヤー) をビルドする OS。コマンドライン引数 <c>-clientPlatform windows|macos</c> に対応する。</summary>
    public enum ClientPlatform
    {
        /// <summary>Windows x86_64 (StandaloneWindows64)。</summary>
        Windows,

        /// <summary>macOS (StandaloneOSX)。</summary>
        MacOS,
    }
}
