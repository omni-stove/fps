using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Shooter.Editor
{
    // batchmode には入力が無いので、プレイヤーは出現点に立ったままで、台車を押すのは bot だけになる。
    // Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class PayloadSmokeRun
    {
        public const string LogTag = "[PayloadSmokeRun]";

        public const string SecondsArgument = "-payloadSmokeSeconds";

        // 道筋は 37m ほどで、攻め側の bot 3 人で押せば 15 秒ほどで届く。撃ち合いで止められて出直す分を何度か足しても届く長さにする。
        public const float DefaultSeconds = 180f;

        // bot が台車まで歩いて最初の中継点まで押すだけの長さより短いと、中継点を確かめられない。
        public const float MinimumSeconds = 30f;

        // GameBootstrap の Awake と Runner の Start を済ませてから見始める。
        public const float WarmUpSeconds = 1f;

        public const float GraceSeconds = 30f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.PayloadSmokeRun.Active";
        private const string SecondsKey = "Shooter.PayloadSmokeRun.Seconds";
        private const string RequestedAtKey = "Shooter.PayloadSmokeRun.RequestedAt";

        private static readonly string _pushingPrefix = PayloadLog.LogPrefix + " cart=" + PayloadLog.Pushing + " ";
        private static readonly string _checkpointPrefix = PayloadLog.LogPrefix + " checkpoint=";
        private static readonly string _endedPrefix = PayloadLog.LogPrefix + " ended winner=";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static int _pushes;
        private static int _checkpoints;
        private static string _checkpointLine;
        private static string _endedLine;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        public enum LineKind
        {
            Other,
            Pushing,
            Checkpoint,
            Ended,
        }

        private enum Step
        {
            CheckStart,
            WaitEnd,
            Done,
        }

        static PayloadSmokeRun()
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
                PayloadSceneBuilder.Build();
                var scene = EditorSceneManager.OpenScene(PayloadSceneBuilder.ScenePathValue, OpenSceneMode.Single);

                var runner = UnityEngine.Object.FindAnyObjectByType<PayloadRunner>();
                if (runner == null)
                {
                    throw new InvalidOperationException(
                        $"'{PayloadSceneBuilder.ScenePathValue}' has no PayloadRunner; rerun {PayloadSceneBuilder.MenuPath}");
                }

                // 調整値のアセットを書き換えると保存しなくてもメモリ上で後の実行に残るので、読まれる値を写した複製を縮めて配線する。
                runner.Definition = Shorten(runner, PayloadRunner.Tuning, seconds);
                EditorSceneManager.MarkSceneDirty(scene);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(SecondsKey, seconds);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log(
                    $"{LogTag} entering play mode in '{PayloadSceneBuilder.ScenePathValue}' with a {seconds.ToString("0.##", CultureInfo.InvariantCulture)}s time limit");
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

        // payload.json と同じ形で、時間だけを替えて書く。PayloadJson.Parse で読み戻せることはテストで確かめる。
        public static string TuningJson(PayloadSpec spec, float timeLimitSeconds)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var multipliers = new StringBuilder();
            for (var i = 0; i < spec.PusherSpeedMultipliers.Count; i++)
            {
                if (i > 0)
                {
                    multipliers.Append(", ");
                }

                multipliers.Append(Number(spec.PusherSpeedMultipliers[i]));
            }

            return new StringBuilder("{\n")
                .Append("  \"").Append(PayloadJson.CartSpeedItem).Append("\": ").Append(Number(spec.CartSpeed)).Append(",\n")
                .Append("  \"").Append(PayloadJson.MinPushersItem).Append("\": ").Append(spec.MinPushers.ToString(CultureInfo.InvariantCulture)).Append(",\n")
                .Append("  \"").Append(PayloadJson.PusherSpeedMultipliersItem).Append("\": [").Append(multipliers).Append("],\n")
                .Append("  \"").Append(PayloadJson.RollbackDelaySecondsItem).Append("\": ").Append(Number(spec.RollbackDelaySeconds)).Append(",\n")
                .Append("  \"").Append(PayloadJson.RollbackSpeedItem).Append("\": ").Append(Number(spec.RollbackSpeed)).Append(",\n")
                .Append("  \"").Append(PayloadJson.CheckpointBonusSecondsItem).Append("\": ").Append(Number(spec.CheckpointBonusSeconds)).Append(",\n")
                .Append("  \"").Append(PayloadJson.TimeLimitSecondsItem).Append("\": ").Append(Number(timeLimitSeconds)).Append('\n')
                .Append('}')
                .ToString();
        }

        // 複製に HideFlags.DontSave を付けるのは、アセットにもシーンにも保存されず、アンロードもされないようにするため。
        // Definition が付いていればその調整値を、無ければ入口の調整値 (fallback) を写す。
        public static PayloadDefinition Shorten(PayloadRunner runner, PayloadSpec fallback, float seconds)
        {
            if (runner == null)
            {
                throw new ArgumentNullException(nameof(runner));
            }

            if (fallback == null)
            {
                throw new ArgumentNullException(nameof(fallback));
            }

            var source = runner.Definition;
            var spec = (source != null ? source.ReadSpec() : null) ?? fallback;
            var json = TuningJson(spec, seconds);

            // 書いた JSON が読めなければ、Play に入る前にここで止める。
            PayloadJson.Parse(json, LogTag);

            var copy = ScriptableObject.CreateInstance<PayloadDefinition>();
            copy.name = (source != null ? source.name : nameof(PayloadDefinition)) + " (smoke)";
            copy.hideFlags = HideFlags.DontSave;
            copy.AttackerSideId = source != null ? source.AttackerSideId : runner.AttackerSideId;
            copy.RespawnSeconds = source != null ? source.RespawnSeconds : runner.RespawnSeconds;

            var tuning = new TextAsset(json)
            {
                name = copy.name,
                hideFlags = HideFlags.DontSave,
            };
            copy.Tuning = tuning;
            return copy;
        }

        public static LineKind Classify(string line)
        {
            if (line == null)
            {
                return LineKind.Other;
            }

            if (line.StartsWith(_pushingPrefix, StringComparison.Ordinal))
            {
                return LineKind.Pushing;
            }

            if (line.StartsWith(_checkpointPrefix, StringComparison.Ordinal))
            {
                return LineKind.Checkpoint;
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
            _pushes = 0;
            _checkpoints = 0;
            _checkpointLine = null;
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
                case LineKind.Pushing:
                    _pushes++;
                    break;
                case LineKind.Checkpoint:
                    _checkpoints++;
                    _checkpointLine = condition;
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

            // 中継点を越えるたびに時間が延びるので、延びた分も待つ。
            var timeout = WarmUpSeconds + seconds + (_checkpoints * BonusSeconds()) + GraceSeconds;
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

        private static float BonusSeconds()
        {
            var runner = UnityEngine.Object.FindAnyObjectByType<PayloadRunner>();
            return runner != null && runner.Payload != null ? runner.Payload.Spec.CheckpointBonusSeconds : 0f;
        }

        private static string Advance(float seconds)
        {
            var runner = UnityEngine.Object.FindAnyObjectByType<PayloadRunner>();
            if (runner == null)
            {
                return "the scene has no PayloadRunner";
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

        private static string CheckStart(PayloadRunner runner, float seconds)
        {
            var payload = runner.Payload;
            if (!runner.IsStarted || payload == null)
            {
                return "the PayloadRunner did not start a match; see the errors above";
            }

            if (payload.Session.State != GameModeState.Playing)
            {
                return $"the match is {payload.Session.State}, not {GameModeState.Playing}";
            }

            if (!Mathf.Approximately(payload.Spec.TimeLimitSeconds, seconds))
            {
                return $"the time limit is {payload.Spec.TimeLimitSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s, not the {seconds.ToString("0.##", CultureInfo.InvariantCulture)}s set before play";
            }

            if (runner.Cart == null || runner.Cart.Zone == null)
            {
                return $"the PayloadRunner has no cart with a zone; rerun {PayloadSceneBuilder.MenuPath}";
            }

            if (payload.Route.CheckpointCount == 0)
            {
                return $"the route has no checkpoint to reach; rerun {PayloadSceneBuilder.MenuPath}";
            }

            if (CountBots(runner, payload.Attackers) == 0)
            {
                return $"the attackers (side {payload.Attackers.Id}) have no bot to push the cart; rerun {PayloadSceneBuilder.MenuPath}";
            }

            _step = Step.WaitEnd;
            return null;
        }

        private static string WaitEnd(PayloadRunner runner)
        {
            if (!runner.IsMatchOver || _endedLine == null)
            {
                return runner.IsStarted ? null : "the PayloadRunner stopped the match before it ended";
            }

            if (_pushes == 0)
            {
                return $"no bot pushed the cart before the match ended ({_endedLine})";
            }

            if (_checkpoints == 0)
            {
                return $"bots pushed the cart {_pushes} time(s) but it never reached a checkpoint before the match ended ({_endedLine})";
            }

            _step = Step.Done;
            return null;
        }

        private static int CountBots(PayloadRunner runner, Side side)
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
                "{0} step={1} played={2:0.#}s pushes={3} checkpoints={4} last checkpoint={5} ended={6} errors={7}",
                LogTag,
                _step,
                played,
                _pushes,
                _checkpoints,
                _checkpointLine ?? "-",
                _endedLine ?? "-",
                _errors.Count));

            if (problems.Count == 0 && _step == Step.Done)
            {
                Finish(SuccessExitCode, $"passed ({_checkpointLine}; {_endedLine})");
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

            if (SmokeSuite.TakeOver(LogTag, exitCode))
            {
                return;
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

        private static string Number(float value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
