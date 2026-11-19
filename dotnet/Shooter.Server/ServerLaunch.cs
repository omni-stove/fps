using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    // 待ち受けるアドレスとポートを環境変数からも受け取るのは、Docker のイメージの入り口 (docker/server/entrypoint.sh) と
    // 同じ意味にし、引数と両方で渡したら同じく 2 回指定したとして断るため。
    internal sealed class ServerLaunch
    {
        public const string AddressVariable = "SHOOTER_SERVER_ADDRESS";
        public const string PortVariable = "SHOOTER_SERVER_PORT";

        public const string UnknownVersion = "unknown";

        // Unity のサーバーも -map が無ければこのシーンのまま始めるため。
        public const string DefaultMap = MenuRoute.NetSceneName;

        public const string RulesFlag = "-rules";

        // Unity の NetMatch.prefab がこのルールに配線されているため。
        public const string DefaultRules = "TeamDeathmatch";

        // 引数のほかのフラグは - が 1 つだが、Issue #1004 で決めた名前に合わせる。
        public const string PrivateFlag = "--private";

        public const string JoinCodeFlag = "--join-code";

        public const string BanFileFlag = "--ban-file";

        public const string RegionFlag = "--region";

        public const string ReportFileFlag = "--report-file";

        public const string StatsFlag = "--stats";

        public const string TelemetryFlag = "--telemetry";

        public const string TelemetryDirectoryFlag = "--telemetry-dir";

        private ServerLaunch(
            NetLaunchOptions options,
            string version,
            int drainTimeoutSeconds,
            string rules,
            AntiCheatSettings antiCheat,
            bool privateMatch,
            string joinCode,
            string banFile,
            string region,
            ServerRecordsLaunch records)
        {
            BanFile = banFile;
            Region = region;
            Records = records ?? ServerRecordsLaunch.Off;
            PrivateMatch = privateMatch;
            JoinCode = joinCode;
            AntiCheat = antiCheat;
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

        // -map が無いとき候補 (ルールの mapPool) の先頭を選ぶのは、候補に無い既定のシーンで始めると、次のマップを替えた後に二度と戻れないマップで 1 試合だけ遊ぶことになるため。
        public string StartMap(IReadOnlyList<string> mapPool)
        {
            ArgumentNullException.ThrowIfNull(mapPool);
            return Options.Map ?? (mapPool.Count > 0 ? mapPool[0] : DefaultMap);
        }

        public string Rules { get; }

        public AntiCheatSettings AntiCheat { get; }

        public bool PrivateMatch { get; }

        public string JoinCode { get; }

        // null なら BAN は起動している間だけ覚え、ファイルには残さない。
        public string BanFile { get; }

        public string Region { get; }

        // 通報・統計・テレメトリの残し方。どれも運用する人が起動で頼んだときだけ使う。
        public ServerRecordsLaunch Records { get; }

        public ServerLaunch WithOptions(NetLaunchOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);
            return new ServerLaunch(options, Version, DrainTimeoutSeconds, Rules, AntiCheat, PrivateMatch, JoinCode, BanFile, Region, Records);
        }

        // 起動のオプションをルールの JSON より優先する。ルールの値を消さずに重ねるのは、JSON だけで非公開にする運用も残すため。
        public NetMatchSettings ApplyPrivacy(NetMatchSettings rules, Func<int, int> nextIndex, out bool generated)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(nextIndex);

            generated = false;
            if (!PrivateMatch && !rules.PrivateMatch)
            {
                return rules;
            }

            var code = JoinCode;
            if (code == null && Shooter.Core.JoinCode.TryNormalize(rules.JoinCode, out var fromRules))
            {
                code = fromRules;
            }

            // コードの無い非公開のサーバーには誰も入れないので、決め忘れても作って入れるようにする
            if (code == null)
            {
                code = Shooter.Core.JoinCode.Generate(Shooter.Core.JoinCode.DefaultLength, nextIndex);
                generated = true;
            }

            return rules.WithPrivacy(true, code);
        }

        public static string PrivacyLogLine(NetMatchSettings settings, bool generated)
        {
            ArgumentNullException.ThrowIfNull(settings);
            if (!settings.PrivateMatch)
            {
                return ServerProgram.LogTag + " public match";
            }

            return ServerProgram.LogTag + " private match join_code=" + settings.JoinCode
                + (generated ? " (generated; pass " + JoinCodeFlag + " to choose one)" : "");
        }

        public static bool TryParse(
            IReadOnlyList<string> args, Func<string, string> environment, out ServerLaunch launch, out string error)
        {
            return TryParse(args, environment, null, out launch, out error);
        }

        // builtVersion (ビルドに焼き込んだ版、ServerBuildVersion) は -buildVersion が無いときに使う。
        // イメージの版を入り口の引数で渡すと、docker run で -buildVersion を足した人が「2 回指定」で起動できなくなるため。
        public static bool TryParse(
            IReadOnlyList<string> args, Func<string, string> environment, string builtVersion, out ServerLaunch launch, out string error)
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

            if (version == null && !string.IsNullOrWhiteSpace(builtVersion) && !BuildVersion.TryNormalize(builtVersion, out version, out error))
            {
                error = "the version built into the server: " + error;
                return false;
            }

            if (!TryParseRules(args, out var rules, out error))
            {
                return false;
            }

            if (!TryParsePrivacy(args, out var privateMatch, out var joinCode, out error))
            {
                return false;
            }

            if (!TryParseBanFile(args, out var banFile, out error))
            {
                return false;
            }

            if (!TryParseRegion(args, options, out var region, out error))
            {
                return false;
            }

            if (!ServerRecordsLaunch.TryParse(args, out var records, out error))
            {
                return false;
            }

            if (!AntiCheatSettings.TryResolveDedicatedServer(args, options, out var antiCheat, out error))
            {
                return false;
            }

            launch = new ServerLaunch(
                options,
                version,
                ServerDrain.ResolveTimeoutSeconds(args, options.DrainTimeoutSeconds),
                rules,
                antiCheat,
                privateMatch,
                joinCode,
                banFile,
                region,
                records);
            return true;
        }

        // コードだけ渡されたときも非公開にするのは、公開のサーバーはコードを確かめず、渡しても効かないため
        private static bool TryParsePrivacy(IReadOnlyList<string> args, out bool privateMatch, out string joinCode, out string error)
        {
            privateMatch = false;
            joinCode = null;
            var privateGiven = false;
            for (var i = 0; i < args.Count; i++)
            {
                if (string.Equals(args[i], PrivateFlag, StringComparison.OrdinalIgnoreCase))
                {
                    if (privateGiven)
                    {
                        error = PrivateFlag + " is given more than once";
                        return false;
                    }

                    privateGiven = true;
                    continue;
                }

                if (!string.Equals(args[i], JoinCodeFlag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1][0] == '-')
                {
                    error = JoinCodeFlag + " requires a value";
                    return false;
                }

                if (joinCode != null)
                {
                    error = JoinCodeFlag + " is given more than once";
                    return false;
                }

                var text = args[++i];
                if (!Shooter.Core.JoinCode.TryNormalize(text, out joinCode))
                {
                    error = JoinCodeFlag + " must be " + Shooter.Core.JoinCode.MinLength + "-" + Shooter.Core.JoinCode.MaxLength
                        + " characters of letters, digits, '_' or '-' (got '" + text + "')";
                    return false;
                }
            }

            privateMatch = privateGiven || joinCode != null;
            error = null;
            return true;
        }

        // 値の先頭の - を断るのは、パスを書き忘れたときに次のフラグをファイル名として読み、BAN を思わぬ場所へ書き出さないため
        private static bool TryParseBanFile(IReadOnlyList<string> args, out string banFile, out string error)
        {
            banFile = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], BanFileFlag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1][0] == '-')
                {
                    error = BanFileFlag + " requires a path";
                    return false;
                }

                if (banFile != null)
                {
                    error = BanFileFlag + " is given more than once";
                    return false;
                }

                banFile = args[++i];
            }

            error = null;
            return true;
        }

        // 地域はロビーの属性でしか使わないので、ロビーに載らないサーバーで受けると、地域を分けたつもりで分かれていない運用になる。
        // 大文字を小文字に直さず断るのは、設定の地域と突き合わせる名前を、運用する人が書いたとおりにログと揃えるため。
        private static bool TryParseRegion(IReadOnlyList<string> args, NetLaunchOptions options, out string region, out string error)
        {
            region = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], RegionFlag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1][0] == '-')
                {
                    error = RegionFlag + " requires a value";
                    return false;
                }

                if (region != null)
                {
                    error = RegionFlag + " is given more than once";
                    return false;
                }

                var text = args[++i];
                if (!ServerLobby.IsRegionName(text))
                {
                    error = RegionFlag + " must be " + ServerLobby.RegionRule + " (got '" + text + "')";
                    return false;
                }

                region = text;
            }

            if (region != null && !options.RegistersLobby)
            {
                error = RegionFlag + " is published only in the server lobby; pass " + NetLaunchOptions.RegisterLobbyFlag + " too";
                return false;
            }

            error = null;
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
            if (options.SimulatesNetwork)
            {
                return NetLaunchOptions.NetLatencyFlag + " / " + NetLaunchOptions.NetLossFlag + " / " + NetLaunchOptions.NetJitterFlag
                    + " are not supported by the .NET server yet";
            }

            return null;
        }
    }
}
