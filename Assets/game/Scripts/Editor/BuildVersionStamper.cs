using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using Shooter.Core;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Shooter.Editor
{
    // ProjectSettings を版ごとに書き換えてコミットの差分に残さないため、版はビルドの間だけ PlayerSettings.bundleVersion に入れ、終わったら (失敗しても) 戻す。
    public static class BuildVersionStamper
    {
        public const string LogTag = "[BuildVersion]";

        private const string GitDescribeArguments = "describe --tags --always --dirty";

        // 大きなリポジトリでも describe は一瞬で終わる。止まったら (認証の入力待ちなど) ビルドごと待たせないように打ち切る
        private const int GitTimeoutMilliseconds = 30000;

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

        public static T BuildWithResolvedVersion<T>(Func<T> build)
        {
            return WithVersion(Resolve(Environment.GetCommandLineArgs()), build);
        }

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
