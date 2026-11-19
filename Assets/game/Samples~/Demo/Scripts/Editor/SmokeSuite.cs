using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Shooter.Editor
{
    // 1 つの Editor で順に走らせるため、各 SmokeRun は終わるときに TakeOver を呼び、スイートが走っている間は Editor を閉じずに結果をここへ渡す。
    // スクリプトが読み直されても続きを走らせられるよう、進み具合と結果は SessionState に置く。
    [InitializeOnLoad]
    public static class SmokeSuite
    {
        public const string LogTag = "[SmokeSuite]";

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        // 各 SmokeRun の Play Mode に入れないときの待ち (120 秒) と走らせる秒数を足しても収まる長さにしてある。
        public const float RunTimeoutSeconds = 600f;

        private const string ActiveKey = "Shooter.SmokeSuite.Active";
        private const string NextKey = "Shooter.SmokeSuite.Next";
        private const string RunningKey = "Shooter.SmokeSuite.Running";
        private const string StartedAtKey = "Shooter.SmokeSuite.StartedAt";
        private const string ResultsKey = "Shooter.SmokeSuite.Results";

        private const int NotRunning = -1;
        private const char ResultSeparator = ';';
        private const char CodeSeparator = '=';

        public static IReadOnlyList<SmokeRunEntry> Runs { get; } = new[]
        {
            new SmokeRunEntry(nameof(CampaignSmokeRun), CampaignSmokeRun.LogTag, CampaignSmokeRun.Run),
            new SmokeRunEntry(nameof(DestructionSmokeRun), DestructionSmokeRun.LogTag, DestructionSmokeRun.Run),
            new SmokeRunEntry(nameof(EnemyFireSmokeRun), EnemyFireSmokeRun.LogTag, EnemyFireSmokeRun.Run),
            new SmokeRunEntry(nameof(EnemyStuckSmokeRun), EnemyStuckSmokeRun.LogTag, EnemyStuckSmokeRun.Run),
            new SmokeRunEntry(nameof(WeaponSwitchSmokeRun), WeaponSwitchSmokeRun.LogTag, WeaponSwitchSmokeRun.Run),
            new SmokeRunEntry(nameof(LauncherSmokeRun), LauncherSmokeRun.LogTag, LauncherSmokeRun.Run),
            new SmokeRunEntry(nameof(VehicleSmokeRun), VehicleSmokeRun.LogTag, VehicleSmokeRun.Run),
            new SmokeRunEntry(nameof(InfectedSmokeRun), InfectedSmokeRun.LogTag, InfectedSmokeRun.Run),
            new SmokeRunEntry(nameof(ExtractionSmokeRun), ExtractionSmokeRun.LogTag, ExtractionSmokeRun.Run),
        };

        public static bool IsActive => SessionState.GetBool(ActiveKey, false);

        static SmokeSuite()
        {
            if (IsActive)
            {
                Attach();
            }
        }

        public static void RunAll()
        {
            // -quit を付けると、このメソッドから戻った時点で Editor が終了コード 0 で閉じ、SmokeRun が 1 つも走らないまま緑になる。
            if (Application.isBatchMode && HasQuitArgument(Environment.GetCommandLineArgs()))
            {
                Debug.LogError($"{LogTag} -quit was given; the smoke runs enter play mode and need the editor to stay open (exit code {FailureExitCode})");
                EditorApplication.Exit(FailureExitCode);
                return;
            }

            SessionState.SetBool(ActiveKey, true);
            SessionState.SetInt(NextKey, 0);
            SessionState.SetInt(RunningKey, NotRunning);
            SessionState.SetString(ResultsKey, string.Empty);
            SessionState.EraseFloat(StartedAtKey);
            Attach();

            Debug.Log($"{LogTag} running {Runs.Count} smoke run(s): {string.Join(", ", NamesOf(Runs))}");
        }

        public static bool TakeOver(string logTag, int exitCode)
        {
            if (!IsActive)
            {
                return false;
            }

            var running = SessionState.GetInt(RunningKey, NotRunning);
            if (running < 0 || running >= Runs.Count)
            {
                // スイートが呼んでいない SmokeRun の結果。スイートの判定には入れないが、Editor は閉じさせない。
                Debug.LogWarning($"{LogTag} {logTag} finished (exit code {exitCode}) while no smoke run was started by the suite; ignored");
            }
            else
            {
                Record(Runs[running].Name, exitCode);
                SessionState.SetInt(RunningKey, NotRunning);
                Debug.Log($"{LogTag} {Runs[running].Name} finished with exit code {exitCode}");
            }

            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }

            return true;
        }

        // Unity と同じく、大文字小文字は区別しない。
        public static bool HasQuitArgument(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            foreach (var arg in args)
            {
                if (string.Equals(arg, "-quit", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        // 形の崩れた項目は、成功と取り違えないよう終了コード FailureExitCode として扱う。
        public static IReadOnlyList<KeyValuePair<string, int>> ParseResults(string text)
        {
            var results = new List<KeyValuePair<string, int>>();
            if (string.IsNullOrEmpty(text))
            {
                return results;
            }

            foreach (var item in text.Split(ResultSeparator))
            {
                if (item.Length == 0)
                {
                    continue;
                }

                var at = item.LastIndexOf(CodeSeparator);
                if (at <= 0
                    || !int.TryParse(item.Substring(at + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var code))
                {
                    results.Add(new KeyValuePair<string, int>(item, FailureExitCode));
                    continue;
                }

                results.Add(new KeyValuePair<string, int>(item.Substring(0, at), code));
            }

            return results;
        }

        public static string FormatResults(IEnumerable<KeyValuePair<string, int>> results)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            var text = new StringBuilder();
            foreach (var result in results)
            {
                if (string.IsNullOrEmpty(result.Key)
                    || result.Key.IndexOf(ResultSeparator) >= 0
                    || result.Key.IndexOf(CodeSeparator) >= 0)
                {
                    throw new ArgumentException($"run name '{result.Key}' must be non-empty and contain neither '{ResultSeparator}' nor '{CodeSeparator}'", nameof(results));
                }

                if (text.Length > 0)
                {
                    text.Append(ResultSeparator);
                }

                text.Append(result.Key).Append(CodeSeparator).Append(result.Value.ToString(CultureInfo.InvariantCulture));
            }

            return text.ToString();
        }

        public static int ExitCodeFor(IReadOnlyList<KeyValuePair<string, int>> results, IReadOnlyList<SmokeRunEntry> expected)
        {
            if (results == null)
            {
                throw new ArgumentNullException(nameof(results));
            }

            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            var passed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in results)
            {
                if (result.Value != SuccessExitCode)
                {
                    return FailureExitCode;
                }

                passed.Add(result.Key);
            }

            foreach (var run in expected)
            {
                if (!passed.Contains(run.Name))
                {
                    return FailureExitCode;
                }
            }

            return SuccessExitCode;
        }

        private static void Attach()
        {
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void Detach()
        {
            EditorApplication.update -= OnUpdate;
        }

        private static void Record(string name, int exitCode)
        {
            var results = new List<KeyValuePair<string, int>>(ParseResults(SessionState.GetString(ResultsKey, string.Empty)))
            {
                new KeyValuePair<string, int>(name, exitCode),
            };
            SessionState.SetString(ResultsKey, FormatResults(results));
        }

        private static void OnUpdate()
        {
            if (!IsActive)
            {
                Detach();
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            var running = SessionState.GetInt(RunningKey, NotRunning);
            if (running >= 0)
            {
                var startedAt = SessionState.GetFloat(StartedAtKey, (float)now);
                if (now - startedAt > RunTimeoutSeconds)
                {
                    // 走っている SmokeRun を外から止める手立てが無いので、続きは走らせずにスイートごと終える。
                    var name = running < Runs.Count ? Runs[running].Name : running.ToString(CultureInfo.InvariantCulture);
                    Debug.LogError($"{LogTag} {name} did not finish within {RunTimeoutSeconds}s");
                    Record(name, FailureExitCode);
                    Complete();
                }

                return;
            }

            // 前の SmokeRun の Play Mode を抜け切り、生成したアセットの読み込みが済んでから次を呼ぶ。
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            var next = SessionState.GetInt(NextKey, 0);
            if (next >= Runs.Count)
            {
                Complete();
                return;
            }

            StartRun(next, now);
        }

        private static void StartRun(int index, double now)
        {
            var run = Runs[index];
            SessionState.SetInt(NextKey, index + 1);
            SessionState.SetInt(RunningKey, index);
            SessionState.SetFloat(StartedAtKey, (float)now);
            Debug.Log($"{LogTag} ({index + 1}/{Runs.Count}) starting {run.Name}");

            try
            {
                run.Run();
            }
            catch (Exception e)
            {
                // 各 SmokeRun は自分で例外を受けて TakeOver を呼ぶが、それより前で投げたときもスイートを止めずに次へ進める。
                Debug.LogException(e);
                if (SessionState.GetInt(RunningKey, NotRunning) == index)
                {
                    TakeOver(run.LogTag, FailureExitCode);
                }
            }
        }

        private static void Complete()
        {
            var results = ParseResults(SessionState.GetString(ResultsKey, string.Empty));
            var exitCode = ExitCodeFor(results, Runs);

            Detach();
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseInt(NextKey);
            SessionState.EraseInt(RunningKey);
            SessionState.EraseFloat(StartedAtKey);
            SessionState.EraseString(ResultsKey);

            var lines = new StringBuilder();
            var reported = new HashSet<string>(StringComparer.Ordinal);
            foreach (var result in results)
            {
                reported.Add(result.Key);
                lines.Append("\n  ").Append(result.Value == SuccessExitCode ? "passed " : "FAILED ")
                    .Append(result.Key).Append(" (exit code ").Append(result.Value.ToString(CultureInfo.InvariantCulture)).Append(')');
            }

            foreach (var run in Runs)
            {
                if (!reported.Contains(run.Name))
                {
                    lines.Append("\n  NOT RUN ").Append(run.Name);
                }
            }

            var summary = $"{LogTag} {(exitCode == SuccessExitCode ? "passed" : "failed")}:{lines} (exit code {exitCode})";
            if (exitCode == SuccessExitCode)
            {
                Debug.Log(summary);
            }
            else
            {
                Debug.LogError(summary);
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
            else if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
        }

        private static IEnumerable<string> NamesOf(IEnumerable<SmokeRunEntry> runs)
        {
            foreach (var run in runs)
            {
                yield return run.Name;
            }
        }
    }

    public sealed class SmokeRunEntry
    {
        public SmokeRunEntry(string name, string logTag, Action run)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name must not be empty", nameof(name));
            }

            Name = name;
            LogTag = logTag ?? throw new ArgumentNullException(nameof(logTag));
            Run = run ?? throw new ArgumentNullException(nameof(run));
        }

        public string Name { get; }

        public string LogTag { get; }

        public Action Run { get; }
    }
}
