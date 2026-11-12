namespace Fps.Core
{
    /// <summary>専用サーバーをビルドする OS。コマンドライン引数 <c>-serverPlatform macos|linux</c> に対応する。</summary>
    public enum ServerPlatform
    {
        /// <summary>macOS (StandaloneOSX の Dedicated Server)。</summary>
        MacOS,

        /// <summary>Linux x86_64 (StandaloneLinux64 の Dedicated Server)。</summary>
        Linux,
    }
}
