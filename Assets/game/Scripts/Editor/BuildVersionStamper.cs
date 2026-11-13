using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Fps.Core;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Fps.Editor
{
    /// <summary>
    /// ビルドの版を決めて PlayerSettings.bundleVersion に入れ、ビルドが終わったら (失敗しても) 元の値に戻す。
    /// 版はコマンドライン引数 <c>-buildVersion &lt;版&gt;</c>、無ければ <c>git describe --tags --always --dirty</c> の出力を
    /// <see cref="BuildVersion"/> で正規化したもの。実行時は Application.version として読め、起動時に <c>[Version]</c> の行で出る。
    /// ProjectSettings を版ごとに書き換えてコミットの差分に残さないため、ビルドの間だけ入れる。
    /// </summary>
    public static class BuildVersionStamper
    {
        public const string LogTag = "[BuildVersion]";

        private const string GitDescribeArguments = "describe --tags --always --dirty";

        // 大きなリポジトリでも describe は一瞬で終わる。止まったら (認証の入力待ちなど) ビルドごと待たせないように打ち切る
        private const int GitTimeoutMilliseconds = 30000;

        /// <summary>
        /// コマンドライン引数の <c>-buildVersion</c>、無ければプロジェクトのルートでの git describe から版を決める。
        /// どちらからも決められなければ例外を投げる (batchmode の Unity は終了コード 1 で終わる)。
        /// </summary>
        public static string Resolve(string[] args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            if (!BuildVersion.TryParseArgs(args, out var fromArgs, out var error))
            {
                throw new ArgumentException(LogTag + " invalid arguments: " + error);
            }

            if (fromArgs != null)
            {
                Debug.Log(LogTag + " using " + fromArgs + " from " + BuildVersion.Flag);
                return fromArgs;
            }

            var described = DescribeWithGit(ProjectRoot());
            if (!BuildVersion.TryNormalize(described, out var version, out var normalizeError))
            {
                throw new InvalidOperationException(
                    LogTag + " git " + GitDescribeArguments + " gave an unusable version: " + normalizeError
                    + "; pass " + BuildVersion.Flag + " <version> instead");
            }

            Debug.Log(LogTag + " using " + version + " from git " + GitDescribeArguments);
            return version;
        }

        /// <summary>
        /// PlayerSettings.bundleVersion を version にして build を呼び、終わったら元の値に戻す。
        /// build が例外を投げても戻してから投げ直す。
        /// </summary>
        public static T WithVersion<T>(string version, Func<T> build)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            version = BuildVersion.Normalize(version);
            var original = PlayerSettings.bundleVersion;
            if (string.Equals(original, version, StringComparison.Ordinal))
            {
                Debug.Log(LogTag + " bundleVersion is already " + version);
                return build();
            }

            Debug.Log(LogTag + " stamping bundleVersion " + version + " (was " + original + ") for this build");
            PlayerSettings.bundleVersion = version;
            try
            {
                return build();
            }
            finally
            {
                Restore(original);
            }
        }

        /// <summary>コマンドライン引数から版を決め (<see cref="Resolve"/>)、その版を入れて build を呼ぶ (<see cref="WithVersion{T}"/>)。</summary>
        public static T BuildWithResolvedVersion<T>(Func<T> build)
        {
            return WithVersion(Resolve(Environment.GetCommandLineArgs()), build);
        }

        /// <summary>
        /// projectRoot で <c>git describe --tags --always --dirty</c> を実行し、その出力 (前後の空白は落とさない) を返す。
        /// git が無い、リポジトリでない、時間内に終わらないときは例外を投げる。
        /// </summary>
        public static string DescribeWithGit(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new ArgumentException("projectRoot must not be empty", nameof(projectRoot));
            }

            var startInfo = new ProcessStartInfo("git", GitDescribeArguments)
            {
                WorkingDirectory = projectRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // 端末の無い batchmode で資格情報などを聞きに来て止まらないようにする
            startInfo.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Win32Exception e)
            {
                throw new InvalidOperationException(
                    LogTag + " could not run git (" + e.Message + "); install git or pass " + BuildVersion.Flag + " <version>", e);
            }

            if (process == null)
            {
                throw new InvalidOperationException(LogTag + " could not start git; pass " + BuildVersion.Flag + " <version>");
            }

            using (process)
            {
                // 片方のパイプが詰まって git が止まらないよう、標準出力と標準エラーを並行して読む
                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(GitTimeoutMilliseconds))
                {
                    try
                    {
                        process.Kill();
                    }
                    catch (InvalidOperationException)
                    {
                        // 打ち切る直前に終わっていた
                    }

                    throw new InvalidOperationException(
                        LogTag + " git " + GitDescribeArguments + " did not finish in " + (GitTimeoutMilliseconds / 1000)
                        + " s; pass " + BuildVersion.Flag + " <version>");
                }

                var output = stdout.Result;
                var errors = stderr.Result;
                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        LogTag + " git " + GitDescribeArguments + " failed with exit code " + process.ExitCode + " in " + projectRoot
                        + ": " + errors.Trim() + "; pass " + BuildVersion.Flag + " <version>");
                }

                return output;
            }
        }

        /// <summary>Assets の 1 つ上 (ProjectSettings と同じ階層) のディレクトリ。</summary>
        private static string ProjectRoot()
        {
            return Directory.GetParent(UnityEngine.Application.dataPath).FullName;
        }

        private static void Restore(string original)
        {
            PlayerSettings.bundleVersion = original;

            // ビルドの途中で ProjectSettings.asset が版を入れたまま書き出されていても、戻した値で書き直して差分を残さない
            AssetDatabase.SaveAssets();
            Debug.Log(LogTag + " restored bundleVersion " + original);
        }
    }
}
