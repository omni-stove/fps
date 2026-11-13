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
    /// 武器とルールのアセットと TpsRange シーン (<see cref="TpsRangeSceneBuilder"/>) を生成し直して Play Mode に入り、
    /// プレイヤーをコードから動かして、このシーンで TPS のアクションが成り立つことを確かめて Editor を終える。
    /// <list type="number">
    /// <item>肩越し視点 (<see cref="ViewMode.ThirdPerson"/>) で始まっている。</item>
    /// <item>低い遮蔽物 (<see cref="TpsRangeSceneBuilder.LowCoverName"/>) の前でカバーに入り (<c>[Cover] enter</c>)、張り付いたまま狙わずに撃つ (<c>[Combat] blind-fire</c>)。</item>
    /// <item>カバーを出て乗り越えの壁 (<see cref="TpsRangeSceneBuilder.VaultWallName"/>) の手前に立ち、前へ進みながら跳んで向こう側に降りる。</item>
    /// <item>開けた場所 (<see cref="TpsRangeSceneBuilder.OpenAreaName"/>) で右へ回避し (<c>[Player] dodging=true</c> → <c>dodging=false</c>)、
    /// 転がっている間は撃てず、終わったら撃てる。</item>
    /// </list>
    /// どれかが成り立たないか、Play 中にエラー・例外・Assert のログが出たら終了コード 1、すべて成り立てば 0 で終わる。
    /// <see cref="SmokeSuite"/> には入れていない。Play Mode に入るので <c>-quit</c> は付けずに、Editor を閉じた状態で次のように呼ぶ:
    /// <code>unity -batchmode -projectPath . -executeMethod Fps.Editor.TpsRangeSmokeRun.Run -logFile -</code>
    /// </summary>
    /// <remarks>
    /// batchmode には入力が無いので、Cover / Attack / 移動とジャンプ / Dodge の代わりに <see cref="CoverController.Tick"/>・
    /// <see cref="WeaponController.PullTrigger"/>・<see cref="PlayerController.MoveFilter"/> への差し込み・<see cref="PlayerController.TryStartDodge"/> を使う。
    /// 乗り越えは「前へ進みながらジャンプを押して壁の向こうに降りられる」ことを確かめ、<see cref="PlayerController.IsVaulting"/> の動きの有無は問わない。
    /// </remarks>
    [InitializeOnLoad]
    public static class TpsRangeSmokeRun
    {
        public const string LogTag = "[TpsRangeSmokeRun]";

        /// <summary>Play Mode に入ってから操作し始めるまでに待つ秒数。GameBootstrap の Awake と各コンポーネントの初期化を済ませるため。</summary>
        public const float WarmUpSeconds = 1f;

        /// <summary>操作し始めてから、すべての手順が終わるまでに待つ秒数の上限。乗り越えで歩く分を見込む。</summary>
        public const float ExerciseTimeoutSeconds = 20f;

        /// <summary>Play Mode に入れないまま、これだけ待ったら失敗にする (秒)。</summary>
        public const float EnterTimeoutSeconds = 120f;

        /// <summary>カバーに入る前に置く位置の、体の表面から遮蔽物の手前の面までの隙間 (m)。<see cref="CoverProbe.MaxEnterDistance"/> に収まる。</summary>
        public const float StandOffFromCover = 0.3f;

        /// <summary>乗り越える前に置く位置の、体の表面から壁の手前の面までの隙間 (m)。</summary>
        public const float StandOffFromVaultWall = 1f;

        /// <summary>回避で少なくとも動くべき水平の距離の、<see cref="MotorSpec.DodgeDistance"/> に対する割合。床との摩擦や最後のフレームの端数を見込む。</summary>
        public const float MinDodgeDistanceRatio = 0.5f;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Fps.TpsRangeSmokeRun.Active";
        private const string RequestedAtKey = "Fps.TpsRangeSmokeRun.RequestedAt";
        private const string ExitReason = "smoke";

        private static readonly string _coverEnterLog = CoverState.LogTag + " enter";
        private static readonly string _dodgeStartLog = PlayerController.FormatDodgingLog(true);
        private static readonly string _dodgeEndLog = PlayerController.FormatDodgingLog(false);

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static bool _sawCoverEnter;
        private static bool _sawBlindFire;
        private static bool _sawDodgeStart;
        private static bool _sawDodgeEnd;
        private static bool _crossedVaultWall;
        private static bool _dodgeBlockedFire;
        private static double _playStartedAt = -1d;
        private static double _exerciseStartedAt = -1d;
        private static bool _finished;
        private static Vector3 _dodgeFrom;
        private static ForwardJumpFilter _vaultFilter;

        private enum Step
        {
            CheckView,
            EnterCover,
            WaitInCover,
            BlindFire,
            PlaceAtVaultWall,
            CrossVaultWall,
            PlaceInOpenArea,
            StartDodge,
            WaitDodgeEnd,
            FireAfterDodge,
            Done,
        }

        static TpsRangeSmokeRun()
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
                // シーンは TPS 射撃場のルール (カバー・回避・乗り越えが On) を配線するので、先にアセットを作り直す。
                WeaponAssetBuilder.Build();
                GameRulesAssetBuilder.Build();
                TpsRangeSceneBuilder.Build();
                EditorSceneManager.OpenScene(TpsRangeSceneBuilder.ScenePath, OpenSceneMode.Single);

                ResetObservations();
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetFloat(RequestedAtKey, (float)EditorApplication.timeSinceStartup);
                Attach();

                Debug.Log($"{LogTag} entering play mode in '{TpsRangeSceneBuilder.ScenePath}'");
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
            _step = Step.CheckView;
            _sawCoverEnter = false;
            _sawBlindFire = false;
            _sawDodgeStart = false;
            _sawDodgeEnd = false;
            _crossedVaultWall = false;
            _dodgeBlockedFire = false;
            _playStartedAt = -1d;
            _exerciseStartedAt = -1d;
            _finished = false;
            _dodgeFrom = Vector3.zero;
            _vaultFilter = null;
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
            else if (string.Equals(condition, _dodgeStartLog, StringComparison.Ordinal))
            {
                _sawDodgeStart = true;
            }
            else if (string.Equals(condition, _dodgeEndLog, StringComparison.Ordinal))
            {
                _sawDodgeEnd = true;
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

        /// <summary>
        /// 今の手順を 1 つ進める。次のフレームを待つ手順ではそのまま返す。進められない理由があれば返す (null なら問題なし)。
        /// 各手順は、前の手順の結果を確かめてから行う。
        /// </summary>
        private static string Advance()
        {
            var bootstrap = UnityEngine.Object.FindAnyObjectByType<GameBootstrap>();
            if (bootstrap == null || bootstrap.World == null)
            {
                return "the scene has no GameBootstrap that built the player";
            }

            var world = bootstrap.World;
            var player = world.Player;
            var mover = world.Mover;
            var cover = player.GetComponent<CoverController>();
            var blindFire = player.GetComponent<BlindFireController>();
            var weapon = world.Weapon;
            if (mover == null || !mover.IsInitialized)
            {
                return "the player's PlayerController is not initialized";
            }

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
                case Step.CheckView:
                    return CheckView(world);

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

                case Step.PlaceAtVaultWall:
                    return PlaceAtVaultWall(world, cover);

                case Step.CrossVaultWall:
                    return CrossVaultWall(world);

                case Step.PlaceInOpenArea:
                    return PlaceInOpenArea(world);

                case Step.StartDodge:
                    return StartDodge(world, weapon);

                case Step.WaitDodgeEnd:
                    return WaitDodgeEnd(world, weapon);

                case Step.FireAfterDodge:
                    return FireAfterDodge(weapon);

                default:
                    return null;
            }
        }

        private static string CheckView(BootstrapWorld world)
        {
            var actions = world.Rules.Actions;
            if (actions.Cover != Cover.On || actions.BlindFire != BlindFire.On || actions.Dodge != Dodge.On || actions.Vault != Vault.On)
            {
                return $"the TPS range rules must turn cover, blind fire, dodge and vault on; rerun {GameRulesAssetBuilder.MenuPath} (got {PlayerActions.FormatLog(actions)})";
            }

            var rig = world.Rig;
            if (rig == null || !rig.IsInitialized)
            {
                return "the player has no initialized ViewRig";
            }

            if (rig.Mode != ViewMode.ThirdPerson)
            {
                return $"the TPS range must start in third person (got {rig.Mode}); rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            _step = Step.EnterCover;
            return null;
        }

        private static string EnterLowCover(BootstrapWorld world, CoverController cover)
        {
            var wall = FindCollider(TpsRangeSceneBuilder.CoversName + "/" + TpsRangeSceneBuilder.LowCoverName, out var problem);
            if (wall == null)
            {
                return problem;
            }

            // 遮蔽物の手前 (-Z 側) の面の真ん中に、+Z (遮蔽物) を向いて立たせる。
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
                return $"'{TpsRangeSceneBuilder.LowCoverName}' was judged as a {cover.CoverHeight} cover";
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

            _step = Step.PlaceAtVaultWall;
            return null;
        }

        private static string PlaceAtVaultWall(BootstrapWorld world, CoverController cover)
        {
            cover.ExitCover(ExitReason);
            if (cover.IsInCover)
            {
                return "the player could not leave the cover";
            }

            var wall = FindCollider(TpsRangeSceneBuilder.VaultWallName, out var problem);
            if (wall == null)
            {
                return problem;
            }

            var bounds = wall.bounds;
            if (bounds.max.y > world.Mover.Motor.Spec.VaultMaxHeight)
            {
                return $"'{TpsRangeSceneBuilder.VaultWallName}' is {bounds.max.y.ToString("0.##", CultureInfo.InvariantCulture)}m high, above the vault limit {world.Mover.Motor.Spec.VaultMaxHeight.ToString("0.##", CultureInfo.InvariantCulture)}m";
            }

            var standOff = GameBootstrap.PlayerRadius + StandOffFromVaultWall;
            Teleport(world.Controller, new Vector3(bounds.center.x, GameBootstrap.PlayerSpawn.y, bounds.min.z - standOff));
            FaceForward(world);

            // カバーの差し込み (カバー外では何もしない) を残したまま、前進とジャンプを足す。
            _vaultFilter = new ForwardJumpFilter(world.Mover.MoveFilter, world.Player.transform, bounds.max.z + GameBootstrap.PlayerRadius);
            world.Mover.MoveFilter = _vaultFilter;
            _step = Step.CrossVaultWall;
            return null;
        }

        private static string CrossVaultWall(BootstrapWorld world)
        {
            if (_vaultFilter == null || !ReferenceEquals(world.Mover.MoveFilter, _vaultFilter))
            {
                return "something replaced the move filter while crossing the vault wall";
            }

            if (!_vaultFilter.IsPast || !world.Mover.IsGrounded)
            {
                return null;
            }

            world.Mover.MoveFilter = _vaultFilter.Inner;
            _vaultFilter = null;
            _crossedVaultWall = true;
            _step = Step.PlaceInOpenArea;
            return null;
        }

        private static string PlaceInOpenArea(BootstrapWorld world)
        {
            var area = GameObject.Find(TpsRangeSceneBuilder.OpenAreaName);
            if (area == null)
            {
                return $"the scene has no '{TpsRangeSceneBuilder.OpenAreaName}'; rerun {TpsRangeSceneBuilder.MenuPath}";
            }

            var center = area.transform.position;
            Teleport(world.Controller, new Vector3(center.x, center.y + GameBootstrap.PlayerSpawn.y, center.z));
            FaceForward(world);
            _step = Step.StartDodge;
            return null;
        }

        private static string StartDodge(BootstrapWorld world, WeaponController weapon)
        {
            var mover = world.Mover;

            // 移した直後は CharacterController の接地が前の位置のままなので、次の移動で床に着くまで待つ。
            if (!mover.IsGrounded || (mover.DodgeMotion != null && !mover.DodgeMotion.IsReady))
            {
                return null;
            }

            _dodgeFrom = world.Player.transform.position;
            if (!mover.TryStartDodge(Vector2.right))
            {
                return $"the player could not dodge in the open area (rule {mover.DodgeRule}, grounded={mover.IsGrounded}, vaulting={mover.IsVaulting})";
            }

            if (!_sawDodgeStart)
            {
                return $"the dodge logged no '{_dodgeStartLog}' line";
            }

            if (weapon.PullTrigger(true, true))
            {
                return "the weapon fired while dodging";
            }

            _dodgeBlockedFire = true;
            _step = Step.WaitDodgeEnd;
            return null;
        }

        private static string WaitDodgeEnd(BootstrapWorld world, WeaponController weapon)
        {
            var mover = world.Mover;
            if (mover.IsDodging)
            {
                return weapon.PullTrigger(true, true) ? "the weapon fired while dodging" : null;
            }

            if (!_sawDodgeEnd)
            {
                return $"the dodge ended but no '{_dodgeEndLog}' line was logged";
            }

            var moved = world.Player.transform.position - _dodgeFrom;
            moved.y = 0f;
            var minimum = mover.Motor.Spec.DodgeDistance * MinDodgeDistanceRatio;
            if (moved.magnitude < minimum)
            {
                return $"the dodge moved the player only {moved.magnitude.ToString("0.##", CultureInfo.InvariantCulture)}m (expected at least {minimum.ToString("0.##", CultureInfo.InvariantCulture)}m)";
            }

            if (Vector3.Dot(moved, world.Player.transform.right) <= 0f)
            {
                return $"the dodge to the right moved the player by {Format(moved)}";
            }

            _step = Step.FireAfterDodge;
            return null;
        }

        private static string FireAfterDodge(WeaponController weapon)
        {
            // 連射間隔は回避の長さより短いが、リロード中だと撃てないので 1 フレームずつ待つ (上限は ExerciseTimeoutSeconds)。
            if (weapon.Weapon.IsReloading)
            {
                return null;
            }

            if (!weapon.PullTrigger(true, true))
            {
                return null;
            }

            _step = Step.Done;
            return null;
        }

        private static Collider FindCollider(string path, out string problem)
        {
            var found = GameObject.Find(path);
            var collider = found != null ? found.GetComponent<Collider>() : null;
            problem = collider == null ? $"the scene has no '{path}' with a collider; rerun {TpsRangeSceneBuilder.MenuPath}" : null;
            return collider;
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
                $"{LogTag} step={_step} cover enter={_sawCoverEnter} blind fire={_sawBlindFire} vault crossed={_crossedVaultWall} dodge start={_sawDodgeStart} dodge end={_sawDodgeEnd} dodge blocked fire={_dodgeBlockedFire} errors={_errors.Count.ToString(CultureInfo.InvariantCulture)}");

            var passed = problems.Count == 0
                && _step == Step.Done
                && _sawCoverEnter
                && _sawBlindFire
                && _crossedVaultWall
                && _sawDodgeStart
                && _sawDodgeEnd
                && _dodgeBlockedFire;
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

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
            else if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
        }

        /// <summary>
        /// 乗り越えの手順で、入力の代わりに前進とジャンプを押し続ける差し込み。体が <see cref="_pastZ"/> を越えたらジャンプをやめて降りさせる。
        /// 先に差し込まれていたもの (カバー) は後から呼び、カバーの制限を上書きしない。
        /// </summary>
        private sealed class ForwardJumpFilter : IPlayerMoveFilter
        {
            private readonly Transform _body;
            private readonly float _pastZ;

            public ForwardJumpFilter(IPlayerMoveFilter inner, Transform body, float pastZ)
            {
                Inner = inner;
                _body = body ?? throw new ArgumentNullException(nameof(body));
                _pastZ = pastZ;
            }

            public IPlayerMoveFilter Inner { get; }

            public bool IsPast => _body.position.z > _pastZ;

            public void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed)
            {
                move = Vector2.up;
                sprint = false;
                crouch = false;
                jumpPressed = !IsPast;
                Inner?.Filter(ref move, ref sprint, ref crouch, ref jumpPressed);
            }
        }
    }
}
