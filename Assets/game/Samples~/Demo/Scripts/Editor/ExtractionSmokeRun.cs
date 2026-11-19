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
    // batchmode には入力が無いので、箱と脱出地点へは歩かせずに移し、箱は Interact の代わりに ExtractionRunner.TryLoot で開ける。
    // 脱出すると持ち物庫 (PlayerPrefs) に書くので、走らせる前の中身を SessionState に控えて空から始め、終わったら戻す。
    // 開く地点は種で決まるので、種を決めて Play ごとに同じ地点が開くようにする。
    // Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class ExtractionSmokeRun
    {
        public const string LogTag = "[ExtractionSmokeRun]";

        public const string SecondsArgument = "-extractionSmokeSeconds";

        // 待ちが長いとスイート全体が延びるので、地点が開くまでと立ち続ける秒数を縮める。
        public const float OpenAfterSeconds = 2f;
        public const float ExtractHoldSeconds = 2f;

        public const float DefaultSeconds = 60f;

        // 地点が開いてから立ち続けて脱出するまでより短いと、時間切れで終わる。
        public const float MinimumSeconds = OpenAfterSeconds + ExtractHoldSeconds + 5f;

        public const int Seed = 1;

        public const int ContainerIndex = 0;

        // 箱 (LootContainer.DefaultVisualSize) の手前に、手の届く (LootContainer.DefaultReach) 所へ立たせる。
        public static readonly Vector3 LootStandOffset = new Vector3(0f, 0f, -1.1f);

        // GameBootstrap の Awake と ExtractionRunner の Start を済ませてから見始める。
        public const float WarmUpSeconds = 1f;

        public const float GraceSeconds = 30f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.ExtractionSmokeRun.Active";
        private const string SecondsKey = "Shooter.ExtractionSmokeRun.Seconds";
        private const string RequestedAtKey = "Shooter.ExtractionSmokeRun.RequestedAt";
        private const string HadStashKey = "Shooter.ExtractionSmokeRun.HadStash";
        private const string StashKey = "Shooter.ExtractionSmokeRun.Stash";

        private static readonly string _startedPrefix = ExtractionLog.Tag + " started points=";
        private static readonly string _openedPrefix = LootLog.Tag + " opened container=";
        private static readonly string _extractedPrefix = ExtractionLog.Tag + " ended outcome=" + ExtractionOutcome.Extracted + " ";
        private static readonly string _raidEndedPrefix = ExtractionLog.Tag + " ended outcome=";
        private static readonly string _modeEndedPrefix = ModeLog.Tag + " ended winner=";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static string _startedLine;
        private static string _openedLine;
        private static string _raidEndedLine;
        private static string _modeEndedLine;
        private static string _exitId;
        private static int _carried;
        private static int _stored;
        private static int _teleports;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        public enum LineKind
        {
            Other,
            Started,
            Opened,
            Extracted,
            RaidEnded,
            ModeEnded,
        }

        private enum Step
        {
            CheckStart,
            Loot,
            WaitOpen,
            Hold,
            Done,
        }

        static ExtractionSmokeRun()
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
                ExtractionSceneBuilder.Build();
                var scene = EditorSceneManager.OpenScene(ExtractionSceneBuilder.ScenePathValue, OpenSceneMode.Single);

                var runner = UnityEngine.Object.FindAnyObjectByType<ExtractionRunner>();
                if (runner == null)
                {
                    throw new InvalidOperationException(
                        $"'{ExtractionSceneBuilder.ScenePathValue}' has no ExtractionRunner; rerun {ExtractionSceneBuilder.MenuPath}");
                }

                // 調整値のアセットを書き換えると保存しなくてもメモリ上で後の実行に残るので、複製を縮めて配線する。
                runner.Definition = Shorten(runner.Definition, seconds);
                runner.Seed = Seed;
                EditorSceneManager.MarkSceneDirty(scene);

                ResetObservations();
                SetAsideStash();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(SecondsKey, seconds);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log(
                    $"{LogTag} entering play mode in '{ExtractionSceneBuilder.ScenePathValue}' with a {seconds.ToString("0.##", CultureInfo.InvariantCulture)}s raid");
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

        public static ExtractionDefinition Shorten(ExtractionDefinition source, float seconds)
        {
            ExtractionDefinition copy;
            if (source != null)
            {
                copy = UnityEngine.Object.Instantiate(source);
            }
            else
            {
                copy = ScriptableObject.CreateInstance<ExtractionDefinition>();
                copy.CopyFrom(ExtractionSceneBuilder.DemoSpec());
            }

            copy.name = (source != null ? source.name : nameof(ExtractionDefinition)) + " (smoke)";
            copy.hideFlags = HideFlags.DontSave;
            copy.RaidSeconds = seconds;
            copy.OpenAfterSeconds = OpenAfterSeconds;
            copy.ExtractHoldSeconds = ExtractHoldSeconds;
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

            if (line.StartsWith(_startedPrefix, StringComparison.Ordinal))
            {
                return LineKind.Started;
            }

            if (line.StartsWith(_extractedPrefix, StringComparison.Ordinal))
            {
                return LineKind.Extracted;
            }

            if (line.StartsWith(_raidEndedPrefix, StringComparison.Ordinal))
            {
                return LineKind.RaidEnded;
            }

            if (line.StartsWith(_modeEndedPrefix, StringComparison.Ordinal))
            {
                return LineKind.ModeEnded;
            }

            return line.StartsWith(_openedPrefix, StringComparison.Ordinal) ? LineKind.Opened : LineKind.Other;
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
            _startedLine = null;
            _openedLine = null;
            _raidEndedLine = null;
            _modeEndedLine = null;
            _exitId = null;
            _carried = 0;
            _stored = 0;
            _teleports = 0;
            _playStartedAt = -1d;
            _finished = false;
        }

        private static void SetAsideStash()
        {
            var had = PlayerPrefs.HasKey(StashStore.DefaultKey);
            SessionState.SetBool(HadStashKey, had);
            SessionState.SetString(StashKey, had ? PlayerPrefs.GetString(StashStore.DefaultKey, string.Empty) : string.Empty);
            PlayerPrefs.DeleteKey(StashStore.DefaultKey);
            PlayerPrefs.Save();
        }

        private static void RestoreStash()
        {
            if (!SessionState.GetBool(ActiveKey, false))
            {
                return;
            }

            if (SessionState.GetBool(HadStashKey, false))
            {
                PlayerPrefs.SetString(StashStore.DefaultKey, SessionState.GetString(StashKey, string.Empty));
            }
            else
            {
                PlayerPrefs.DeleteKey(StashStore.DefaultKey);
            }

            PlayerPrefs.Save();
            SessionState.EraseBool(HadStashKey);
            SessionState.EraseString(StashKey);
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
                case LineKind.Started:
                    _startedLine = condition;
                    break;
                case LineKind.Opened:
                    _openedLine = condition;
                    break;
                case LineKind.Extracted:
                case LineKind.RaidEnded:
                    _raidEndedLine = condition;
                    break;
                case LineKind.ModeEnded:
                    _modeEndedLine = condition;
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
                problem = Advance();
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

        private static string Advance()
        {
            var runner = UnityEngine.Object.FindAnyObjectByType<ExtractionRunner>();
            if (runner == null)
            {
                return "the scene has no ExtractionRunner";
            }

            var raid = runner.Extraction;
            if (_step != Step.CheckStart && _step != Step.Done && raid != null && raid.IsOver && raid.Outcome != ExtractionOutcome.Extracted)
            {
                return $"the raid ended with {raid.Outcome} at step {_step} ({_raidEndedLine ?? "-"})";
            }

            switch (_step)
            {
                case Step.CheckStart:
                    return CheckStart(runner);

                case Step.Loot:
                    return Loot(runner);

                case Step.WaitOpen:
                    return WaitOpen(runner);

                case Step.Hold:
                    return Hold(runner);

                default:
                    return null;
            }
        }

        private static string CheckStart(ExtractionRunner runner)
        {
            if (!runner.IsStarted || runner.Extraction == null || _startedLine == null)
            {
                return "the ExtractionRunner did not start a raid; see the errors above";
            }

            if (runner.LocalPlayer == null)
            {
                return "the raid has no player";
            }

            if (runner.Containers.Count != ExtractionSceneBuilder.Containers.Count)
            {
                return $"the raid has {runner.Containers.Count} loot container(s), expected {ExtractionSceneBuilder.Containers.Count}; rerun {ExtractionSceneBuilder.MenuPath}";
            }

            if (runner.Zones.Count != ExtractionSceneBuilder.Exits.Count)
            {
                return $"the raid has {runner.Zones.Count} exit(s), expected {ExtractionSceneBuilder.Exits.Count}; rerun {ExtractionSceneBuilder.MenuPath}";
            }

            if (runner.Extraction.OpenPointIds.Count != ExtractionSceneBuilder.OpenPoints)
            {
                return $"the raid opens {runner.Extraction.OpenPointIds.Count} exit(s), expected {ExtractionSceneBuilder.OpenPoints}";
            }

            _step = Step.Loot;
            return null;
        }

        private static string Loot(ExtractionRunner runner)
        {
            var container = runner.Containers[ContainerIndex];
            var player = runner.LocalPlayer;
            if (!player.IsAlive)
            {
                return "the player was killed before reaching the loot";
            }

            if (!container.IsInReach(player.GroundPosition))
            {
                MovePlayer(container.transform.position + LootStandOffset);
                return null;
            }

            if (!runner.TryLoot())
            {
                return $"the player stood by '{container.name}' but could not open it";
            }

            if (!container.IsOpened || _openedLine == null)
            {
                return $"'{container.name}' did not open";
            }

            _carried = Total(runner.Extraction.Carried);
            if (_carried == 0)
            {
                return $"'{container.name}' gave nothing ({_openedLine})";
            }

            _step = Step.WaitOpen;
            return null;
        }

        private static string WaitOpen(ExtractionRunner runner)
        {
            var raid = runner.Extraction;
            if (!raid.PointsOpen)
            {
                return null;
            }

            foreach (var zone in runner.Zones)
            {
                if (raid.IsOpen(zone.PointId) && zone.IsOpen)
                {
                    _exitId = zone.PointId;
                    _step = Step.Hold;
                    return null;
                }
            }

            // 開いた Tick と見た目を替える Tick がずれても落とさないよう、開いてから 1 秒は待つ。
            if (raid.ElapsedSeconds < raid.Spec.OpenAfterSeconds + 1f)
            {
                return null;
            }

            return $"the exits opened, but no ExtractionZone of {string.Join(",", raid.OpenPointIds)} shows open";
        }

        private static string Hold(ExtractionRunner runner)
        {
            var raid = runner.Extraction;
            if (raid.IsOver)
            {
                if (_raidEndedLine == null || !_raidEndedLine.StartsWith(_extractedPrefix, StringComparison.Ordinal) || _modeEndedLine == null)
                {
                    return null;
                }

                if (!runner.IsMatchOver)
                {
                    return "the raid ended but the match did not";
                }

                if (!string.Equals(_modeEndedLine, ModeLog.Ended(runner.PlayerSide), StringComparison.Ordinal))
                {
                    return $"the player extracted but the match did not end as the player's win ({_modeEndedLine})";
                }

                _stored = Total(raid.Stored);
                if (_stored < _carried)
                {
                    return $"the player carried {_carried} item(s) out but only {_stored} were stored ({_raidEndedLine})";
                }

                _step = Step.Done;
                return null;
            }

            var player = runner.LocalPlayer;
            if (!player.IsAlive)
            {
                return "the player was killed before extracting";
            }

            ExtractionZone zone = null;
            foreach (var candidate in runner.Zones)
            {
                if (candidate != null && string.Equals(candidate.PointId, _exitId, StringComparison.Ordinal))
                {
                    zone = candidate;
                    break;
                }
            }

            if (zone == null)
            {
                return $"the exit '{_exitId}' is gone";
            }

            if (!zone.Contains(player.GroundPosition))
            {
                MovePlayer(zone.transform.position);
            }

            return null;
        }

        private static int Total(IReadOnlyList<ItemCount> items)
        {
            var total = 0;
            foreach (var item in items)
            {
                total += item.Count;
            }

            return total;
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void MovePlayer(Vector3 feet)
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            var controller = bootstrap != null && bootstrap.World != null ? bootstrap.World.Controller : null;
            if (controller == null)
            {
                throw new InvalidOperationException("the scene has no GameBootstrap that built the player");
            }

            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = feet + GameBootstrap.PlayerSpawn;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
            _teleports++;
        }

        private static void Report(string problem)
        {
            var problems = new List<string>();
            if (problem != null)
            {
                problems.Add(problem);
            }

            problems.AddRange(_errors);
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} step={1} started={2} opened={3} exit={4} carried={5} stored={6} ended={7} match={8} teleports={9} errors={10}",
                LogTag,
                _step,
                _startedLine ?? "-",
                _openedLine ?? "-",
                _exitId ?? "-",
                _carried,
                _stored,
                _raidEndedLine ?? "-",
                _modeEndedLine ?? "-",
                _teleports,
                _errors.Count));

            var passed = problems.Count == 0 && _step == Step.Done && _openedLine != null && _modeEndedLine != null;
            if (passed)
            {
                Finish(SuccessExitCode, "passed");
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
            RestoreStash();
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
    }
}
