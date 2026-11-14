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
    /// 武器とルールのアセットと Domination シーン (<see cref="DominationSceneBuilder"/>) を生成し直して Play Mode に入り、
    /// 制限時間を縮めた試合を走らせて、ログで次を確かめて Editor を終える。
    /// <list type="number">
    /// <item>試合が始まる (<c>[Domination] state=Playing</c>)。</item>
    /// <item>プレイヤーが拠点 A に立ち続けて占領し (<c>[Domination] captured point=A team=Alpha</c>)、Alpha にポイントが入る。</item>
    /// <item>時間かスコア上限で試合が終わる (<c>[Domination] ended winner=...</c>)。</item>
    /// <item>再開すると (<c>[Domination] restart</c>) シーンが読み直され、新しい試合が始まる。</item>
    /// </list>
    /// Play 中にエラー・例外・Assert のログが出たら失敗にする。敵が拠点を取ったか (<c>team=Bravo</c>) は結果の行に数を出すが、
    /// 敵がプレイヤーとの撃ち合いに入るかどうかで変わるので合否には使わない。
    /// すべて成り立てば終了コード 0、そうでなければ 1 で終わる。試合の制限時間は <see cref="SecondsArgument"/> で変えられる。
    /// <see cref="SmokeSuite"/> には入れていない。Play Mode に入るので <c>-quit</c> は付けずに、Editor を閉じた状態で次のように呼ぶ:
    /// <code>unity -batchmode -projectPath . -executeMethod Fps.Editor.DominationSmokeRun.Run -logFile -</code>
    /// </summary>
    /// <remarks>
    /// 制限時間は、開いたシーンの <see cref="DominationRunner.TimeLimitSeconds"/> を Play の前に書き換えて縮める (シーンのファイルは保存しない)。
    /// 調整値のアセット (<see cref="DominationRunner.Definition"/>) が配線されていれば、その値を Runner に写して外してから縮める。
    /// batchmode には入力が無いので、拠点 A へは歩かせずに移し、倒れて戻されたら生き返った後にまた移す。
    /// 再開も R の入力の代わりに <see cref="DominationRunner.Restart"/> を呼ぶ (R から Restart へつなぐのは DominationRunner の Update)。
    /// Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    /// </remarks>
    [InitializeOnLoad]
    public static class DominationSmokeRun
    {
        public const string LogTag = "[DominationSmokeRun]";

        /// <summary>試合の制限時間 (秒) を渡す引数。例: <c>-dominationSmokeSeconds 60</c>。</summary>
        public const string SecondsArgument = "-dominationSmokeSeconds";

        /// <summary>
        /// 既定の試合の制限時間 (秒)。拠点 A を 1 人で占領し終える <see cref="DominationRunner.DefaultCaptureSeconds"/> 秒に、
        /// 倒されて戻ってくる分を何度か足しても収まる長さ。
        /// </summary>
        public const float DefaultSeconds = 40f;

        /// <summary>Play Mode に入ってから見始めるまでに待つ秒数。GameBootstrap の Awake と DominationRunner の Start を済ませるため。</summary>
        public const float WarmUpSeconds = 1f;

        /// <summary>制限時間を過ぎてから、終了と再開が済むまでに待つ秒数の上限。</summary>
        public const float GraceSeconds = 30f;

        /// <summary>Play Mode に入れないまま、これだけ待ったら失敗にする (秒)。</summary>
        public const float EnterTimeoutSeconds = 120f;

        /// <summary>プレイヤーを移す拠点の番号 (A)。Alpha の出現点にいちばん近い。</summary>
        public const int PlayerPointIndex = 0;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Fps.DominationSmokeRun.Active";
        private const string SecondsKey = "Fps.DominationSmokeRun.Seconds";
        private const string RequestedAtKey = "Fps.DominationSmokeRun.RequestedAt";

        private static readonly string _playingLog = DominationLog.Tag + " state=" + GameModeState.Playing + " ";
        private static readonly string _endedLog = DominationLog.Tag + " ended winner=";
        private static readonly string _restartLog = DominationLog.Restart();
        private static readonly string _alphaCaptureSuffix = " team=" + Team.Alpha;
        private static readonly string _bravoCaptureSuffix = " team=" + Team.Bravo;
        private static readonly string _capturedPrefix = DominationLog.Tag + " captured point=";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static int _playingLogs;
        private static bool _sawPlayerPointCapture;
        private static int _alphaCaptures;
        private static int _bravoCaptures;
        private static bool _sawEnded;
        private static bool _sawRestart;
        private static float _alphaScore;
        private static string _endedLine;
        private static EntityId _firstRunnerId;
        private static int _teleports;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        private enum Step
        {
            CheckStart,
            HoldPoint,
            WaitRestart,
            Done,
        }

        static DominationSmokeRun()
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
                var seconds = ParseSeconds(Environment.GetCommandLineArgs());

                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                DominationSceneBuilder.Build();
                var scene = EditorSceneManager.OpenScene(DominationSceneBuilder.ScenePath, OpenSceneMode.Single);

                var runner = UnityEngine.Object.FindAnyObjectByType<DominationRunner>();
                if (runner == null)
                {
                    throw new InvalidOperationException($"'{DominationSceneBuilder.ScenePath}' has no DominationRunner; rerun {DominationSceneBuilder.MenuPath}");
                }

                // 調整値のアセットを書き換えると保存しなくてもメモリ上で後の実行に残るので、値を Runner に写してアセットを外し、Runner の値だけを縮める。
                if (runner.Definition != null)
                {
                    runner.CopyFrom(runner.Definition);
                    runner.Definition = null;
                }

                runner.TimeLimitSeconds = seconds;
                EditorSceneManager.MarkSceneDirty(scene);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(SecondsKey, seconds);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log(
                    $"{LogTag} entering play mode in '{DominationSceneBuilder.ScenePath}' with a {seconds.ToString("0.##", CultureInfo.InvariantCulture)}s time limit");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
        }

        /// <summary>
        /// args から <see cref="SecondsArgument"/> の値を読む。無ければ <see cref="DefaultSeconds"/>。
        /// 値が無い・拠点を占領し終える <see cref="DominationRunner.DefaultCaptureSeconds"/> 秒より長い有限な数でなければ
        /// <see cref="ArgumentException"/> を投げる。
        /// </summary>
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
                    || !(seconds > DominationRunner.DefaultCaptureSeconds)
                    || float.IsInfinity(seconds))
                {
                    throw new ArgumentException(
                        $"{SecondsArgument} needs a finite number of seconds longer than the capture time ({DominationRunner.DefaultCaptureSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s)",
                        nameof(args));
                }

                return seconds;
            }

            return DefaultSeconds;
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
            _playingLogs = 0;
            _sawPlayerPointCapture = false;
            _alphaCaptures = 0;
            _bravoCaptures = 0;
            _sawEnded = false;
            _sawRestart = false;
            _alphaScore = 0f;
            _endedLine = null;
            _firstRunnerId = EntityId.None;
            _teleports = 0;
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

            if (condition.StartsWith(_playingLog, StringComparison.Ordinal))
            {
                _playingLogs++;
            }
            else if (condition.StartsWith(_capturedPrefix, StringComparison.Ordinal))
            {
                if (condition.EndsWith(_alphaCaptureSuffix, StringComparison.Ordinal))
                {
                    _alphaCaptures++;
                    if (string.Equals(condition, DominationLog.Captured(PlayerPointIndex, Team.Alpha), StringComparison.Ordinal))
                    {
                        _sawPlayerPointCapture = true;
                    }
                }
                else if (condition.EndsWith(_bravoCaptureSuffix, StringComparison.Ordinal))
                {
                    _bravoCaptures++;
                }
            }
            else if (condition.StartsWith(_endedLog, StringComparison.Ordinal))
            {
                _sawEnded = true;
                _endedLine = condition;
            }
            else if (string.Equals(condition, _restartLog, StringComparison.Ordinal))
            {
                _sawRestart = true;
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

        /// <summary>今の手順を 1 つ進める。次のフレームを待つ手順ではそのまま返す。進められない理由があれば返す (null なら問題なし)。</summary>
        private static string Advance(float seconds)
        {
            var runner = UnityEngine.Object.FindAnyObjectByType<DominationRunner>();
            if (runner == null)
            {
                // 再開でシーンを読み直している間は、Runner がいなくなるフレームがある。
                return _step == Step.WaitRestart ? null : "the scene has no DominationRunner";
            }

            switch (_step)
            {
                case Step.CheckStart:
                    return CheckStart(runner, seconds);

                case Step.HoldPoint:
                    return HoldPoint(runner);

                case Step.WaitRestart:
                    return WaitRestart(runner);

                default:
                    return null;
            }
        }

        private static string CheckStart(DominationRunner runner, float seconds)
        {
            if (!runner.IsStarted)
            {
                return "the DominationRunner did not start a match; see the errors above";
            }

            if (runner.Domination.State != GameModeState.Playing || _playingLogs == 0)
            {
                return $"the match is {runner.Domination.State} without a '{_playingLog.TrimEnd()}' line";
            }

            if (!Mathf.Approximately(runner.TimeLimitSeconds, seconds))
            {
                return $"the time limit is {runner.TimeLimitSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s, not the {seconds.ToString("0.##", CultureInfo.InvariantCulture)}s set before play";
            }

            if (runner.Zones.Count != DominationSceneBuilder.ControlPoints.Count)
            {
                return $"the match has {runner.Zones.Count} control point(s), expected {DominationSceneBuilder.ControlPoints.Count}; rerun {DominationSceneBuilder.MenuPath}";
            }

            _firstRunnerId = runner.GetEntityId();
            _step = Step.HoldPoint;
            return null;
        }

        private static string HoldPoint(DominationRunner runner)
        {
            var domination = runner.Domination;
            if (domination == null)
            {
                return "the DominationRunner stopped the match before it ended";
            }

            if (domination.State == GameModeState.Ended)
            {
                if (!_sawEnded)
                {
                    return null;
                }

                _alphaScore = domination.GetScore(Team.Alpha);
                if (!_sawPlayerPointCapture)
                {
                    return $"the player stood in point {DominationHudFormat.PointName(PlayerPointIndex)} but never captured it ({_endedLine})";
                }

                if (!(_alphaScore > 0f))
                {
                    return $"Alpha captured a point but scored nothing ({_endedLine})";
                }

                runner.Restart();
                _step = Step.WaitRestart;
                return null;
            }

            if (domination.State != GameModeState.Playing)
            {
                return $"the match went to {domination.State} before it ended";
            }

            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            var world = bootstrap != null ? bootstrap.World : null;
            if (world == null || world.PlayerDamageable == null || world.Controller == null)
            {
                return "the scene has no GameBootstrap that built the player";
            }

            // 倒れている間は Runner が出現点へ戻すのを待ち、生き返ってから拠点に移す。
            var zone = runner.Zones[PlayerPointIndex];
            if (!world.PlayerDamageable.IsDead && !zone.Contains(world.Player.transform.position))
            {
                Teleport(world.Controller, zone.transform.position + GameBootstrap.PlayerSpawn);
                _teleports++;
            }

            return null;
        }

        private static string WaitRestart(DominationRunner runner)
        {
            if (runner.GetEntityId() == _firstRunnerId || !runner.IsStarted)
            {
                return null;
            }

            if (!_sawRestart)
            {
                return $"the scene was reloaded without a '{_restartLog}' line";
            }

            if (runner.Domination.State != GameModeState.Playing || _playingLogs < 2)
            {
                return null;
            }

            _step = Step.Done;
            return null;
        }

        // CharacterController は有効なままだと Transform の書き換えを次の移動で上書きするので、移す間だけ切る。
        private static void Teleport(CharacterController controller, Vector3 position)
        {
            var wasEnabled = controller.enabled;
            controller.enabled = false;
            controller.transform.position = position;
            controller.enabled = wasEnabled;
            Physics.SyncTransforms();
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
                "{0} step={1} playing={2} alpha captures={3} (point {4}={5}) bravo captures={6} ended={7} alpha score={8:0.#} restart={9} teleports={10} errors={11}",
                LogTag,
                _step,
                _playingLogs,
                _alphaCaptures,
                DominationHudFormat.PointName(PlayerPointIndex),
                _sawPlayerPointCapture,
                _bravoCaptures,
                _endedLine ?? "-",
                _alphaScore,
                _sawRestart,
                _teleports,
                _errors.Count));

            var passed = problems.Count == 0 && _step == Step.Done && _sawPlayerPointCapture && _sawEnded && _sawRestart;
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
