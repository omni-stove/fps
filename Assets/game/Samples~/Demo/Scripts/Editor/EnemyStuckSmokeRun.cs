using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Shooter.Editor
{
    // 敵が目標に気を取られないよう、Play 直後にプレイヤーを無効にする。
    // Play Mode に入るときにスクリプトが読み直されても続きを走らせられるよう、状態は SessionState に置く。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class EnemyStuckSmokeRun
    {
        public const string LogTag = "[EnemyStuckSmokeRun]";

        public const float TimeoutSeconds = 60f;

        public const float EnterTimeoutSeconds = 120f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        public static readonly Vector3 UnreachablePoint = new Vector3(0f, 0f, 30f);

        public static readonly Vector3 ReachablePoint = new Vector3(14f, 0f, 2f);

        public const float WalkSeconds = 3f;

        // NavMeshAgent を有効にし直したときに Unity が自動で NavMesh に乗せない遠さにし (2.5m では乗せられた)、
        // EnemyAgent.OffMeshSnapRadius より遠くして近くの NavMesh にも戻れなくする。
        public const float OffMeshOffset = 10f;

        private const string ActiveKey = "Shooter.EnemyStuckSmokeRun.Active";
        private const string RequestedAtKey = "Shooter.EnemyStuckSmokeRun.RequestedAt";

        private static readonly string StuckPrefix = $"{EnemyAgent.LogTag} id={EnemyTestSceneBuilder.EnemyId} stuck recovery=";
        private static readonly string WarpedPrefix = $"{EnemyAgent.LogTag} id={EnemyTestSceneBuilder.EnemyId} warped ";

        private static readonly StuckRecovery[] ExpectedRecoveries =
        {
            StuckRecovery.Repath, StuckRecovery.Sidestep, StuckRecovery.AbandonGoal,
        };

        private static readonly List<string> _errors = new List<string>();
        private static readonly List<StuckRecovery> _recoveries = new List<StuckRecovery>();
        private static EnemyAgent _enemy;
        private static double _abandonedAt = -1d;
        private static int _recoveriesBeforeDisplace = -1;
        private static bool _displaced;
        private static bool _warped;
        private static double _playStartedAt = -1d;
        private static bool _finished;

        static EnemyStuckSmokeRun()
        {
            if (SessionState.GetBool(ActiveKey, false))
            {
                Attach();
            }
        }

        public static void Run()
        {
            try
            {
                WeaponAssetBuilder.Build();
                EnemyTestSceneBuilder.Build();
                EditorSceneManager.OpenScene(EnemyTestSceneBuilder.ScenePath, OpenSceneMode.Single);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log($"{LogTag} entering play mode in '{EnemyTestSceneBuilder.ScenePath}'");
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(FailureExitCode, $"could not start: {e.Message}");
            }
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
            _recoveries.Clear();
            _enemy = null;
            _abandonedAt = -1d;
            _recoveriesBeforeDisplace = -1;
            _displaced = false;
            _warped = false;
            _playStartedAt = -1d;
            _finished = false;
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (_finished || !EditorApplication.isPlaying)
            {
                return;
            }

            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                // EnemyFireSmokeRun と同じく、batchmode の Editor の起動時インデックスが投げる例外はゲームのものではない。
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

            if (condition.StartsWith(StuckPrefix, StringComparison.Ordinal))
            {
                var name = condition.Substring(StuckPrefix.Length).Split(' ')[0];
                if (Enum.TryParse<StuckRecovery>(name, out var recovery))
                {
                    _recoveries.Add(recovery);
                    if (recovery == StuckRecovery.AbandonGoal && _abandonedAt < 0d)
                    {
                        _abandonedAt = EditorApplication.timeSinceStartup;
                    }
                }
            }
            else if (condition.StartsWith(WarpedPrefix, StringComparison.Ordinal))
            {
                _warped = true;
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

            if (_enemy == null && !Setup())
            {
                return;
            }

            if (!_displaced && _abandonedAt >= 0d && now - _abandonedAt >= WalkSeconds)
            {
                _recoveriesBeforeDisplace = _recoveries.Count;
                Displace();
            }

            if (!_warped && now - _playStartedAt < TimeoutSeconds)
            {
                return;
            }

            Report();
        }

        private static bool Setup()
        {
            _enemy = UnityEngine.Object.FindAnyObjectByType<EnemyAgent>();
            if (_enemy == null || !_enemy.IsInitialized)
            {
                Finish(FailureExitCode, "no initialized EnemyAgent in the scene");
                return false;
            }

            var player = GameObject.FindGameObjectWithTag(EnemyAgent.TargetTag);
            if (player != null)
            {
                player.SetActive(false);
            }

            _enemy.Target = null;
            _enemy.SetPatrolRoute(new[] { UnreachablePoint, ReachablePoint });
            Debug.Log($"{LogTag} patrol set to unreachable {UnreachablePoint} then {ReachablePoint}");
            return true;
        }

        private static void Displace()
        {
            _displaced = true;
            var agent = _enemy.GetComponent<NavMeshAgent>();
            var position = _enemy.transform.position;
            var edgeX = (EnemyTestSceneBuilder.FloorScale.x * 5f) + OffMeshOffset;
            agent.enabled = false;
            _enemy.transform.position = new Vector3(edgeX, position.y, Mathf.Clamp(position.z, -10f, 10f));
            agent.enabled = true;

            if (agent.isOnNavMesh)
            {
                _errors.Add($"the enemy is still on the NavMesh after moving it to {_enemy.transform.position}; raise OffMeshOffset");
            }

            Debug.Log($"{LogTag} displaced the enemy off the NavMesh to {_enemy.transform.position}");
        }

        private static void Report()
        {
            var problems = new List<string>();
            var expected = string.Join(", ", ExpectedRecoveries);
            var before = _recoveriesBeforeDisplace >= 0 ? _recoveries.GetRange(0, _recoveriesBeforeDisplace) : _recoveries;
            if (string.Join(", ", before) != expected)
            {
                problems.Add($"recoveries before leaving the NavMesh were [{string.Join(", ", before)}]; expected [{expected}]");
            }

            if (!_displaced)
            {
                problems.Add("the enemy never gave up the unreachable point, so it was not moved off the NavMesh");
            }
            else if (!_warped)
            {
                problems.Add($"no '{WarpedPrefix}...' line; the enemy off the NavMesh was not brought back");
            }

            problems.AddRange(_errors);

            Debug.Log($"{LogTag} recoveries=[{string.Join(", ", _recoveries)}] warped={_warped} errors={_errors.Count}");
            if (problems.Count == 0)
            {
                Finish(SuccessExitCode, "passed");
            }
            else
            {
                Finish(FailureExitCode, "failed:\n  " + string.Join("\n  ", problems));
            }
        }

        private static void Finish(int exitCode, string summary)
        {
            _finished = true;
            Detach();
            SessionState.EraseBool(ActiveKey);
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
