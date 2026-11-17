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
    // batchmode には入力が無いので、Cover / Attack / Reload / 肩の入れ替えの代わりに CoverController.Tick・
    // WeaponController.PullTrigger・WeaponController.TryActiveReload・ViewRig.Tick を呼ぶ。
    // Play Mode に入るので、batchmode で呼ぶときは -quit を付けない。
    [InitializeOnLoad]
    public static class TpsActionSmokeRun
    {
        public const string LogTag = "[TpsActionSmokeRun]";

        // GameBootstrap の Awake と各コンポーネントの初期化を済ませてから操作し始める。
        public const float WarmUpSeconds = 1f;

        public const float ExerciseTimeoutSeconds = 10f;

        public const float EnterTimeoutSeconds = 120f;

        // MotorSpec.DefaultCoverMaxEnterDistance に収まる隙間にする。
        public const float StandOffFromCover = 0.3f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Shooter.TpsActionSmokeRun.Active";
        private const string RequestedAtKey = "Shooter.TpsActionSmokeRun.RequestedAt";

        private static readonly string _coverEnterLog = CoverState.LogTag + " enter";
        private static readonly string _activeReloadSuccessLog = WeaponController.ActiveReloadLog(ActiveReloadResult.Success);
        private static readonly string _shoulderLeftLog = ViewRig.FormatShoulderLog(ShoulderSide.Left);

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static bool _sawCoverEnter;
        private static bool _sawBlindFire;
        private static bool _sawActiveReloadSuccess;
        private static bool _sawShoulderLeft;
        private static double _playStartedAt = -1d;
        private static double _exerciseStartedAt = -1d;
        private static bool _finished;

        private enum Step
        {
            EnterCover,
            WaitInCover,
            BlindFire,
            ActiveReload,
            SwapShoulder,
            Done,
        }

        static TpsActionSmokeRun()
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
                // シーンは射撃場のルール (カバー・ブラインドファイア・アクティブリロードが On) を配線するので、先にアセットを作り直す。
                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                BootstrapSceneBuilder.Build();
                EditorSceneManager.OpenScene(BootstrapSceneBuilder.DefaultScenePath, OpenSceneMode.Single);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log($"{LogTag} entering play mode in '{BootstrapSceneBuilder.DefaultScenePath}'");
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
            _step = Step.EnterCover;
            _sawCoverEnter = false;
            _sawBlindFire = false;
            _sawActiveReloadSuccess = false;
            _sawShoulderLeft = false;
            _playStartedAt = -1d;
            _exerciseStartedAt = -1d;
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

            if (condition.StartsWith(_coverEnterLog, StringComparison.Ordinal))
            {
                _sawCoverEnter = true;
            }
            else if (condition.StartsWith(BlindFireShot.LogPrefix, StringComparison.Ordinal))
            {
                _sawBlindFire = true;
            }
            else if (string.Equals(condition, _activeReloadSuccessLog, StringComparison.Ordinal))
            {
                _sawActiveReloadSuccess = true;
            }
            else if (string.Equals(condition, _shoulderLeftLog, StringComparison.Ordinal))
            {
                _sawShoulderLeft = true;
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
                problem = Advance();
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

        private static string Advance()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null || bootstrap.World == null)
            {
                return "the scene has no GameBootstrap that built the player";
            }

            var world = bootstrap.World;
            var player = world.Player;
            var cover = player.GetComponent<CoverController>();
            var blindFire = player.GetComponent<BlindFireController>();
            var weapon = world.Weapon;
            if (cover == null || !cover.IsInitialized || !cover.enabled)
            {
                return $"the player's CoverController is missing, uninitialized or disabled (rules: {PlayerActions.FormatLog(world.Rules.Actions)})";
            }

            if (blindFire == null || !blindFire.IsInitialized || !blindFire.enabled)
            {
                return $"the player's BlindFireController is missing, uninitialized or disabled (rules: {PlayerActions.FormatLog(world.Rules.Actions)})";
            }

            if (weapon == null || !weapon.IsInitialized)
            {
                return "the player's WeaponController is not initialized";
            }

            switch (_step)
            {
                case Step.EnterCover:
                    return EnterLowCover(world, cover);

                case Step.WaitInCover:
                    if (!cover.IsInCover)
                    {
                        return $"the player left the cover while sliding in (phase {cover.Phase})";
                    }

                    if (cover.Phase == CoverPhase.In)
                    {
                        _step = Step.BlindFire;
                    }

                    return null;

                case Step.BlindFire:
                    return FireBlind(cover, blindFire, weapon);

                case Step.ActiveReload:
                    return JudgeActiveReload(weapon);

                case Step.SwapShoulder:
                    return SwapShoulder(world.Rig);

                default:
                    return null;
            }
        }

        private static string EnterLowCover(BootstrapWorld world, CoverController cover)
        {
            var actions = world.Rules.Actions;
            if (actions.Cover != Cover.On || actions.BlindFire != BlindFire.On || actions.ActiveReload != ActiveReload.On)
            {
                return $"the range rules must turn cover, blind fire and active reload on; rerun {GameRulesAssetBuilder.MenuPath} (got {PlayerActions.FormatLog(actions)})";
            }

            var path = BootstrapSceneBuilder.CoversName + "/" + BootstrapSceneBuilder.LowCoverName;
            var lowCover = GameObject.Find(path);
            var wall = lowCover != null ? lowCover.GetComponent<Collider>() : null;
            if (wall == null)
            {
                return $"the scene has no '{path}' with a collider; rerun {BootstrapSceneBuilder.MenuPath}";
            }

            // 遮蔽物の手前 (-Z 側) の面の真ん中に、+Z (遮蔽物) を向いて立たせる。端から離すのは、左端で肩が左に替わらないようにするため。
            var bounds = wall.bounds;
            var standOff = GameBootstrap.PlayerRadius + StandOffFromCover;
            var position = new Vector3(bounds.center.x, GameBootstrap.PlayerSpawn.y, bounds.min.z - standOff);
            Teleport(world.Controller, position);
            FaceForward(world);

            cover.Tick(true, false, Vector2.zero, 0f);
            if (!cover.IsInCover)
            {
                var probe = cover.LastProbe;
                return $"could not enter the low cover from {Format(position)} (wall={probe.IsWall} canEnter={probe.CanEnter} distance={probe.Distance.ToString("0.###", CultureInfo.InvariantCulture)})";
            }

            if (!_sawCoverEnter)
            {
                return $"entered the cover but no '{_coverEnterLog}' line was logged";
            }

            if (cover.CoverHeight != CoverHeight.Low)
            {
                return $"'{BootstrapSceneBuilder.LowCoverName}' was judged as a {cover.CoverHeight} cover";
            }

            _step = Step.WaitInCover;
            return null;
        }

        private static string FireBlind(CoverController cover, BlindFireController blindFire, WeaponController weapon)
        {
            if (cover.IsPeeking || !blindFire.IsBlindFiring)
            {
                return $"firing from the cover would not be a blind fire (phase {cover.Phase})";
            }

            if (!weapon.PullTrigger(true, true))
            {
                return $"the weapon did not fire from the cover (ammo {weapon.Weapon.AmmoInMagazine}, reloading={weapon.Weapon.IsReloading})";
            }

            if (!_sawBlindFire)
            {
                return $"the shot from the cover logged no '{BlindFireShot.LogPrefix}' line";
            }

            _step = Step.ActiveReload;
            return null;
        }

        private static string JudgeActiveReload(WeaponController weapon)
        {
            var current = weapon.Weapon;
            if (!current.HasActiveReload)
            {
                return "the weapon was built without active reload; the rules did not reach the WeaponController";
            }

            if (!weapon.StartReload())
            {
                return $"the weapon did not start reloading (ammo {current.AmmoInMagazine}/{current.Spec.MagazineSize}, reserve {current.ReserveAmmo})";
            }

            // 同じフレームのうちに判定の幅の真ん中まで進めるので、WeaponController.Update の実時間の Tick とは入れ違わない。
            var spec = current.Spec;
            weapon.Tick(spec.ReloadSeconds * (spec.ActiveReloadWindowStart + (spec.ActiveReloadWindowLength / 2f)));
            if (!weapon.TryActiveReload())
            {
                return "pressing Reload again while reloading did not judge an active reload";
            }

            if (!_sawActiveReloadSuccess)
            {
                return $"the active reload in the middle of its window logged no '{_activeReloadSuccessLog}' line";
            }

            if (current.IsReloading || current.AmmoInMagazine != spec.MagazineSize)
            {
                return $"a successful active reload did not refill the magazine (ammo {current.AmmoInMagazine}/{spec.MagazineSize})";
            }

            _step = Step.SwapShoulder;
            return null;
        }

        private static string SwapShoulder(ViewRig rig)
        {
            if (rig == null || !rig.IsInitialized)
            {
                return "the player has no initialized ViewRig";
            }

            if (rig.Shoulder != ShoulderSide.Right)
            {
                return $"the shoulder must be Right before the swap (got {rig.Shoulder}); the cover may have overridden it at an edge";
            }

            // 肩の入れ替えは三人称のときだけ効く。射撃場は一人称で始まるので、ここで切り替える。
            rig.Mode = ViewMode.ThirdPerson;
            rig.Tick(true, 0f);
            if (rig.Shoulder != ShoulderSide.Left)
            {
                return $"swapping the shoulder in third person did not move it to Left (local view={rig.IsLocalView})";
            }

            if (!_sawShoulderLeft)
            {
                return $"swapping the shoulder logged no '{_shoulderLeftLog}' line";
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

            // カバーの Raycast は同じフレームに行うので、移した位置を物理に反映させておく。
            Physics.SyncTransforms();
        }

        // FirstPersonLook が自分の角度を持っているので、Transform を直接回すと次の視点操作で戻る。
        private static void FaceForward(BootstrapWorld world)
        {
            var look = world.Look;
            if (look != null && look.IsInitialized)
            {
                look.ApplyLookDelta(new Vector2(Mathf.DeltaAngle(look.Yaw, 0f), 0f));
            }
            else
            {
                world.Player.transform.rotation = Quaternion.identity;
            }
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
                $"{LogTag} step={_step} cover enter={_sawCoverEnter} blind fire={_sawBlindFire} active reload success={_sawActiveReloadSuccess} shoulder left={_sawShoulderLeft} errors={_errors.Count.ToString(CultureInfo.InvariantCulture)}");

            var passed = problems.Count == 0
                && _step == Step.Done
                && _sawCoverEnter
                && _sawBlindFire
                && _sawActiveReloadSuccess
                && _sawShoulderLeft;
            if (passed)
            {
                Finish(SuccessExitCode, "passed");
            }
            else
            {
                if (problems.Count == 0)
                {
                    problems.Add("not every expected line was logged");
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
