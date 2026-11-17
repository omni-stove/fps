using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // EAC は相手を EOS の ProductUserId で登録するので、EOS の transport でなければ使わない。
    public sealed class AntiCheatSettings
    {
        public const string NoEacFlag = "-noeac";

        public const string EacFlag = "-eac";

        public const string LogTag = "[AntiCheat]";

        // サーバーとクライアントのどちらが先に切っても同じ文を出すため、1 つにする。
        public const string RejectedDetail = "anti-cheat";

        private AntiCheatSettings(bool enabled, string reason)
        {
            Enabled = enabled;
            Reason = reason;
        }

        public bool Enabled { get; }

        public string Reason { get; }

        public static AntiCheatSettings Disabled(string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("must not be null or blank", nameof(reason));
            }

            return new AntiCheatSettings(false, reason);
        }

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
                    + NetLaunchOptions.ModeName(NetLaunchMode.Server) + ") is not a protected peer; the .NET server decides with "
                    + nameof(TryResolveDedicatedServer));
            }
            else if (isEditor && !forceEac)
            {
                settings = Disabled("disabled in the editor by default; pass " + EacFlag + " to enable");
            }
            else
            {
                settings = new AntiCheatSettings(true, "enabled (client-server, " + NetLaunchOptions.ModeName(options.Mode) + ")");
            }

            return true;
        }

        // 専用サーバーで既定で無効にするのは、EAC を通さないクライアント (NoEacFlag) が専用サーバーにつなげる今の運用を、
        // ポータルでクライアントとサーバーの保護を用意するまで変えないため。
        public static bool TryResolveDedicatedServer(
            IReadOnlyList<string> args,
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

            settings = null;
            var noEac = HasFlag(args, NoEacFlag);
            var eac = HasFlag(args, EacFlag);
            if (noEac && eac)
            {
                error = NoEacFlag + " and " + EacFlag + " cannot be given together";
                return false;
            }

            if (!eac)
            {
                error = null;
                settings = Disabled(noEac
                    ? "disabled by " + NoEacFlag
                    : "disabled on the dedicated server by default; pass " + EacFlag + " with " + NetLaunchOptions.TransportFlag + " "
                        + NetLaunchOptions.TransportName(NetTransportKind.Eos) + " to protect the match");
                return true;
            }

            if (options.Transport != NetTransportKind.Eos)
            {
                error = EacFlag + " needs " + NetLaunchOptions.TransportFlag + " " + NetLaunchOptions.TransportName(NetTransportKind.Eos)
                    + ": clients are registered by EOS product user id";
                return false;
            }

            error = null;
            settings = new AntiCheatSettings(true, "enabled (client-server, dedicated server)");
            return true;
        }

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
