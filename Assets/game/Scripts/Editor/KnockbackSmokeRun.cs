using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Core;
using Fps.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace Fps.Editor
{
    // 射撃場 (Bootstrap / TpsRange) には NavMesh も敵も無いので、敵とプレイヤーが同じ床にいる EnemyAiTest で確かめる。
    // 投げると着弾点と信管で爆発の位置がぶれるので、グレネードはその場に作ってすぐ爆発させる。
    [InitializeOnLoad]
    public static class KnockbackSmokeRun
    {
        public const string LogTag = "[KnockbackSmokeRun]";

        public const float WarmUpSeconds = 1f;
        public const float ExerciseTimeoutSeconds = 30f;
        public const float EnterTimeoutSeconds = 120f;

        // 体の表面から中心までがグレネードの減らさない半径に収まり、強さ 1 で押す距離。
        public const float BlastOffset = 1.5f;

        // 床の高さで爆発させると、中心から狙う点への線が床に遮られることがある。
        public const float BlastHeight = 0.3f;

        // 飛ばした後に着地まで見るので、倒さない量にする。
        public const int LightBlastDamage = 10;

        public const int LethalBlastDamage = 1000;
        public const float MinPushDistance = 0.5f;

        // ラグドールが押された向きへ動いたかを見るまでの秒数。固まる (RagdollCatalog.SettleSeconds) より前に見る。
        public const float RagdollWatchSeconds = 0.5f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Fps.KnockbackSmokeRun.Active";
        private const string RequestedAtKey = "Fps.KnockbackSmokeRun.RequestedAt";

        private static readonly string _enemyLaunchLog =
            $"{Knockback.LogTag} id={EnemyTestSceneBuilder.EnemyId} launched kind={DamageKind.Explosion} ";

        private static readonly string _enemyLandLog = $"{Knockback.LogTag} id={EnemyTestSceneBuilder.EnemyId} {Knockback.ReasonLanded} ";
        private static readonly string _enemyResumeLog = EnemyAgent.FormatResumeLog(EnemyTestSceneBuilder.EnemyId, EnemyAgent.ReasonKnockback);
        private static readonly string _playerLaunchLog = $"{Knockback.LogTag} id={GameBootstrap.PlayerName} launched kind={DamageKind.Explosion} ";
        private static readonly string _playerLandLog = $"{Knockback.LogTag} id={GameBootstrap.PlayerName} {Knockback.ReasonLanded} ";
        private static readonly string _playerOverrideLog = $"{PlayerController.LogTag} override={nameof(KnockbackMovement)} active=true";
        private static readonly string _ragdollLog = $"{DeathBodyPresenter.LogTag} knockback id={EnemyTestSceneBuilder.EnemyId} ";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static bool _sawEnemyLaunch;
        private static bool _sawEnemyLand;
        private static bool _sawEnemyResume;
        private static bool _sawPlayerLaunch;
        private static bool _sawPlayerOverride;
        private static bool _sawPlayerLand;
        private static bool _sawRagdoll;
        private static bool _ragdollMoved;
        private static Vector3 _from;
        private static Vector3 _push;
        private static double _stepStartedAt;
        private static double _playStartedAt = -1d;
        private static double _exerciseStartedAt = -1d;
        private static bool _finished;

        private enum Step
        {
            ExplodeNearPlayer,
            WaitPlayerLanded,
            ExplodeNearEnemy,
            WaitEnemyLanded,
            WaitEnemyResumed,
            KillEnemy,
            WatchRagdoll,
            Done,
        }

        static KnockbackSmokeRun()
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
            _step = Step.ExplodeNearPlayer;
            _sawEnemyLaunch = false;
            _sawEnemyLand = false;
            _sawEnemyResume = false;
            _sawPlayerLaunch = false;
            _sawPlayerOverride = false;
            _sawPlayerLand = false;
            _sawRagdoll = false;
            _ragdollMoved = false;
            _from = Vector3.zero;
            _push = Vector3.zero;
            _stepStartedAt = 0d;
            _playStartedAt = -1d;
            _exerciseStartedAt = -1d;
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
                // ほかの SmokeRun と同じく、Editor の起動時インデックスが Play 中に投げる例外はゲームのコードではないので外す。
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

            if (condition.StartsWith(_playerLaunchLog, StringComparison.Ordinal))
            {
                _sawPlayerLaunch = true;
            }
            else if (string.Equals(condition, _playerOverrideLog, StringComparison.Ordinal))
            {
                _sawPlayerOverride = true;
            }
            else if (condition.StartsWith(_playerLandLog, StringComparison.Ordinal))
            {
                _sawPlayerLand = true;
            }
            else if (condition.StartsWith(_enemyLaunchLog, StringComparison.Ordinal))
            {
                _sawEnemyLaunch = true;
            }
            else if (condition.StartsWith(_enemyLandLog, StringComparison.Ordinal))
            {
                _sawEnemyLand = true;
            }
            else if (string.Equals(condition, _enemyResumeLog, StringComparison.Ordinal))
            {
                _sawEnemyResume = true;
            }
            else if (condition.StartsWith(_ragdollLog, StringComparison.Ordinal))
            {
                _sawRagdoll = true;
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

            if (_exerciseStartedAt < 0d)
            {
                _exerciseStartedAt = now;
            }

            string problem;
            try
            {
                problem = Advance(now);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                problem = $"{e.GetType().Name} at step {_step}: {e.Message}";
            }

            if (problem == null && _step != Step.Done && now - _exerciseStartedAt > ExerciseTimeoutSeconds)
            {
                problem = $"step {_step} did not finish within {ExerciseTimeoutSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s";
            }

            if (problem == null && _step != Step.Done && _errors.Count == 0)
            {
                return;
            }

            Report(problem);
        }

        private static string Advance(double now)
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null || bootstrap.World == null)
            {
                return "the scene has no GameBootstrap that built the player";
            }

            var world = bootstrap.World;
            var movement = world.Player.GetComponent<KnockbackMovement>();
            if (movement == null || movement.Mover != world.Mover)
            {
                return "the player has no KnockbackMovement bound to its PlayerController";
            }

            var enemy = FindEnemy();
            if (enemy == null)
            {
                return $"the scene has no EnemyAgent '{EnemyTestSceneBuilder.EnemyId}'; rerun {EnemyTestSceneBuilder.MenuPath}";
            }

            var enemyBody = enemy.GetComponent<Damageable>();
            switch (_step)
            {
                case Step.ExplodeNearPlayer:
                    _push = Vector3.forward;
                    _from = world.Player.transform.position;
                    Explode(world.PlayerDamageable.GroundPoint - (_push * BlastOffset), LightBlastDamage);
                    if (!movement.IsKnockedBack)
                    {
                        return $"a blast {BlastOffset}m behind the player did not knock it back";
                    }

                    _step = Step.WaitPlayerLanded;
                    return null;

                case Step.WaitPlayerLanded:
                    if (movement.IsKnockedBack || !world.Mover.IsGrounded)
                    {
                        return null;
                    }

                    if (!_sawPlayerLaunch || !_sawPlayerOverride || !_sawPlayerLand)
                    {
                        return $"the player landed without logging '{_playerLaunchLog}', '{_playerOverrideLog}' and '{_playerLandLog}' (launch={_sawPlayerLaunch} override={_sawPlayerOverride} land={_sawPlayerLand})";
                    }

                    return CheckPushed("the player", world.Player.transform.position, Step.ExplodeNearEnemy);

                case Step.ExplodeNearEnemy:
                    _push = Vector3.right;
                    _from = enemy.GroundPosition;
                    Explode(enemyBody.GroundPoint - (_push * BlastOffset), LightBlastDamage);
                    if (!enemy.IsKnockedBack)
                    {
                        return $"a blast {BlastOffset}m beside '{EnemyTestSceneBuilder.EnemyId}' did not knock it back (dead={enemyBody.IsDead})";
                    }

                    if (enemy.GetComponent<NavMeshAgent>().enabled)
                    {
                        return "the enemy's NavMeshAgent stayed enabled while flying";
                    }

                    _step = Step.WaitEnemyLanded;
                    return null;

                case Step.WaitEnemyLanded:
                    if (enemy.IsKnockedBack)
                    {
                        return null;
                    }

                    if (!_sawEnemyLaunch || !_sawEnemyLand)
                    {
                        return $"the enemy landed without logging '{_enemyLaunchLog}' and '{_enemyLandLog}' (launch={_sawEnemyLaunch} land={_sawEnemyLand})";
                    }

                    var agent = enemy.GetComponent<NavMeshAgent>();
                    if (!agent.enabled || !agent.isOnNavMesh)
                    {
                        return $"the enemy landed at {Format(enemy.GroundPosition)} but is not back on the NavMesh (agent enabled={agent.enabled})";
                    }

                    return CheckPushed("the enemy", enemy.GroundPosition, Step.WaitEnemyResumed);

                case Step.WaitEnemyResumed:
                    if (!_sawEnemyResume)
                    {
                        return null;
                    }

                    _step = Step.KillEnemy;
                    return null;

                case Step.KillEnemy:
                    var presenter = DeathBodyPresenter.Attach(enemy.gameObject);
                    presenter.Bind(enemyBody, DeathBody.Collapse);
                    _push = Vector3.right;
                    Explode(enemyBody.GroundPoint - (_push * BlastOffset), LethalBlastDamage);
                    if (!enemyBody.IsDead)
                    {
                        return "a lethal blast did not kill the enemy";
                    }

                    if (!presenter.HasShell)
                    {
                        return "the dead enemy left no death body to push";
                    }

                    if (!_sawRagdoll)
                    {
                        return $"the death body was not pushed by the blast (no '{_ragdollLog}' line)";
                    }

                    _from = ShellCenter(presenter.Shell);
                    _stepStartedAt = now;
                    _step = Step.WatchRagdoll;
                    return null;

                case Step.WatchRagdoll:
                    if (now - _stepStartedAt < RagdollWatchSeconds)
                    {
                        return null;
                    }

                    var dead = enemy.GetComponent<DeathBodyPresenter>();
                    if (dead == null || !dead.HasShell)
                    {
                        return "the enemy's death body disappeared before it could be watched";
                    }

                    var moved = Vector3.Dot(ShellCenter(dead.Shell) - _from, _push);
                    if (moved < MinPushDistance)
                    {
                        return $"the death body moved only {moved.ToString("0.##", CultureInfo.InvariantCulture)}m along the blast {Format(_push)}";
                    }

                    _ragdollMoved = true;
                    _step = Step.Done;
                    return null;

                default:
                    return null;
            }
        }

        private static string CheckPushed(string who, Vector3 to, Step next)
        {
            var moved = Vector3.Dot(to - _from, _push);
            if (moved < MinPushDistance)
            {
                return $"{who} moved only {moved.ToString("0.##", CultureInfo.InvariantCulture)}m along the blast {Format(_push)} (from {Format(_from)} to {Format(to)})";
            }

            _step = next;
            return null;
        }

        private static void Explode(Vector3 groundCenter, int damage)
        {
            var center = groundCenter + (Vector3.up * BlastHeight);
            var explosion = new ExplosionDamage(damage, GrenadeSpec.DefaultDamageRadius, GrenadeSpec.DefaultFullDamageRadius);
            var grenade = ThrownGrenade.Spawn(null, center, Vector3.zero, 0f, explosion, 0);
            Debug.Log($"{LogTag} exploding dmg={damage.ToString(CultureInfo.InvariantCulture)} at {Format(center)}");
            grenade.Explode();
        }

        private static EnemyAgent FindEnemy()
        {
            foreach (var agent in UnityEngine.Object.FindObjectsByType<EnemyAgent>())
            {
                if (agent.Id == EnemyTestSceneBuilder.EnemyId)
                {
                    return agent;
                }
            }

            return null;
        }

        // ラグドールは根が動かずボーンだけが飛ぶので、Rigidbody の位置の平均で見る。
        private static Vector3 ShellCenter(GameObject shell)
        {
            var bodies = shell.GetComponentsInChildren<Rigidbody>();
            if (bodies.Length == 0)
            {
                return shell.transform.position;
            }

            var sum = Vector3.zero;
            foreach (var body in bodies)
            {
                sum += body.worldCenterOfMass;
            }

            return sum / bodies.Length;
        }

        private static string Format(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.##}, {1:0.##}, {2:0.##})", v.x, v.y, v.z);
        }

        private static void Report(string problem)
        {
            var problems = new List<string>();
            if (problem != null)
            {
                problems.Add(problem);
            }

            problems.AddRange(_errors);
            Debug.Log(
                $"{LogTag} step={_step} player launch={_sawPlayerLaunch} override={_sawPlayerOverride} land={_sawPlayerLand} enemy launch={_sawEnemyLaunch} land={_sawEnemyLand} resume={_sawEnemyResume} ragdoll={_sawRagdoll} ragdoll moved={_ragdollMoved} errors={_errors.Count.ToString(CultureInfo.InvariantCulture)}");

            var passed = problems.Count == 0
                && _step == Step.Done
                && _sawPlayerLaunch
                && _sawPlayerOverride
                && _sawPlayerLand
                && _sawEnemyLaunch
                && _sawEnemyLand
                && _sawEnemyResume
                && _sawRagdoll
                && _ragdollMoved;
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
