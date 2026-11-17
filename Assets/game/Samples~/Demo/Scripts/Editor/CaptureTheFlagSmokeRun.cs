using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    // 試合が数分かかるので SmokeSuite には入れていない。
    // batchmode には入力が無いので、プレイヤーは出現点に立ったままで、旗を運ぶのは bot だけになる。
    // Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class CaptureTheFlagSmokeRun
    {
        public const string LogTag = "[CaptureTheFlagSmokeRun]";

        public const string SecondsArgument = "-captureTheFlagSmokeSeconds";

        // 台どうしは 50m 離れていて、bot が敵の台まで行って戻る (100m ほど) のに 30 秒ほどかかる。
        // 撃ち合いで倒れて出直す分を何度か足しても、1 回は運び込める長さにする。
        public const float DefaultSeconds = 180f;

        // bot が敵の台まで行って戻るだけの長さより短いと、旗を運び込めない。
        public const float MinimumSeconds = 30f;

        // GameBootstrap の Awake と Runner の Start を済ませてから見始める。
        public const float WarmUpSeconds = 1f;

        public const float GraceSeconds = 30f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.CaptureTheFlagSmokeRun.Active";
        private const string SecondsKey = "Shooter.CaptureTheFlagSmokeRun.Seconds";
        private const string RequestedAtKey = "Shooter.CaptureTheFlagSmokeRun.RequestedAt";

        private static readonly string _takenPrefix = CaptureTheFlagLog.Tag + " flag-taken ";
        private static readonly string _capturedPrefix = CaptureTheFlagLog.Tag + " flag-captured ";
        private static readonly string _endedPrefix = ModeLog.Tag + " ended winner=";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static int _taken;
        private static int _captured;
        private static string _capturedLine;
        private static string _endedLine;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        public enum LineKind
        {
            Other,
            Taken,
            Captured,
            Ended,
        }

        private enum Step
        {
            CheckStart,
            WaitEnd,
            Done,
        }

        static CaptureTheFlagSmokeRun()
        {
            // Play Mode に入るときにスクリプトが読み直された場合は、ここから続きを見張る。
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        public static void Run()
        {
            try
            {
                var seconds = ParseSeconds(Environment.GetCommandLineArgs());

                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                CaptureTheFlagSceneBuilder.Build();
                var scene = EditorSceneManager.OpenScene(CaptureTheFlagSceneBuilder.ScenePathValue, OpenSceneMode.Single);

                var runner = UnityEngine.Object.FindAnyObjectByType<CaptureTheFlagRunner>();
                if (runner == null)
                {
                    throw new InvalidOperationException(
                        $"'{CaptureTheFlagSceneBuilder.ScenePathValue}' has no CaptureTheFlagRunner; rerun {CaptureTheFlagSceneBuilder.MenuPath}");
                }

                // 調整値のアセットを書き換えると保存しなくてもメモリ上で後の実行に残る。Definition を外して Inspector の値を縮めても、
                // 入口 (GameContent) に旗取りのアセットがあればそちらが読まれるので、読まれる方を複製して縮め、それを配線する。
                runner.Definition = Shorten(runner.EffectiveDefinition, seconds);
                runner.TimeLimitSeconds = seconds;
                EditorSceneManager.MarkSceneDirty(scene);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(SecondsKey, seconds);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log(
                    $"{LogTag} entering play mode in '{CaptureTheFlagSceneBuilder.ScenePathValue}' with a {seconds.ToString("0.##", CultureInfo.InvariantCulture)}s time limit");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        public static float ParseSeconds(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], SecondsArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count
                    || !float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                    || !(seconds >= MinimumSeconds)
                    || float.IsInfinity(seconds))
                {
                    throw new ArgumentException(
                        $"{SecondsArgument} needs a finite number of seconds of at least {MinimumSeconds.ToString("0.##", CultureInfo.InvariantCulture)}",
                        nameof(args));
                }

                return seconds;
            }

            return DefaultSeconds;
        }

        // 複製に HideFlags.DontSave を付けるのは、アセットにもシーンにも保存されず、アンロードもされないようにするため。
        public static CaptureTheFlagDefinition Shorten(CaptureTheFlagDefinition source, float seconds)
        {
            var copy = source != null
                ? UnityEngine.Object.Instantiate(source)
                : ScriptableObject.CreateInstance<CaptureTheFlagDefinition>();
            copy.name = (source != null ? source.name : nameof(CaptureTheFlagDefinition)) + " (smoke)";
            copy.hideFlags = HideFlags.DontSave;
            copy.TimeLimitSeconds = seconds;
            try
            {
                copy.ToSpec();
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(copy);
                throw;
            }

            return copy;
        }

        public static LineKind Classify(string line)
        {
            if (line == null)
            {
                return LineKind.Other;
            }

            if (line.StartsWith(_takenPrefix, StringComparison.Ordinal))
            {
                return LineKind.Taken;
            }

            if (line.StartsWith(_capturedPrefix, StringComparison.Ordinal))
            {
                return LineKind.Captured;
            }

            return line.StartsWith(_endedPrefix, StringComparison.Ordinal) ? LineKind.Ended : LineKind.Other;
        }

        private static void Attach()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void Detach()
        {
            Application.logMessageReceived -= OnLog;
            EditorApplication.update -= OnUpdate;
        }

        private static void ResetObservations()
        {
            _errors.Clear();
            _step = Step.CheckStart;
            _taken = 0;
            _captured = 0;
            _capturedLine = null;
            _endedLine = null;
            _playStartedAt = -1d;
            _finished = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            // Play Mode に入る前 (アセットとシーンの生成) のログは判定に使わない。
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                // CampaignSmokeRun と同じく、Editor の起動時インデックスが Play 中に投げる例外はゲームのコードではないので外す。
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
                {
                    return;
                }

                _errors.Add($"{type}: {condition}");
                return;
            }

            switch (Classify(condition))
            {
                case LineKind.Taken:
                    _taken++;
                    break;
                case LineKind.Captured:
                    _captured++;
                    _capturedLine = condition;
                    break;
                case LineKind.Ended:
                    _endedLine = condition;
                    break;
            }
        }

        private static void OnUpdate()
        {
            if (_finished)
            {
                return;
            }

            var now = EditorApplication.timeSinceStartup;
            if (!EditorApplication.isPlaying)
            {
                var requestedAt = SessionState.GetFloat(RequestedAtKey, (float)now);
                if (now - requestedAt > EnterTimeoutSeconds)
                {
                    Finish(FailureExitCode, $"did not enter play mode within {EnterTimeoutSeconds}s");
                }

                return;
            }

            if (_playStartedAt < 0d)
            {
                _playStartedAt = now;
                return;
            }

            if (now - _playStartedAt < WarmUpSeconds)
            {
                return;
            }

            var seconds = SessionState.GetFloat(SecondsKey, DefaultSeconds);
            string problem;
            try
            {
                problem = Advance(seconds);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                problem = $"{e.GetType().Name} at step {_step}: {e.Message}";
            }

            var timeout = WarmUpSeconds + seconds + GraceSeconds;
            if (problem == null && _step != Step.Done && now - _playStartedAt > timeout)
            {
                problem = $"step {_step} did not finish within {timeout.ToString("0.##", CultureInfo.InvariantCulture)}s of play";
            }

            if (problem == null && _step != Step.Done && _errors.Count == 0)
            {
                return;
            }

            Report(problem);
        }

        private static string Advance(float seconds)
        {
            var runner = UnityEngine.Object.FindAnyObjectByType<CaptureTheFlagRunner>();
            if (runner == null)
            {
                return "the scene has no CaptureTheFlagRunner";
            }

            switch (_step)
            {
                case Step.CheckStart:
                    return CheckStart(runner, seconds);

                case Step.WaitEnd:
                    return WaitEnd(runner);

                default:
                    return null;
            }
        }

        private static string CheckStart(CaptureTheFlagRunner runner, float seconds)
        {
            var game = runner.Game;
            if (!runner.IsStarted || game == null)
            {
                return "the CaptureTheFlagRunner did not start a match; see the errors above";
            }

            if (game.Session.State != GameModeState.Playing)
            {
                return $"the match is {game.Session.State}, not {GameModeState.Playing}";
            }

            if (!Mathf.Approximately(game.Spec.TimeLimitSeconds, seconds))
            {
                return $"the time limit is {game.Spec.TimeLimitSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s, not the {seconds.ToString("0.##", CultureInfo.InvariantCulture)}s set before play";
            }

            foreach (var side in CaptureTheFlag.FlagSides)
            {
                if (runner.BaseOf(side) == null)
                {
                    return $"side {side.Id} has no flag base; rerun {CaptureTheFlagSceneBuilder.MenuPath}";
                }

                if (CountBots(runner, side) == 0)
                {
                    return $"side {side.Id} has no bot to carry a flag; rerun {CaptureTheFlagSceneBuilder.MenuPath}";
                }
            }

            _step = Step.WaitEnd;
            return null;
        }

        private static string WaitEnd(CaptureTheFlagRunner runner)
        {
            if (!runner.IsMatchOver || _endedLine == null)
            {
                return runner.IsStarted ? null : "the CaptureTheFlagRunner stopped the match before it ended";
            }

            if (_taken == 0)
            {
                return $"no bot took a flag before the match ended ({_endedLine})";
            }

            if (_captured == 0)
            {
                return $"bots took {_taken} flag(s) but never carried one home before the match ended ({_endedLine})";
            }

            _step = Step.Done;
            return null;
        }

        private static int CountBots(CaptureTheFlagRunner runner, Side side)
        {
            var count = 0;
            foreach (var member in runner.Members)
            {
                if (member != null && member.Side == side && member != runner.LocalPlayer && member.GetComponent<EnemyAgent>() != null)
                {
                    count++;
                }
            }

            return count;
        }

        private static void Report(string problem)
        {
            var problems = new List<string>();
            if (problem != null)
            {
                problems.Add(problem);
            }

            problems.AddRange(_errors);
            var played = _playStartedAt >= 0d ? EditorApplication.timeSinceStartup - _playStartedAt : 0d;
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} step={1} played={2:0.#}s taken={3} captured={4} last capture={5} ended={6} errors={7}",
                LogTag,
                _step,
                played,
                _taken,
                _captured,
                _capturedLine ?? "-",
                _endedLine ?? "-",
                _errors.Count));

            if (problems.Count == 0 && _step == Step.Done)
            {
                Finish(SuccessExitCode, $"passed ({_capturedLine}; {_endedLine})");
            }
            else
            {
                if (problems.Count == 0)
                {
                    problems.Add("not every step was confirmed");
                }

                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", problems));
            }
        }

        private static void Finish(int exitCode, string summary)
        {
            _finished = true;
            Detach();
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(SecondsKey);
            SessionState.EraseFloat(RequestedAtKey);

            var line = $"{LogTag} {summary} (exit code {exitCode})";
            if (exitCode == SuccessExitCode)
            {
                Debug.Log(line);
            }
            else
            {
                Debug.LogError(line);
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
    }
}
