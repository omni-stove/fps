using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Shooter.Core;
using Shooter.EosSdk;
using Shooter.Protocol;
using Shooter.Server.Eos;

namespace Shooter.Server
{
    // 終了コードを Unity の専用サーバー (NetBootstrap) と同じ番号にするのは、運用の監視をそのまま使えるようにするため。
    internal static class ServerProgram
    {
        public const string LogTag = "[Server]";

        public const int ExitOk = 0;

        public const int ExitFailure = 1;

        public const int ExitUsage = 2;

        public const int ExitMisconfigured = 3;

        public const int ExitStartFailed = 4;

        public const int ExitLobbyFailed = 7;

        public static int Run(IReadOnlyList<string> args, IEnumerable<IServerModule> modules, ServerRuntime runtime)
        {
            ArgumentNullException.ThrowIfNull(modules);

            return Run(args, (ServerLaunch _, ServerRuntime _, ServerRulesData _, ICollection<IServerModule> created, out string error) =>
            {
                foreach (var module in modules)
                {
                    created.Add(module);
                }

                error = null;
                return true;
            }, runtime);
        }

        public static int Run(IReadOnlyList<string> args, ServerModuleFactory createModules, ServerRuntime runtime)
        {
            ArgumentNullException.ThrowIfNull(args);
            ArgumentNullException.ThrowIfNull(createModules);
            ArgumentNullException.ThrowIfNull(runtime);

            var output = runtime.Output;
            var error = runtime.Error;
            if (!ServerLaunch.TryParse(args, runtime.Environment, runtime.BuiltVersion, out var launch, out var problem))
            {
                error.WriteLine("[Net] invalid arguments: " + problem);
                return ExitUsage;
            }

            output.WriteLine(BuildVersion.LogLine(launch.Version));
            if (!TryPlanTransport(launch, runtime, out var plan, out problem))
            {
                error.WriteLine(problem);
                return ExitMisconfigured;
            }

            launch = launch.WithOptions(plan.Options);
            output.WriteLine(LogTag + " " + launch.Options);
            output.WriteLine(launch.AntiCheat.Describe());
            output.WriteLine(EosSdkBuild.LogLine(AppContext.BaseDirectory));

            // 最初のマップとほかに読むマップはルールの候補 (プレイリストがあればその項目) で決まるので、ルールを先に読む
            ServerPlaylist playlist = null;
            ServerRulesData rules;
            if (launch.Playlist != null)
            {
                // 混ぜる並びをサーバーごとに変えるため、起動ごとに種を取る
                if (!ServerPlaylist.TryLoad(launch.Playlist, runtime.MapSearchRoots, RandomNumberGenerator.GetInt32(int.MaxValue), out playlist, out problem))
                {
                    error.WriteLine(LogTag + " " + problem);
                    return ExitMisconfigured;
                }

                output.WriteLine(ServerPlaylistLog.Loaded(playlist));
                rules = playlist.Current.Data;
            }
            else if (!ServerRulesData.TryLoad(launch.Rules, runtime.MapSearchRoots, out rules, out problem))
            {
                error.WriteLine(LogTag + " " + problem);
                return ExitMisconfigured;
            }

            output.WriteLine(rules.LogLine);

            // 持ち物庫を置けない Extraction の試合を始めると、脱出しても持ち帰れないので、待ち受ける前に止める
            problem = launch.StashProblem(Objectives(rules, playlist));
            if (problem != null)
            {
                error.WriteLine(LogTag + " " + problem);
                return ExitMisconfigured;
            }

            if (launch.StashDirectory != null)
            {
                if (!ServerStashStore.TryOpen(launch.StashDirectory, output, out var stashes, out problem))
                {
                    error.WriteLine(ExtractionRulesLog.StashTag + " " + problem);
                    return ExitMisconfigured;
                }

                output.WriteLine(ExtractionRulesLog.StashDirectory(stashes.Directory));
            }

            // コードを当てれば非公開の試合に入れるので、続きを読まれうる System.Random ではなく JoinCode と同じ暗号の乱数で作る
            rules = rules.WithSettings(launch.ApplyPrivacy(rules.Settings, RandomNumberGenerator.GetInt32, out var generatedJoinCode));
            output.WriteLine(ServerLaunch.PrivacyLogLine(rules.Settings, generatedJoinCode));

            var mapPool = PlaylistMapPool(playlist, rules.Settings.MapPool);
            var startMap = playlist != null ? playlist.Current.Map : launch.StartMap(mapPool);
            if (!ServerMapData.TryLoad(startMap, runtime.MapSearchRoots, out var map, out problem))
            {
                error.WriteLine(LogTag + " " + problem);
                return ExitMisconfigured;
            }

            output.WriteLine(map.LogLine);
            if (!ServerMapData.TryLoadPool(map, mapPool, runtime.MapSearchRoots, out var maps, out problem))
            {
                error.WriteLine(LogTag + " " + problem);
                return ExitMisconfigured;
            }

            for (var i = 1; i < maps.Count; i++)
            {
                output.WriteLine(maps[i].LogLine);
            }

            if (mapPool.Count > 0)
            {
                output.WriteLine(ServerMapData.PoolLogLine(maps));
            }

            // 壊れた一覧で起動すると、BAN したはずの人を黙って入れてしまうので止める
            if (!ServerBanList.TryLoad(launch.BanFile, out var bans, out problem))
            {
                error.WriteLine(ServerAdminLog.LogTag + " " + problem);
                return ExitMisconfigured;
            }

            output.WriteLine(ServerAdminLog.Loaded(bans));

            var modules = new List<IServerModule>();
            if (!createModules(launch, runtime, rules, modules, out problem))
            {
                error.WriteLine(problem);
                return ExitMisconfigured;
            }

            var host = new ServerHost();
            foreach (var module in modules)
            {
                host.Register(module);
            }

            if (runtime.Input != null)
            {
                host.Register(new ServerAdminConsole(runtime.Input, bans, () => DateTime.UtcNow));
            }

            var json = launch.Options.LogFormat == ServerLogFormat.Json;
            if (json)
            {
                output.WriteLine("[Net] match events are also written to stdout as JSON lines (" + NetLaunchOptions.LogFormatFlag + " json)");
            }

            if (!TryCreateTransport(launch, plan, runtime, out var created, out problem))
            {
                error.WriteLine(problem);
                return ExitMisconfigured;
            }

            using (var transport = created)
            {
                var server = new MatchServer(
                    launch,
                    transport,
                    host,
                    map,
                    output,
                    new ServerEventWriter(json, output, output),
                    () => DateTime.UtcNow,
                    rules.Settings.RestartDelaySeconds,
                    rules,
                    maps,
                    bans,
                    playlist);
                if (playlist != null)
                {
                    output.WriteLine(ServerPlaylistLog.Entry(playlist, playlist.Current, playlist.Round));
                }

                var code = Start(server, error);
                if (code != ExitOk)
                {
                    return code;
                }

                using (var loop = new ServerLoop(server))
                using (runtime.StopSignal.Subscribe(loop.RequestStop))
                {
                    loop.Run();
                }

                var stopped = Shutdown(server, error);
                return stopped == ExitOk ? server.ExitCode : stopped;
            }
        }

        // プレイリストなら、どの項目の目的も試合になりうるので全部を返す
        internal static IReadOnlyList<MatchObjective> Objectives(ServerRulesData rules, ServerPlaylist playlist)
        {
            ArgumentNullException.ThrowIfNull(rules);
            var objectives = new List<MatchObjective> { rules.Rules.Objective };
            if (playlist != null)
            {
                foreach (var entry in playlist.Entries)
                {
                    objectives.Add(entry.Rules.Objective);
                }
            }

            return objectives;
        }

        // 項目のマップを先に並べ、最初の項目のルールの mapPool を後ろに足す。投票はこの全部から候補を選ぶので、mapPool のマップへも投票で替えられる
        internal static IReadOnlyList<string> PlaylistMapPool(ServerPlaylist playlist, IReadOnlyList<string> mapPool)
        {
            ArgumentNullException.ThrowIfNull(mapPool);
            if (playlist == null)
            {
                return mapPool;
            }

            var names = new List<string>(playlist.MapNames);
            foreach (var name in mapPool)
            {
                if (!names.Contains(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        // Unity のサーバー (NetBootstrap) と同じく、EOS を求められても認証情報が揃わなければ UDP で待ち受ける。
        // 認証情報のファイルが壊れているときは、黙って UDP にすると誤りに気づけないので止める。
        private static bool TryPlanTransport(ServerLaunch launch, ServerRuntime runtime, out NetTransportPlan plan, out string error)
        {
            plan = null;
            EosCredentials credentials = null;
            IReadOnlyList<string> missing = null;
            if (launch.Options.Transport == NetTransportKind.Eos
                && !EosCredentialSource.TryLoad(runtime.Environment, runtime.CredentialSearchRoots, out credentials, out missing, out var loadError))
            {
                error = "[Net] cannot load EOS credentials: " + loadError;
                return false;
            }

            plan = NetTransportPlan.Decide(launch.Options, credentials, missing);
            if (plan.FellBackToUdp && launch.AntiCheat.Enabled)
            {
                // EAC はクライアントを ProductUserId で登録するので、UDP に切り替えて守られないまま待ち受けない
                error = AntiCheatSettings.LogTag + " " + AntiCheatSettings.EacFlag + " needs EOS credentials; missing " + string.Join(", ", plan.MissingCredentials)
                    + " (set them as environment variables or in " + EosCredentials.LocalFileName + ")";
                plan = null;
                return false;
            }

            if (plan.FellBackToUdp)
            {
                runtime.Output.WriteLine(NetTransportPlan.CredentialsMissingLog);
                runtime.Output.WriteLine(plan.DescribeMissingCredentials());
            }

            error = null;
            return true;
        }

        private static bool TryCreateTransport(
            ServerLaunch launch, NetTransportPlan plan, ServerRuntime runtime, out IMatchTransport transport, out string error)
        {
            if (launch.Options.Transport != NetTransportKind.Eos)
            {
                transport = runtime.CreateTransport();
                error = null;
                return true;
            }

            if (!runtime.CreateEosTransport(plan.Credentials, launch.Version, runtime.Output, out transport, out var eosError))
            {
                error = "[Net] " + NetLaunchOptions.TransportFlag + " " + NetLaunchOptions.TransportName(NetTransportKind.Eos)
                    + " cannot use EOS: " + eosError;
                return false;
            }

            error = null;
            return true;
        }

        private static int Start(MatchServer server, TextWriter error)
        {
#pragma warning disable CA1031 // どのモジュールの失敗でも、例外の画面ではなく 1 行の理由と終了コードで知らせるため
            try
            {
                server.Start();
                return ExitOk;
            }
            catch (Exception e)
            {
                // 待ち受けの失敗はモジュールの失敗と分けて知らせる。監視が設定の誤りとポートの取り合いを見分けられるように
                if (server.ListenFailed)
                {
                    error.WriteLine("[Net] failed to start the server: " + e.Message);
                    return ExitStartFailed;
                }

                error.WriteLine(LogTag + " failed to start: " + e.Message);
                return ExitFailure;
            }
#pragma warning restore CA1031
        }

        private static int Shutdown(MatchServer server, TextWriter error)
        {
            try
            {
                server.Shutdown();
                return ExitOk;
            }
            catch (AggregateException e)
            {
                foreach (var inner in e.InnerExceptions)
                {
                    error.WriteLine(LogTag + " failed to stop: " + inner.Message);
                }

                return ExitFailure;
            }
        }
    }
}
