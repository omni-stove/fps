using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.EosSdk;

namespace Fps.Server
{
    /// <summary>終了コードを Unity の専用サーバー (<c>NetBootstrap</c>) と同じ番号にするのは、運用の監視をそのまま使えるようにするため。</summary>
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

            return Run(args, (ServerLaunch _, ServerRuntime _, ICollection<IServerModule> created, out string error) =>
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
            if (!ServerLaunch.TryParse(args, runtime.Environment, out var launch, out var problem))
            {
                error.WriteLine("[Net] invalid arguments: " + problem);
                return ExitUsage;
            }

            output.WriteLine(BuildVersion.LogLine(launch.Version));
            output.WriteLine(LogTag + " " + launch.Options);
            output.WriteLine(EosSdkBuild.LogLine(AppContext.BaseDirectory));

            if (!ServerMapData.TryLoad(launch.Map, runtime.MapSearchRoots, out var map, out problem))
            {
                error.WriteLine(LogTag + " " + problem);
                return ExitMisconfigured;
            }

            output.WriteLine(map.LogLine);

            if (!ServerRulesData.TryLoad(launch.Rules, runtime.MapSearchRoots, out var rules, out problem))
            {
                error.WriteLine(LogTag + " " + problem);
                return ExitMisconfigured;
            }

            output.WriteLine(rules.LogLine);

            var modules = new List<IServerModule>();
            if (!createModules(launch, runtime, modules, out problem))
            {
                error.WriteLine(problem);
                return ExitMisconfigured;
            }

            var host = new ServerHost();
            foreach (var module in modules)
            {
                host.Register(module);
            }

            var json = launch.Options.LogFormat == ServerLogFormat.Json;
            if (json)
            {
                output.WriteLine("[Net] match events are also written to stdout as JSON lines (" + NetLaunchOptions.LogFormatFlag + " json)");
            }

            using (var transport = runtime.CreateTransport())
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
                    rules);
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
