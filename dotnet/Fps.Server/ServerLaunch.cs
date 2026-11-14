using System;
using System.Collections.Generic;
using Fps.Core;

namespace Fps.Server
{
    /// <summary>
    /// 待ち受けるアドレスとポートを環境変数からも受け取るのは、Docker のイメージの入り口 (<c>docker/server/entrypoint.sh</c>) と
    /// 同じ意味にし、引数と両方で渡したら同じく 2 回指定したとして断るため。
    /// </summary>
    internal sealed class ServerLaunch
    {
        public const string AddressVariable = "FPS_SERVER_ADDRESS";
        public const string PortVariable = "FPS_SERVER_PORT";

        public const string UnknownVersion = "unknown";

        /// <summary>Unity のサーバーも <c>-map</c> が無ければこのシーンのまま始めるため。</summary>
        public const string DefaultMap = MenuRoute.NetSceneName;

        public const string RulesFlag = "-rules";

        /// <summary>Unity の NetMatch.prefab がこのルールに配線されているため。</summary>
        public const string DefaultRules = "TeamDeathmatch";

        private ServerLaunch(NetLaunchOptions options, string version, int drainTimeoutSeconds, string rules)
        {
            Options = options;
            Version = version;
            DrainTimeoutSeconds = drainTimeoutSeconds;
            Rules = rules ?? DefaultRules;
        }

        public NetLaunchOptions Options { get; }

        public string Version { get; }

        public string ServerVersion => Version ?? UnknownVersion;

        public int DrainTimeoutSeconds { get; }

        public string Map => Options.Map ?? DefaultMap;

        public string Rules { get; }

        public static bool TryParse(
            IReadOnlyList<string> args, Func<string, string> environment, out ServerLaunch launch, out string error)
        {
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(environment);

            launch = null;
            // 待ち受けのアドレスを足す前に断るのは、足した 0.0.0.0 をクライアントの接続先として咎める文より、モードの誤りを先に伝えるため
            if (NetLaunchOptions.TryParse(args, NetLaunchMode.Server, out var given, out _) && given.Mode != NetLaunchMode.Server)
            {
                error = ModeError(given.Mode);
                return false;
            }

            var effective = new List<string>(args);
            if (!TryAddFromEnvironment(effective, args, environment, NetLaunchOptions.AddressFlag, AddressVariable, NetLaunchOptions.AnyAddress, out error)
                || !TryAddFromEnvironment(effective, args, environment, NetLaunchOptions.PortFlag, PortVariable, null, out error))
            {
                return false;
            }

            if (!NetLaunchOptions.TryParse(effective, NetLaunchMode.Server, out var options, out error))
            {
                return false;
            }

            if (options.Mode != NetLaunchMode.Server)
            {
                error = ModeError(options.Mode);
                return false;
            }

            error = Unsupported(options);
            if (error != null)
            {
                return false;
            }

            if (!BuildVersion.TryParseArgs(args, out var version, out error))
            {
                return false;
            }

            if (!TryParseRules(args, out var rules, out error))
            {
                return false;
            }

            launch = new ServerLaunch(options, version, ServerDrain.ResolveTimeoutSeconds(args, options.DrainTimeoutSeconds), rules);
            return true;
        }

        // ルールの名前はファイル名になるので、マップと同じくパスに使えない文字を断る
        private static bool TryParseRules(IReadOnlyList<string> args, out string rules, out string error)
        {
            rules = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], RulesFlag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1][0] == '-')
                {
                    error = RulesFlag + " requires a value";
                    return false;
                }

                if (rules != null)
                {
                    error = RulesFlag + " is given more than once";
                    return false;
                }

                rules = args[++i];
                if (!NetLaunchOptions.IsMapName(rules))
                {
                    error = RulesFlag + " must be a rules name of letters, digits, '_' or '-' (got '" + rules + "')";
                    return false;
                }
            }

            error = null;
            return true;
        }

        private static string ModeError(NetLaunchMode mode)
        {
            return "this is a dedicated server; " + NetLaunchOptions.ModeFlag + " must be "
                + NetLaunchOptions.ModeName(NetLaunchMode.Server) + " (got " + NetLaunchOptions.ModeName(mode) + ")";
        }

        private static bool TryAddFromEnvironment(
            List<string> effective,
            IReadOnlyList<string> args,
            Func<string, string> environment,
            string flag,
            string variable,
            string fallback,
            out string error)
        {
            error = null;
            var value = environment(variable);
            var given = HasFlag(args, flag);
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (given)
                {
                    error = flag + " is given more than once (" + variable + " is set too); pass it in only one of them";
                    return false;
                }

                effective.Add(flag);
                effective.Add(value.Trim());
            }
            else if (!given && fallback != null)
            {
                effective.Add(flag);
                effective.Add(fallback);
            }

            return true;
        }

        private static bool HasFlag(IReadOnlyList<string> args, string flag)
        {
            foreach (var arg in args)
            {
                if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // 黙って無視すると、ロビーや Bot が動いていると運用する人が思い込むので、移すまでは起動させない
        private static string Unsupported(NetLaunchOptions options)
        {
            if (options.Transport != NetTransportKind.Udp)
            {
                return NetLaunchOptions.TransportFlag + " " + NetLaunchOptions.TransportName(options.Transport)
                    + " is not supported by the .NET server yet; use " + NetLaunchOptions.TransportName(NetTransportKind.Udp);
            }

            if (options.Bots > 0)
            {
                return NetLaunchOptions.BotsFlag + " is not supported by the .NET server yet (it has no bot module)";
            }

            if (options.SimulatesNetwork)
            {
                return NetLaunchOptions.NetLatencyFlag + " / " + NetLaunchOptions.NetLossFlag + " / " + NetLaunchOptions.NetJitterFlag
                    + " are not supported by the .NET server yet";
            }

            return null;
        }
    }
}
