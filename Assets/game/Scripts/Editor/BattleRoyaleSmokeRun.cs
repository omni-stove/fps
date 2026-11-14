using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 武器とルールのアセットとバトルロイヤルのシーン (<see cref="BattleRoyaleSceneBuilder"/>) を生成し直して Play Mode に入り、
    /// エリアの予定を縮めた試合を最後の分隊が決まるまで走らせて、ログで次を確かめて Editor を終える。
    /// <list type="number">
    /// <item>試合が <see cref="BattleRoyaleSceneBuilder.SquadCount"/> 分隊・<see cref="BattleRoyaleSceneBuilder.SquadSize"/> 人ずつで始まり、エリアが動き出す。</item>
    /// <item>bot が武器を拾いに行く (<c>[Mode] errand ... kind=Collect</c>)。</item>
    /// <item>ほかの分隊がすべて脱落し (<c>[Mode] eliminated</c> が分隊の数 - 1 行)、勝った分隊で終わる
    /// (<c>[Mode] ended winner=N</c> と <c>[Mode] placements winner=N</c>。引き分けは失敗)。</item>
    /// </list>
    /// Play 中にエラー・例外・Assert のログが出たら失敗にする。最後に勝った分隊を書いた行を出し、すべて成り立てば終了コード 0、そうでなければ 1 で終わる。
    /// <see cref="SmokeSuite"/> には入れていない (試合が数分かかるので)。Play Mode に入るので <c>-quit</c> は付けずに、Editor を閉じた状態で次のように呼ぶ:
    /// <code>unity -batchmode -projectPath . -executeMethod Fps.Editor.BattleRoyaleSmokeRun.Run -logFile -</code>
    /// </summary>
    /// <remarks>
    /// batchmode には入力が無いので、プレイヤーは出現点に立ったまま (エリアに追われて倒れる) で、戦うのは bot だけになる。
    /// エリアは、開いたシーンの <see cref="ShrinkingZone"/> の待ちと縮みの秒数を Play の前に <see cref="ZoneScaleArgument"/> 倍に縮め
    /// (半径と外のダメージはそのまま)、円の中心がいつも同じになるようシードを <see cref="ZoneSeed"/> に固定する。シーンのファイルは保存しない。
    /// Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    /// </remarks>
    [InitializeOnLoad]
    public static class BattleRoyaleSmokeRun
    {
        public const string LogTag = "[BattleRoyaleSmokeRun]";

        /// <summary>エリアの待ちと縮みの秒数に掛ける倍率を渡す引数。例: <c>-battleRoyaleSmokeZoneScale 0.5</c>。</summary>
        public const string ZoneScaleArgument = "-battleRoyaleSmokeZoneScale";

        /// <summary>
        /// 既定の倍率。予定 (約 6 分) を 2 分足らずにする。最初の待ちが 18 秒残り、出現点の 10m 先の武器は拾える。
        /// 出現点から最後の円まで bot が歩く (約 130m、40 秒ほど) より、予定のほうが長い。
        /// </summary>
        public const float DefaultZoneScale = 0.3f;

        /// <summary>エリアの円の中心を選ぶシード。走らせるたびに同じ円になるよう固定する。</summary>
        public const int ZoneSeed = 351;

        /// <summary>Play Mode に入ってから見始めるまでに待つ秒数。GameBootstrap の Awake と Runner の Start を済ませるため。</summary>
        public const float WarmUpSeconds = 2f;

        /// <summary>エリアが最後の円になってから、最後の分隊が決まるまでに待つ秒数の上限。</summary>
        public const float GraceSeconds = 180f;

        /// <summary>Play Mode に入れないまま、これだけ待ったら失敗にする (秒)。</summary>
        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Fps.BattleRoyaleSmokeRun.Active";
        private const string ScheduleSecondsKey = "Fps.BattleRoyaleSmokeRun.ScheduleSeconds";
        private const string RequestedAtKey = "Fps.BattleRoyaleSmokeRun.RequestedAt";

        private static readonly string _collectSuffix = " kind=" + BotErrandKind.Collect + " ";
        private static readonly string _errandPrefix = BattleRoyaleLog.Tag + " errand ";
        private static readonly string _killPrefix = ModeLog.Tag + " kill ";
        private static readonly string _eliminatedPrefix = BattleRoyaleLog.Tag + " eliminated ";
        private static readonly string _endedPrefix = ModeLog.Tag + " ended winner=";
        private static readonly string _placementsPrefix = BattleRoyaleLog.Tag + " placements winner=";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static int _collectErrands;
        private static int _kills;
        private static int _eliminations;
        private static string _endedLine;
        private static string _placementsLine;
        private static int? _winner;
        private static double _playStartedAt = -1d;
        private static bool _finished;

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

        /// <summary>batchmode から <c>-executeMethod</c> で呼ぶ。終わると Editor を終了コード付きで閉じる。</summary>
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

        /// <summary>
        /// args から <see cref="ZoneScaleArgument"/> の値を読む。無ければ <see cref="DefaultZoneScale"/>。
        /// 値が無い・0 より大きく 1 以下の数でなければ <see cref="ArgumentException"/> を投げる。
        /// </summary>
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

        /// <summary>phases の待ちと縮みの秒数を scale 倍にした並び。半径と外のダメージは変えない。</summary>
        /// <exception cref="ArgumentOutOfRangeException">scale が正の有限値でない。</exception>
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

        /// <summary>phases をすべて終えるまでの秒数 (待ちと縮みの和)。</summary>
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

            if (condition == null)
            {
                return;
            }

            if (condition.StartsWith(_errandPrefix, StringComparison.Ordinal))
            {
                if (condition.Contains(_collectSuffix))
                {
                    _collectErrands++;
                }
            }
            else if (condition.StartsWith(_killPrefix, StringComparison.Ordinal))
            {
                _kills++;
            }
            else if (condition.StartsWith(_eliminatedPrefix, StringComparison.Ordinal))
            {
                _eliminations++;
            }
            else if (condition.StartsWith(_endedPrefix, StringComparison.Ordinal))
            {
                _endedLine = condition;
            }
            else if (condition.StartsWith(_placementsPrefix, StringComparison.Ordinal))
            {
                _placementsLine = condition;
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

        /// <summary>今の手順を 1 つ進める。次のフレームを待つ手順ではそのまま返す。進められない理由があれば返す (null なら問題なし)。</summary>
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
                "{0} step={1} played={2:0.#}s collect errands={3} kills={4} eliminated={5} ended={6} placements={7} errors={8}",
                LogTag,
                _step,
                played,
                _collectErrands,
                _kills,
                _eliminations,
                _endedLine ?? "-",
                _placementsLine ?? "-",
                _errors.Count));

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
