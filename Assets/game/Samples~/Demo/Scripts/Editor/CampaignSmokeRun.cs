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
    // セーブ無しから始めるため、この Editor の Application.persistentDataPath にある各ミッションのスロット
    // (CampaignSaveSlots.For) のセーブを走らせる前に消す。
    // 走っている間に開始地点のチェックポイントでセーブされるので、終えるときにもう一度消す。
    // Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態 (何本目のミッションか・見つけた問題) は SessionState に置く。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class CampaignSmokeRun
    {
        public const string LogTag = "[CampaignSmokeRun]";

        public const string SecondsArgument = "-campaignSmokeSeconds";

        public const float DefaultSeconds = 5f;

        // ダミーの復活 (3 秒) で抜け殻が消えるところまで既定の秒数に収まるよう、早めに倒す。
        public const float KillDummyAfterSeconds = 0.5f;

        // ダミーの抜け殻を確かめてから、敵の抜け殻と混ざらないように目標を進め始める。
        public const float WalkthroughAfterSeconds = 1f;

        public const float WalkthroughStepTimeoutSeconds = 2f;

        // 抜け殻がダミーの後ろへ倒れるよう、正面から撃ったことにする。
        private const float KillSourceDistance = 5f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.CampaignSmokeRun.Active";
        private const string SecondsKey = "Shooter.CampaignSmokeRun.Seconds";
        private const string RequestedAtKey = "Shooter.CampaignSmokeRun.RequestedAt";
        private const string MissionIndexKey = "Shooter.CampaignSmokeRun.MissionIndex";
        private const string PhaseKey = "Shooter.CampaignSmokeRun.Phase";
        private const string ProblemsKey = "Shooter.CampaignSmokeRun.Problems";

        // 問題の文に改行は入れないので、SessionState では改行で区切る。
        private const char ProblemSeparator = '\n';

        private const int PhaseEntering = 0;
        private const int PhasePlaying = 1;
        private const int PhaseLeaving = 2;

        private static readonly List<string> _errors = new List<string>();
        private static bool _missionStarted;
        private static bool _startedWithoutSave;
        private static bool _savedAtStart;
        private static bool _killedDummy;
        private static bool _deathBodyCollapsed;
        private static bool _deathBodyRemoved;
        private static bool _ragdollCollapsed;
        private static bool _ragdollFrozen;
        private static string _killProblem;
        private static bool _walkStarted;
        private static bool _collapsedBeforeWalk;
        private static int _walkStep;
        private static double _walkStepStartedAt = -1d;
        private static string _walkProblem;
        private static bool _missionCompleted;
        private static double _playStartedAt = -1d;
        private static bool _finished;
        private static CampaignMissionSpec _currentMission;
        private static int _currentMissionIndex = -1;

        static CampaignSmokeRun()
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

                // シーンはプレイヤーに既定のロードアウト (rifle, handgun) を、敵に武器の一覧から引いた武器を、
                // GameBootstrap と CampaignRunner に難易度別のルールを配線するので、先に武器とルールのアセットを作り直しておく。
                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                CampaignSceneBuilder.Build();
                DeleteSaves("before the run");

                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(SecondsKey, seconds);
                SessionState.SetString(ProblemsKey, string.Empty);
                Attach();
                StartMission(0);
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
                    || !(seconds > 0f)
                    || float.IsInfinity(seconds))
                {
                    throw new ArgumentException($"{SecondsArgument} needs a positive number of seconds", nameof(args));
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

        // ログのたびに配置を組み立て直さないよう、何本目かが変わるまで持っておく。
        private static CampaignMissionSpec CurrentMission()
        {
            var index = SessionState.GetInt(MissionIndexKey, 0);
            if (_currentMission != null && _currentMissionIndex == index)
            {
                return _currentMission;
            }

            var missions = CampaignSceneBuilder.Missions();
            if (index < 0 || index >= missions.Count)
            {
                throw new InvalidOperationException($"mission index {index} is out of range (0-{missions.Count - 1})");
            }

            _currentMission = missions[index];
            _currentMissionIndex = index;
            return _currentMission;
        }

        private static void StartMission(int index)
        {
            SessionState.SetInt(MissionIndexKey, index);
            SessionState.SetInt(PhaseKey, PhaseEntering);
            SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
            ResetObservations();

            var mission = CurrentMission();
            EditorSceneManager.OpenScene(mission.ScenePath, OpenSceneMode.Single);
            Debug.Log(
                $"{LogTag} entering play mode for mission={mission.Id} in '{mission.ScenePath}' for {SessionState.GetFloat(SecondsKey, DefaultSeconds).ToString("0.##", CultureInfo.InvariantCulture)}s");
            EditorApplication.EnterPlaymode();
        }

        private static void ResetObservations()
        {
            _errors.Clear();
            _missionStarted = false;
            _startedWithoutSave = false;
            _savedAtStart = false;
            _killedDummy = false;
            _deathBodyCollapsed = false;
            _deathBodyRemoved = false;
            _ragdollCollapsed = false;
            _ragdollFrozen = false;
            _killProblem = null;
            _walkStarted = false;
            _collapsedBeforeWalk = false;
            _walkStep = 0;
            _walkStepStartedAt = -1d;
            _walkProblem = null;
            _missionCompleted = false;
            _playStartedAt = -1d;
            _finished = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            // Play Mode に入る前 (シーンの生成など) と、判定を終えて抜けるまでの間のログは判定に使わない。
            if (_finished || !EditorApplication.isPlaying || SessionState.GetInt(PhaseKey, PhaseEntering) == PhaseLeaving)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                // Unity 6000.6.0f1 の batchmode では、Editor の起動時インデックス (SearchInit.IndexationOnStartup) が
                // Play 中に例外を投げることがある。ゲームのコードではないので、これだけは判定から外す。
                if (stackTrace != null && stackTrace.Contains("UnityEditor.Search.SearchInit.IndexationOnStartup"))
                {
                    return;
                }

                _errors.Add($"{type}: {OneLine(condition)}");
                return;
            }

            if (condition == null)
            {
                return;
            }

            var mission = CurrentMission();
            if (condition.StartsWith(MissionReporter.Tag + " objective=", StringComparison.Ordinal)
                && condition.EndsWith(" state=" + ObjectiveState.Active, StringComparison.Ordinal))
            {
                _missionStarted = true;
            }
            else if (condition.StartsWith(CampaignRunner.LogTag + " no save; starting ", StringComparison.Ordinal))
            {
                _startedWithoutSave = true;
            }
            else if (condition.StartsWith(CampaignRunner.LogTag + " saved mission=", StringComparison.Ordinal)
                && mission.Checkpoints.Count > 0
                && condition.Contains(" checkpoint=" + mission.Checkpoints[0].Id + " "))
            {
                _savedAtStart = true;
            }
            else if (string.Equals(condition, $"{CampaignRunner.LogTag} mission={mission.Id} completed", StringComparison.Ordinal))
            {
                _missionCompleted = true;
            }
            else if (condition.StartsWith(DeathBodyPresenter.LogTag + " collapse ", StringComparison.Ordinal))
            {
                _deathBodyCollapsed = true;
                _ragdollCollapsed |= condition.Contains(" ragdoll ");
            }
            else if (condition.StartsWith(DeathRagdoll.LogTag + " ragdoll frozen ", StringComparison.Ordinal))
            {
                _ragdollFrozen = true;
            }
            else if (condition.StartsWith(DeathBodyPresenter.LogTag + " removed ", StringComparison.Ordinal))
            {
                _deathBodyRemoved = true;
            }
        }

        private static string KillOneDummy()
        {
            var dummies = UnityEngine.Object.FindObjectsByType<TargetDummy>(FindObjectsInactive.Exclude);
            foreach (var dummy in dummies)
            {
                var damageable = dummy.Damageable;
                if (damageable == null || damageable.IsDead)
                {
                    continue;
                }

                var source = dummy.transform.position + (dummy.transform.forward * KillSourceDistance);
                Debug.Log($"{LogTag} killing target id={damageable.Id} to check the death body");
                damageable.TakeDamage(damageable.Health.Current, source);
                return null;
            }

            return "no living TargetDummy to kill; the death body could not be checked";
        }

        private static void OnUpdate()
        {
            if (_finished)
            {
                return;
            }

            try
            {
                Step();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"failed: {e.Message}");
            }
        }

        private static void Step()
        {
            var now = EditorApplication.timeSinceStartup;
            var phase = SessionState.GetInt(PhaseKey, PhaseEntering);
            if (phase == PhaseLeaving)
            {
                // 前のミッションの Play Mode を抜け切ってから次のシーンを開く。
                if (EditorApplication.isPlaying
                    || EditorApplication.isPlayingOrWillChangePlaymode
                    || EditorApplication.isCompiling
                    || EditorApplication.isUpdating)
                {
                    return;
                }

                StartMission(SessionState.GetInt(MissionIndexKey, 0));
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                var requestedAt = SessionState.GetFloat(RequestedAtKey, (float)now);
                if (now - requestedAt > EnterTimeoutSeconds)
                {
                    Finish(FailureExitCode, $"did not enter play mode for mission={CurrentMission().Id} within {EnterTimeoutSeconds}s");
                }

                return;
            }

            if (_playStartedAt < 0d)
            {
                SessionState.SetInt(PhaseKey, PhasePlaying);
                _playStartedAt = now;
                return;
            }

            var elapsed = now - _playStartedAt;
            if (!_killedDummy && elapsed >= KillDummyAfterSeconds)
            {
                _killedDummy = true;
                _killProblem = KillOneDummy();
            }

            if (_killedDummy && elapsed >= WalkthroughAfterSeconds && _walkProblem == null && !_missionCompleted)
            {
                if (!_walkStarted)
                {
                    _walkStarted = true;
                    _collapsedBeforeWalk = _deathBodyCollapsed;
                }

                WalkThrough(CurrentMission(), now);
            }

            var seconds = SessionState.GetFloat(SecondsKey, DefaultSeconds);
            if (elapsed < seconds)
            {
                return;
            }

            EndMission();
        }

        private static void WalkThrough(CampaignMissionSpec mission, double now)
        {
            if (_walkStep >= mission.Objectives.Count)
            {
                return;
            }

            if (_walkStepStartedAt < 0d)
            {
                _walkStepStartedAt = now;
            }

            var objectiveId = mission.Objectives[_walkStep].Id;
            var waiting = TryProgress(objectiveId, out var progressed);
            if (progressed)
            {
                Debug.Log($"{LogTag} walkthrough completed objective={objectiveId}");
                _walkStep++;
                _walkStepStartedAt = -1d;
                return;
            }

            if (now - _walkStepStartedAt > WalkthroughStepTimeoutSeconds)
            {
                _walkProblem = $"walkthrough stopped at objective={objectiveId}: {waiting}";
            }
        }

        private static string TryProgress(string objectiveId, out bool progressed)
        {
            progressed = false;
            var runner = UnityEngine.Object.FindAnyObjectByType<MissionRunner>();
            var campaign = UnityEngine.Object.FindAnyObjectByType<CampaignRunner>();
            if (runner == null || !runner.IsStarted || campaign == null || !campaign.IsStarted)
            {
                return "the mission or the campaign has not started";
            }

            var state = runner.Mission.GetState(objectiveId);
            if (state == ObjectiveState.Completed)
            {
                progressed = true;
                return null;
            }

            if (state != ObjectiveState.Active)
            {
                return $"the objective is {state}";
            }

            var player = GameObject.FindWithTag(GameBootstrap.PlayerTag);
            var playerCollider = player != null ? player.GetComponent<Collider>() : null;
            if (playerCollider == null)
            {
                return $"no player Collider tagged '{GameBootstrap.PlayerTag}'";
            }

            foreach (var area in UnityEngine.Object.FindObjectsByType<ObjectiveAreaBase>(FindObjectsInactive.Exclude))
            {
                if (!string.Equals(area.ObjectiveId, objectiveId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!area.IsBound)
                {
                    return $"{area.GetType().Name} '{area.name}' is not bound to the mission";
                }

                area.HandleEnter(playerCollider);
                if (area is InteractObjective interact && !interact.Interact())
                {
                    area.HandleExit(playerCollider);
                    return $"InteractObjective '{area.name}' did not progress on Interact";
                }

                area.HandleExit(playerCollider);
                return "the objective did not complete after the player entered its area";
            }

            foreach (var kill in UnityEngine.Object.FindObjectsByType<KillTargetObjective>(FindObjectsInactive.Exclude))
            {
                if (!string.Equals(kill.ObjectiveId, objectiveId, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!kill.IsBound)
                {
                    return $"KillTargetObjective '{kill.name}' is not bound to the mission";
                }

                // 増援は目標が Active になった次のフレームに現れる。現れる前に倒すと数えないので、そろうまで待つ。
                foreach (var target in kill.BoundTargets)
                {
                    if (target != null && !target.IsDead && !target.gameObject.activeInHierarchy)
                    {
                        return $"target '{target.name}' of '{kill.name}' has not appeared";
                    }
                }

                foreach (var target in kill.BoundTargets)
                {
                    if (target != null && !target.IsDead)
                    {
                        target.Kill();
                    }
                }

                return "the objective did not complete after every target was killed";
            }

            return "nothing in the scene progresses the objective";
        }

        private static void EndMission()
        {
            var mission = CurrentMission();
            var problems = new List<string>();
            if (_killProblem != null)
            {
                problems.Add(_killProblem);
            }
            else if (!_killedDummy)
            {
                problems.Add($"the run ended before {KillDummyAfterSeconds}s; no TargetDummy was killed");
            }
            else if (!(_walkStarted ? _collapsedBeforeWalk : _deathBodyCollapsed))
            {
                problems.Add($"no '{DeathBodyPresenter.LogTag} collapse ...' line after killing a TargetDummy; the campaign rules did not collapse the body");
            }

            if (!_missionStarted)
            {
                problems.Add($"no '{MissionReporter.Tag} objective=... state=Active' line; the mission did not start");
            }

            if (!_startedWithoutSave)
            {
                problems.Add($"no '{CampaignRunner.LogTag} no save; starting ...' line; the campaign did not start without a save");
            }

            if (_walkProblem != null)
            {
                problems.Add(_walkProblem);
            }
            else if (!_missionCompleted)
            {
                problems.Add(
                    $"no '{CampaignRunner.LogTag} mission={mission.Id} completed' line; the walkthrough reached {_walkStep}/{mission.Objectives.Count} objectives");
            }

            problems.AddRange(_errors);

            Debug.Log(
                $"{LogTag} mission={mission.Id} started={_missionStarted} started without save={_startedWithoutSave} saved at checkpoint={_savedAtStart} death body collapsed={_deathBodyCollapsed} ragdoll={_ragdollCollapsed} frozen={_ragdollFrozen} removed={_deathBodyRemoved} objectives={_walkStep}/{mission.Objectives.Count} completed={_missionCompleted} errors={_errors.Count}");

            var recorded = SessionState.GetString(ProblemsKey, string.Empty);
            foreach (var problem in problems)
            {
                var line = $"mission={mission.Id}: {OneLine(problem)}";
                recorded = recorded.Length == 0 ? line : recorded + ProblemSeparator + line;
            }

            SessionState.SetString(ProblemsKey, recorded);

            var next = SessionState.GetInt(MissionIndexKey, 0) + 1;
            if (next < CampaignSceneBuilder.Missions().Count)
            {
                SessionState.SetInt(MissionIndexKey, next);
                SessionState.SetInt(PhaseKey, PhaseLeaving);
                EditorApplication.ExitPlaymode();
                return;
            }

            if (recorded.Length == 0)
            {
                Finish(SuccessExitCode, "passed");
            }
            else
            {
                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", recorded.Split(ProblemSeparator)));
            }
        }

        private static void Finish(int exitCode, string summary)
        {
            _finished = true;
            Detach();
            SessionState.EraseBool(ActiveKey);
            SessionState.EraseFloat(SecondsKey);
            SessionState.EraseFloat(RequestedAtKey);
            SessionState.EraseInt(MissionIndexKey);
            SessionState.EraseInt(PhaseKey);
            SessionState.EraseString(ProblemsKey);

            try
            {
                DeleteSaves("after the run");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

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

        private static void DeleteSaves(string when)
        {
            var storage = FileSaveStorage.CreateDefault();
            foreach (var mission in CampaignSceneBuilder.Missions())
            {
                var slot = CampaignSaveSlots.For(mission.Id);
                storage.Delete(slot);
                Debug.Log($"{LogTag} deleted save slot={slot} {when} path={storage.PathFor(slot)}");
            }
        }

        private static string OneLine(string text)
        {
            return (text ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
        }
    }
}
