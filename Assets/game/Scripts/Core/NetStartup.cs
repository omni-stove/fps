using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 起動時のネットワークの判断をまとめる。専用サーバービルド (<c>UNITY_SERVER</c>) では
    /// 引数が無くてもサーバーとして起動し、サーバー以外のモードは受け付けない。
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

        /// <summary><c>-mode</c> が無いときの起動モード。専用サーバーでは設定に関わらず Server。</summary>
        public static NetLaunchMode DefaultMode(bool isDedicatedServer, NetLaunchMode configured)
        {
            return isDedicatedServer ? NetLaunchMode.Server : configured;
        }

        /// <summary>
        /// コマンドライン引数から起動設定を決める。<see cref="NetLaunchOptions.TryParse"/> に
        /// <see cref="DefaultMode"/> を渡し、専用サーバーでサーバー以外のモードが指定されたら失敗にする。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <param name="isDedicatedServer">専用サーバービルドか。</param>
        /// <param name="configuredDefault">シーンに設定された既定のモード。</param>
        /// <param name="options">成功したときの設定。失敗したときは null。</param>
        /// <param name="error">失敗したときの理由。成功したときは null。</param>
        public static bool TryResolve(
            IReadOnlyList<string> args,
            bool isDedicatedServer,
            NetLaunchMode configuredDefault,
            out NetLaunchOptions options,
            out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (!NetLaunchOptions.TryParse(args, DefaultMode(isDedicatedServer, configuredDefault), out options, out error))
            {
                return false;
            }

            if (isDedicatedServer && options.Mode != NetLaunchMode.Server)
            {
                error = "a dedicated server build can only run " + NetLaunchOptions.ModeFlag + " "
                    + NetLaunchOptions.ModeName(NetLaunchMode.Server) + " (got "
                    + NetLaunchOptions.ModeName(options.Mode) + "); use the normal player build for "
                    + NetLaunchOptions.ModeName(options.Mode);
                options = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// コマンドライン引数からロビーの設定を決める。<see cref="LobbyLaunchOptions.TryParse"/> の結果のうち、
        /// 専用サーバーでのロビーは Host になれないので失敗にする。<c>-lobby</c> が無ければ <see cref="LobbyLaunchOptions.None"/>。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <param name="isDedicatedServer">専用サーバービルドか。</param>
        /// <param name="options">成功したときの設定。失敗したときは null。</param>
        /// <param name="error">失敗したときの理由。成功したときは null。</param>
        public static bool TryResolveLobby(
            IReadOnlyList<string> args,
            bool isDedicatedServer,
            out LobbyLaunchOptions options,
            out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (!LobbyLaunchOptions.TryParse(args, out options, out error))
            {
                return false;
            }

            if (isDedicatedServer && options.IsRequested)
            {
                error = options.Command == LobbyCommand.QuickMatch
                    ? LobbyLaunchOptions.LobbyFlag + " quickmatch needs the normal player build: it connects as a client;"
                        + " use " + NetLaunchOptions.RegisterLobbyFlag + " to list a dedicated server for quick match"
                    : LobbyLaunchOptions.LobbyFlag + " needs the normal player build: the lobby owner runs as a host,"
                        + " which a dedicated server build cannot do";
                options = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 起動引数から決めたロビーの設定 fromArgs と、メニューから渡されたロビーの設定 fromMenu のどちらを使うかを決める。
        /// 起動引数に <c>-lobby</c> か <c>-mode</c> があれば起動引数を優先し、メニューの設定は捨てる (引数で起動したときはメニューを通らない)。
        /// 専用サーバーはロビーの Host になれないので、メニューの設定は使わない。
        /// </summary>
        /// <param name="fromArgs"><see cref="TryResolveLobby"/> で決めた設定。</param>
        /// <param name="args">コマンドライン引数。</param>
        /// <param name="isDedicatedServer">専用サーバービルドか。</param>
        /// <param name="fromMenu">メニューが渡した設定。無ければ null か <see cref="LobbyLaunchOptions.None"/>。</param>
        public static LobbyLaunchOptions ChooseLobby(
            LobbyLaunchOptions fromArgs,
            IReadOnlyList<string> args,
            bool isDedicatedServer,
            LobbyLaunchOptions fromMenu)
        {
            if (fromArgs == null)
            {
                throw new ArgumentNullException(nameof(fromArgs));
            }

            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (fromArgs.IsRequested || NetLaunchOptions.HasModeFlag(args) || isDedicatedServer
                || fromMenu == null || !fromMenu.IsRequested)
            {
                return fromArgs;
            }

            return fromMenu;
        }

        /// <summary>
        /// 起動直後にネットワークのシーンへ切り替えるか。エディタでは切り替えない。
        /// プレイヤーでは専用サーバーか、<c>-mode</c> か <c>-lobby</c> が指定されたときに切り替える。
        /// </summary>
        public static bool ShouldLoadNetScene(bool isEditor, bool isDedicatedServer, IReadOnlyList<string> args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (isEditor)
            {
                return false;
            }

            return isDedicatedServer || NetLaunchOptions.HasModeFlag(args) || LobbyLaunchOptions.HasLobbyFlag(args);
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

        /// <summary>
        /// ネットワークのプレイヤーに一人称の視点 (カメラ・音声・視点操作) を付けるか。
        /// 自分が操作するプレイヤー (オーナー) だけで、画面の無い batchmode と専用サーバーでは付けない。
        /// batchmode はエディタからのテスト実行も含めて画面が無いので、エディタかどうかは問わない。
        /// </summary>
        public static bool ShouldEnableFirstPersonView(bool isOwner, bool isBatchMode, bool isDedicatedServer)
        {
            return isOwner && !isBatchMode && !ShouldDisablePresentation(isDedicatedServer);
        }

        /// <summary>
        /// ネットワークのプレイヤーの生成時に HUD を作るか。自分が操作するプレイヤー (オーナー) だけで、
        /// 画面の無い batchmode と専用サーバーでは作らない。
        /// </summary>
        public static bool ShouldCreateHud(bool isOwner, bool isBatchMode, bool isDedicatedServer)
        {
            return isOwner && !isBatchMode && !ShouldDisablePresentation(isDedicatedServer);
        }
    }
}
