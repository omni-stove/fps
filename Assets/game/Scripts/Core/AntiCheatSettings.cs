using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// Easy Anti-Cheat (EAC) を使うかどうかの決定。コマンドライン引数・エディタかどうか・ネットワークの起動設定から決める。
    /// EAC はホスト型の P2P (Anti-Cheat Client インターフェースの PeerToPeer モード) でだけ使い、
    /// ピアを EOS の ProductUserId で登録するので EOS の transport が要る。
    /// </summary>
    public sealed class AntiCheatSettings
    {
        /// <summary>EAC を無効にする。開発ビルドや、ブートストラッパーを通さずに起動するときに使う。</summary>
        public const string NoEacFlag = "-noeac";

        /// <summary>エディタでも EAC を有効にする (エディタでは既定で無効)。</summary>
        public const string EacFlag = "-eac";

        /// <summary>ログの接頭辞。</summary>
        public const string LogTag = "[AntiCheat]";

        private AntiCheatSettings(bool enabled, string reason)
        {
            Enabled = enabled;
            Reason = reason;
        }

        /// <summary>EAC のセッションを始め、ピアを登録するか。</summary>
        public bool Enabled { get; }

        /// <summary>有効・無効になった理由。ログ向け。</summary>
        public string Reason { get; }

        /// <summary>理由を添えた無効の設定。</summary>
        public static AntiCheatSettings Disabled(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("must not be null or blank", nameof(reason));
            }

            return new AntiCheatSettings(false, reason);
        }

        /// <summary>
        /// EAC を使うかを決める。上から順に、<see cref="NoEacFlag"/> があれば無効、UDP の transport なら無効、
        /// 専用サーバー (<c>-mode server</c>) なら無効、エディタで <see cref="EacFlag"/> が無ければ無効、それ以外は有効。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <param name="isEditor">エディタで動いているか。</param>
        /// <param name="options">実際に使うネットワークの設定 (EOS から UDP に切り替えた後のもの)。</param>
        /// <param name="settings">成功したときの設定。失敗したときは null。</param>
        /// <param name="error">失敗したときの理由。成功したときは null。</param>
        /// <returns><see cref="NoEacFlag"/> と <see cref="EacFlag"/> が両方ある場合だけ false。</returns>
        public static bool TryResolve(
            IReadOnlyList<string> args,
            bool isEditor,
            NetLaunchOptions options,
            out AntiCheatSettings settings,
            out string error)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var noEac = HasFlag(args, NoEacFlag);
            var forceEac = HasFlag(args, EacFlag);
            if (noEac && forceEac)
            {
                settings = null;
                error = NoEacFlag + " and " + EacFlag + " cannot be given together";
                return false;
            }

            error = null;
            if (noEac)
            {
                settings = Disabled("disabled by " + NoEacFlag);
            }
            else if (options.Transport != NetTransportKind.Eos)
            {
                settings = Disabled("disabled: peers are registered by EOS product user id, which needs "
                    + NetLaunchOptions.TransportFlag + " " + NetLaunchOptions.TransportName(NetTransportKind.Eos));
            }
            else if (options.Mode == NetLaunchMode.Server)
            {
                settings = Disabled("disabled: a dedicated server (" + NetLaunchOptions.ModeFlag + " "
                    + NetLaunchOptions.ModeName(NetLaunchMode.Server) + ") is not a protected peer; host with "
                    + NetLaunchOptions.ModeFlag + " " + NetLaunchOptions.ModeName(NetLaunchMode.Host)
                    + " to use anti-cheat");
            }
            else if (isEditor && !forceEac)
            {
                settings = Disabled("disabled in the editor by default; pass " + EacFlag + " to enable");
            }
            else
            {
                settings = new AntiCheatSettings(true, "enabled (peer-to-peer, " + NetLaunchOptions.ModeName(options.Mode) + ")");
            }

            return true;
        }

        /// <summary><c>[AntiCheat] enabled ...</c> / <c>[AntiCheat] disabled ...</c> の形のログ行。</summary>
        public string Describe()
        {
            return LogTag + " " + Reason;
        }

        public override string ToString()
        {
            return Reason;
        }

        private static bool HasFlag(IReadOnlyList<string> args, string flag)
        {
            for (var i = 0; i < args.Count; i++)
            {
                if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
