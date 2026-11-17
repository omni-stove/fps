namespace Shooter.Core
{
    /// <summary>
    /// 起動したプロセスが画面を持つかの判断をまとめる。Unity の Dedicated Server ターゲット (<c>UNITY_SERVER</c>) でビルドされたときは、
    /// 描画・音声のコンポーネントを止める。
    /// </summary>
    public static class NetStartup
    {
        /// <summary>
        /// このアセンブリが専用サーバー向けにコンパイルされたか。Unity が Dedicated Server ターゲットで
        /// <c>UNITY_SERVER</c> を定義したときだけ true。dotnet でのビルドでは常に false。
        /// </summary>
        public static bool IsDedicatedServerBuild
        {
            get
            {
#if UNITY_SERVER
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// 画面を持たずに動くプレイヤーか (batchmode か専用サーバー)。
        /// 失敗時に終了コード付きで終了し、フレームレートを抑える対象になる。エディタは含めない。
        /// </summary>
        public static bool IsHeadlessPlayer(bool isEditor, bool isBatchMode, bool isDedicatedServer)
        {
            return !isEditor && (isBatchMode || isDedicatedServer);
        }

        /// <summary>描画・音声のコンポーネントを止めるべきか。専用サーバーでだけ止める。</summary>
        public static bool ShouldDisablePresentation(bool isDedicatedServer)
        {
            return isDedicatedServer;
        }
    }
}
