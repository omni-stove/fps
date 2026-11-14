using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;

namespace Fps.Server
{
    /// <summary>
    /// サーバーの起動の手順。引数を読み、版を出し、<see cref="ServerHost"/> にモジュールを載せて動かす。
    /// 引数は Unity の専用サーバーと同じ (<c>-mode server</c> / <c>-address</c> / <c>-port</c> / <c>-buildVersion</c> など) で、
    /// <c>-mode</c> を省いたときもサーバーとして動く。
    /// </summary>
    internal static class ServerProgram
    {
        public const string LogTag = "[Server]";

        /// <summary>正常に終わった。</summary>
        public const int ExitOk = 0;

        /// <summary>サーバーが起動・停止に失敗した。</summary>
        public const int ExitFailure = 1;

        /// <summary>引数が正しくない。</summary>
        public const int ExitUsage = 2;

        /// <summary>
        /// 引数を読んでサーバーを動かし、終了コードを返す。モジュールは <paramref name="modules"/> の順に登録する。
        /// 試合のループはまだ無いので、すべてのモジュールを起動したらそのまま止める。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <param name="modules">登録するモジュール。</param>
        /// <param name="output">ログの出力先。</param>
        /// <param name="error">誤りの出力先。</param>
        public static int Run(IReadOnlyList<string> args, IEnumerable<IServerModule> modules, TextWriter output, TextWriter error)
        {
            ArgumentNullException.ThrowIfNull(args);

            ArgumentNullException.ThrowIfNull(modules);

            ArgumentNullException.ThrowIfNull(output);

            ArgumentNullException.ThrowIfNull(error);

            if (!TryParseArgs(args, out var options, out var version, out var problem))
            {
                error.WriteLine(LogTag + " " + problem);
                return ExitUsage;
            }

            output.WriteLine(BuildVersion.LogLine(version));
            output.WriteLine(LogTag + " " + options);

            var host = new ServerHost();
            foreach (var module in modules)
            {
                host.Register(module);
            }

            var context = new ServerContext(options, version, output);
#pragma warning disable CA1031 // どのモジュールの失敗でも、例外の画面ではなく 1 行の理由と終了コードで知らせるため
            try
            {
                host.Start(context);
            }
            catch (Exception e)
            {
                error.WriteLine(LogTag + " failed to start: " + e.Message);
                return ExitFailure;
            }
#pragma warning restore CA1031

            try
            {
                host.Stop();
            }
            catch (AggregateException e)
            {
                foreach (var inner in e.InnerExceptions)
                {
                    error.WriteLine(LogTag + " failed to stop: " + inner.Message);
                }

                return ExitFailure;
            }

            return ExitOk;
        }

        /// <summary>
        /// サーバーの引数を読む。<c>-mode</c> は無いか <c>server</c> でなければならない。
        /// </summary>
        /// <param name="args">コマンドライン引数。</param>
        /// <param name="options">成功したときの起動設定。失敗したときは null。</param>
        /// <param name="version">成功したときの <c>-buildVersion</c> の版。無いか失敗したときは null。</param>
        /// <param name="error">失敗したときの理由。成功したときは null。</param>
        public static bool TryParseArgs(
            IReadOnlyList<string> args, out NetLaunchOptions options, out string version, out string error)
        {
            ArgumentNullException.ThrowIfNull(args);

            version = null;
            if (!NetLaunchOptions.TryParse(args, NetLaunchMode.Server, out options, out error))
            {
                return false;
            }

            if (options.Mode != NetLaunchMode.Server)
            {
                error = "this is a dedicated server; " + NetLaunchOptions.ModeFlag + " must be "
                    + NetLaunchOptions.ModeName(NetLaunchMode.Server) + " (got " + NetLaunchOptions.ModeName(options.Mode) + ")";
                options = null;
                return false;
            }

            if (!BuildVersion.TryParseArgs(args, out version, out error))
            {
                options = null;
                return false;
            }

            return true;
        }
    }
}
