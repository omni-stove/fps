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
    // batchmode には入力が無いので、プレイヤーは出現点に立ったまま (エリアに追われて倒れる) で、戦うのは bot だけになる。
    // 復帰は倒れた段階と残りの回数で戻るかが変わり、誰がいつ倒れるかは撃ち合い次第なので、実際に戻ったかではなく、倒れるたびに戻るかを決めた (復帰が入っている) ことを確かめる。
    // Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class BattleRoyaleSmokeRun
    {
        public const string LogTag = "[BattleRoyaleSmokeRun]";

        public const string ZoneScaleArgument = "-battleRoyaleSmokeZoneScale";

        // 予定 (約 6 分) を 2 分足らずにしても、最初の待ちが 18 秒残って出現点の 10m 先の武器は拾え、
        // 出現点から最後の円まで bot が歩く (約 130m、40 秒ほど) より予定のほうが長い。
        public const float DefaultZoneScale = 0.3f;

        // 走らせるたびに同じ円になるよう、エリアのシードを固定する。
        public const int ZoneSeed = 351;

        // GameBootstrap の Awake と Runner の Start を済ませてから見始める。
        public const float WarmUpSeconds = 2f;

        public const float GraceSeconds = 180f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.BattleRoyaleSmokeRun.Active";
        private const string ScheduleSecondsKey = "Shooter.BattleRoyaleSmokeRun.ScheduleSeconds";
        private const string RequestedAtKey = "Shooter.BattleRoyaleSmokeRun.RequestedAt";

        private static readonly string _collectSuffix = " kind=" + BotErrandKind.Collect + " ";
        private static readonly string _errandPrefix = BattleRoyaleLog.Tag + " errand ";
        private static readonly string _killPrefix = ModeLog.Tag + " kill ";
        private static readonly string _eliminatedPrefix = BattleRoyaleLog.Tag + " eliminated ";
        private static readonly string _endedPrefix = ModeLog.Tag + " ended winner=";
        private static readonly string _placementsPrefix = BattleRoyaleLog.Tag + " placements winner=";
        private static readonly string _redeployPrefix = BattleRoyaleLog.Tag + " redeploy ";
        private static readonly string _redeployWaitingPart = " verdict=" + RedeployVerdict.Waiting + " ";
        private static readonly string _crateDroppedPrefix = SupplyDropLog.Tag + " dropped ";
        private static readonly string _crateLandedPrefix = SupplyDropLog.Tag + " landed ";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static int _collectErrands;
        private static int _kills;
        private static int _eliminations;
        private static int _cratesDropped;
        private static int _cratesLanded;
        private static int _redeployDecisions;
        private static int _redeploysWaiting;
        private static string _endedLine;
        private static string _placementsLine;
        private static int? _winner;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        public enum LineKind
        {
            Other,
            CollectErrand,
            Kill,
            Eliminated,
            Ended,
            Placements,
            CrateDropped,
            CrateLanded,
            RedeployWaiting,
            RedeployRefused,
        }

        private enum Step
        {
            CheckStart,
            WaitEnd,
            Done,
        }

        static BattleRoyaleSmokeRun()
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
                var scale = ParseZoneScale(Environment.GetCommandLineArgs());

                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                BattleRoyaleSceneBuilder.Build();
                var scene = EditorSceneManager.OpenScene(BattleRoyaleSceneBuilder.ScenePathValue, OpenSceneMode.Single);

                var runner = UnityEngine.Object.FindAnyObjectByType<BattleRoyaleRunner>();
                if (runner == null)
                {
                    throw new InvalidOperationException(
                        $"'{BattleRoyaleSceneBuilder.ScenePathValue}' has no BattleRoyaleRunner; rerun {BattleRoyaleSceneBuilder.MenuPath}");
                }

                var zone = runner.Zone;
                if (zone == null)
                {
                    throw new InvalidOperationException(
                        $"the BattleRoyaleRunner in '{BattleRoyaleSceneBuilder.ScenePathValue}' has no ShrinkingZone; rerun {BattleRoyaleSceneBuilder.MenuPath}");
                }

                var phases = ScalePhases(zone.PhaseSettingsList, scale);
                zone.SetPhases(phases);
                zone.UseRandomSeed = false;
                zone.Seed = ZoneSeed;
                EditorSceneManager.MarkSceneDirty(scene);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(ScheduleSecondsKey, TotalSeconds(phases));
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} entering play mode in '{1}' with the zone schedule scaled by {2:0.##} ({3:0.#}s, seed {4})",
                    LogTag,
                    BattleRoyaleSceneBuilder.ScenePathValue,
                    scale,
                    TotalSeconds(phases),
                    ZoneSeed));
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        public static float ParseZoneScale(IReadOnlyList<string> args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], ZoneScaleArgument, StringComparison.Ordinal))
                {
                    continue;
                }

                if (i + 1 >= args.Count
                    || !float.TryParse(args[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var scale)
                    || !(scale > 0f)
                    || !(scale <= 1f))
                {
                    throw new ArgumentException($"{ZoneScaleArgument} needs a number greater than 0 and at most 1", nameof(args));
                }

                return scale;
            }

            return DefaultZoneScale;
        }

        public static IReadOnlyList<ShrinkingZone.PhaseSettings> ScalePhases(IReadOnlyList<ShrinkingZone.PhaseSettings> phases, float scale)
        {
            if (phases == null)
            {
                throw new ArgumentNullException(nameof(phases));
            }

            if (!(scale > 0f) || float.IsInfinity(scale))
            {
                throw new ArgumentOutOfRangeException(nameof(scale), scale, "scale must be positive and finite");
            }

            var scaled = new ShrinkingZone.PhaseSettings[phases.Count];
            for (var i = 0; i < phases.Count; i++)
            {
                var phase = phases[i];
                scaled[i] = new ShrinkingZone.PhaseSettings(phase.WaitSeconds * scale, phase.ShrinkSeconds * scale, phase.Radius, phase.DamagePerSecond);
            }

            return scaled;
        }

        public static float TotalSeconds(IReadOnlyList<ShrinkingZone.PhaseSettings> phases)
        {
            if (phases == null)
            {
                throw new ArgumentNullException(nameof(phases));
            }

            var total = 0f;
            foreach (var phase in phases)
            {
                total += phase.WaitSeconds + phase.ShrinkSeconds;
            }

            return total;
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
            _collectErrands = 0;
            _kills = 0;
            _eliminations = 0;
            _cratesDropped = 0;
            _cratesLanded = 0;
            _redeployDecisions = 0;
            _redeploysWaiting = 0;
            _endedLine = null;
            _placementsLine = null;
            _winner = null;
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
                case LineKind.CollectErrand:
                    _collectErrands++;
                    break;
                case LineKind.Kill:
                    _kills++;
                    break;
                case LineKind.Eliminated:
                    _eliminations++;
                    break;
                case LineKind.Ended:
                    _endedLine = condition;
                    break;
                case LineKind.Placements:
                    _placementsLine = condition;
                    break;
                case LineKind.CrateDropped:
                    _cratesDropped++;
                    break;
                case LineKind.CrateLanded:
                    _cratesLanded++;
                    break;
                case LineKind.RedeployWaiting:
                    _redeployDecisions++;
                    _redeploysWaiting++;
                    break;
                case LineKind.RedeployRefused:
                    _redeployDecisions++;
                    break;
            }
        }

        public static LineKind Classify(string line)
        {
            if (line == null)
            {
                return LineKind.Other;
            }

            if (line.StartsWith(_errandPrefix, StringComparison.Ordinal))
            {
                return line.Contains(_collectSuffix) ? LineKind.CollectErrand : LineKind.Other;
            }

            if (line.StartsWith(_killPrefix, StringComparison.Ordinal))
            {
                return LineKind.Kill;
            }

            if (line.StartsWith(_eliminatedPrefix, StringComparison.Ordinal))
            {
                return LineKind.Eliminated;
            }

            if (line.StartsWith(_endedPrefix, StringComparison.Ordinal))
            {
                return LineKind.Ended;
            }

            if (line.StartsWith(_placementsPrefix, StringComparison.Ordinal))
            {
                return LineKind.Placements;
            }

            if (line.StartsWith(_crateDroppedPrefix, StringComparison.Ordinal))
            {
                return LineKind.CrateDropped;
            }

            if (line.StartsWith(_crateLandedPrefix, StringComparison.Ordinal))
            {
                return LineKind.CrateLanded;
            }

            if (line.StartsWith(_redeployPrefix, StringComparison.Ordinal))
            {
                return line.Contains(_redeployWaitingPart) ? LineKind.RedeployWaiting : LineKind.RedeployRefused;
            }

            return LineKind.Other;
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

            var timeout = WarmUpSeconds + SessionState.GetFloat(ScheduleSecondsKey, 0f) + GraceSeconds;
            if (problem == null && _step != Step.Done && now - _playStartedAt > timeout)
            {
                problem = $"step {_step} did not finish within {timeout.ToString("0.#", CultureInfo.InvariantCulture)}s of play";
            }

            if (problem == null && _step != Step.Done && _errors.Count == 0)
            {
                return;
            }

            Report(problem);
        }

        private static string Advance()
        {
            var runner = UnityEngine.Object.FindAnyObjectByType<BattleRoyaleRunner>();
            if (runner == null)
            {
                return "the scene has no BattleRoyaleRunner";
            }

            switch (_step)
            {
                case Step.CheckStart:
                    return CheckStart(runner);

                case Step.WaitEnd:
                    return WaitEnd(runner);

                default:
                    return null;
            }
        }

        private static string CheckStart(BattleRoyaleRunner runner)
        {
            var battleRoyale = runner.BattleRoyale;
            if (!runner.IsStarted || battleRoyale == null)
            {
                return "the BattleRoyaleRunner did not start a match; see the errors above";
            }

            if (!battleRoyale.IsLive)
            {
                return $"the match is {battleRoyale.Mode.State}, not {GameModeState.Playing}";
            }

            if (battleRoyale.EntrantCount != BattleRoyaleSceneBuilder.SquadCount)
            {
                return $"{battleRoyale.EntrantCount} squad(s) entered, expected {BattleRoyaleSceneBuilder.SquadCount}; rerun {BattleRoyaleSceneBuilder.MenuPath}";
            }

            var expectedMembers = BattleRoyaleSceneBuilder.SquadCount * BattleRoyaleSceneBuilder.SquadSize;
            if (runner.Members.Count != expectedMembers)
            {
                return $"{runner.Members.Count} player(s) joined, expected {expectedMembers}; rerun {BattleRoyaleSceneBuilder.MenuPath}";
            }

            if (runner.RunningZone == null || !runner.RunningZone.IsRunning)
            {
                return "the shrinking zone did not start";
            }

            if (runner.RunningSupplyDropper == null)
            {
                return $"the supply drops did not start; rerun {BattleRoyaleSceneBuilder.MenuPath}";
            }

            if (!battleRoyale.RedeployRules.IsEnabled)
            {
                return $"redeploys are off (charges {battleRoyale.RedeployRules.Charges}); check Resources/Tuning/redeploy.json or the BattleRoyaleDefinition";
            }

            _step = Step.WaitEnd;
            return null;
        }

        private static string WaitEnd(BattleRoyaleRunner runner)
        {
            var battleRoyale = runner.BattleRoyale;
            if (battleRoyale == null)
            {
                return "the BattleRoyaleRunner stopped the match before it ended";
            }

            // 終わりの行と順位の行は同じフレームに出るが、両方を受け取ってから判定する。
            if (!runner.IsMatchOver || _endedLine == null || _placementsLine == null)
            {
                return null;
            }

            var winner = battleRoyale.Mode.Winner;
            if (!winner.HasValue)
            {
                return $"the match ended in a draw ({_placementsLine})";
            }

            _winner = winner.Value.Id;
            if (_eliminations != BattleRoyaleSceneBuilder.SquadCount - 1)
            {
                return $"{_eliminations} squad(s) were eliminated before the end, expected {BattleRoyaleSceneBuilder.SquadCount - 1} ({_placementsLine})";
            }

            if (_collectErrands == 0)
            {
                return "no bot went to collect a pickup";
            }

            if (_cratesDropped == 0 || _cratesLanded == 0)
            {
                return $"{_cratesDropped} supply crate(s) dropped and {_cratesLanded} landed before the end; expected at least one of each";
            }

            if (_redeployDecisions == 0)
            {
                return $"nobody who fell was considered for a redeploy ({_kills} kill(s))";
            }

            _step = Step.Done;
            return null;
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
                "{0} step={1} played={2:0.#}s collect errands={3} kills={4} eliminated={5} crates dropped={9} landed={10} redeploys decided={11} waiting={12} ended={6} placements={7} errors={8}",
                LogTag,
                _step,
                played,
                _collectErrands,
                _kills,
                _eliminations,
                _endedLine ?? "-",
                _placementsLine ?? "-",
                _errors.Count,
                _cratesDropped,
                _cratesLanded,
                _redeployDecisions,
                _redeploysWaiting));

            if (problems.Count == 0 && _step == Step.Done && _winner.HasValue)
            {
                Finish(SuccessExitCode, $"passed: squad {_winner.Value.ToString(CultureInfo.InvariantCulture)} won ({_placementsLine})");
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
            SessionState.EraseFloat(ScheduleSecondsKey);
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
