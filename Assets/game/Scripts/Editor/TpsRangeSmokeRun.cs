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
    /// <item>カバーを出て乗り越えの壁 (<see cref="TpsRangeSceneBuilder.VaultWallName"/>) の手前に立ち、前へ進みながら Jump を押して乗り越え
    /// (<c>[Player] vault=Vault</c> → <c>[Player] vaulting=true</c> → <c>vaulting=false</c>)、壁の向こう側に降りる。
    /// ふつうのジャンプで越えたら失敗にする。</item>
    /// <item>開けた場所 (<see cref="TpsRangeSceneBuilder.OpenAreaName"/>) で右へ回避し (<c>[Player] dodging=true</c> → <c>dodging=false</c>)、
    /// 転がっている間は撃てず、終わったら撃てる。</item>
    /// <item>同じ開けた場所で手榴弾を投げ、残りが <see cref="GameRulesAssetBuilder.TpsRangeGrenadeCount"/> から 1 つ減り
    /// (<c>[Grenade] thrown remaining=</c>)、信管が切れて爆発する (<c>[Grenade] exploded</c>)。</item>
    /// <item>プレイヤーのすぐ前で小さな爆発を起こして巻き込ませ、それまでの射撃と合わせて画面の揺れのトラウマが
    /// 発砲・爆発・被弾のそれぞれで足される (<c>[CameraShake] trauma +… source=fire|explosion|damage</c>)。</item>
    /// <item>足された手順 (<see cref="ITpsActionSmokeStep"/>、<see cref="ExtraSteps"/>) を <see cref="ITpsActionSmokeStep.Order"/> の順に 1 つずつ終える。
    /// 時間の上限は手順ごとの <see cref="ITpsActionSmokeStep.TimeoutSeconds"/>。</item>
    /// </list>
    /// どれかが成り立たないか、Play 中にエラー・例外・Assert のログが出たら終了コード 1、すべて成り立てば 0 で終わる。
    /// <see cref="SmokeSuite"/> には入れていない。Play Mode に入るので <c>-quit</c> は付けずに、Editor を閉じた状態で次のように呼ぶ:
    /// <code>unity -batchmode -projectPath . -executeMethod Fps.Editor.TpsRangeSmokeRun.Run -logFile -</code>
    /// </summary>
    /// <remarks>
    /// batchmode には入力が無いので、Cover / Attack / 移動とジャンプ / Dodge / Grenade の代わりに <see cref="CoverController.Tick"/>・
    /// <see cref="WeaponController.PullTrigger"/>・<see cref="PlayerController.MoveFilter"/> への差し込み・<see cref="PlayerController.TryStartDodge"/>・
    /// <see cref="GrenadeThrower.TryThrow"/> を使う。
    /// 乗り越えは接地しているフレームにだけ Jump を押し、その Jump で <see cref="PlayerController.VaultStarted"/> が呼ばれて
    /// <see cref="PlayerController.IsVaulting"/> が true になることを確かめる。
    /// </remarks>
    [InitializeOnLoad]
    public static class TpsRangeSmokeRun
    {
        public const string LogTag = "[TpsRangeSmokeRun]";

        /// <summary>Play Mode に入ってから操作し始めるまでに待つ秒数。GameBootstrap の Awake と各コンポーネントの初期化を済ませるため。</summary>
        public const float WarmUpSeconds = 1f;

        /// <summary>操作し始めてから、すべての手順が終わるまでに待つ秒数の上限。乗り越えで歩く分と、手榴弾の信管 (<see cref="GrenadeSpec.DefaultFuseSeconds"/>) を見込む。</summary>
        public const float ExerciseTimeoutSeconds = 25f;

        /// <summary>Play Mode に入れないまま、これだけ待ったら失敗にする (秒)。</summary>
        public const float EnterTimeoutSeconds = 120f;

        /// <summary>カバーに入る前に置く位置の、体の表面から遮蔽物の手前の面までの隙間 (m)。<see cref="CoverProbe.MaxEnterDistance"/> に収まる。</summary>
        public const float StandOffFromCover = 0.3f;

        /// <summary>
        /// 乗り越える前に置く位置の、体の表面から壁の手前の面までの隙間 (m)。体の中心から壁までが <see cref="VaultProbe.MaxReach"/> に収まり、
        /// 最初の Jump で乗り越えられる。届かないとふつうのジャンプになり、低い壁はそれでも越えられてしまうので乗り越えを確かめられない。
        /// </summary>
        public const float StandOffFromVaultWall = 0.3f;

        /// <summary>乗り越え始めた向きと、壁を越える向き (+Z) とのなす角の余弦の下限。</summary>
        public const float MinVaultDirectionDot = 0.9f;

        /// <summary>回避で少なくとも動くべき水平の距離の、<see cref="MotorSpec.DodgeDistance"/> に対する割合。床との摩擦や最後のフレームの端数を見込む。</summary>
        public const float MinDodgeDistanceRatio = 0.5f;

        public const float CloseExplosionDistance = 2f;

        public const float CloseExplosionRadius = 4f;

        // 巻き込んだプレイヤーを倒すと被弾の後のリスポーンが手順に割り込むので、倒さない小ささにする。
        public const int CloseExplosionDamage = 10;

        public const int SuccessExitCode = 0;
        public const int FailureExitCode = 1;

        private const string ActiveKey = "Fps.TpsRangeSmokeRun.Active";
        private const string RequestedAtKey = "Fps.TpsRangeSmokeRun.RequestedAt";
        private const string ExitReason = "smoke";

        private static readonly string _coverEnterLog = CoverState.LogTag + " enter";
        private static readonly string _dodgeStartLog = PlayerController.FormatDodgingLog(true);
        private static readonly string _dodgeEndLog = PlayerController.FormatDodgingLog(false);
        private static readonly string _vaultProbeLog = $"{PlayerController.LogTag} vault={VaultVerdict.Vault} ";
        private static readonly string _vaultStartLog = PlayerController.FormatVaultingLog(true);
        private static readonly string _vaultEndLog = PlayerController.FormatVaultingLog(false);
        private static readonly string _grenadeThrownLog = GrenadeThrower.LogTag + " thrown remaining=";
        private static readonly string _grenadeExplodedLog = GrenadeThrower.LogTag + " exploded ";
        private static readonly string _shakeTraumaLog = CameraShake.LogTag + " trauma +";

        private static readonly List<string> _errors = new List<string>();
        private static Step _step;
        private static bool _sawCoverEnter;
        private static bool _sawBlindFire;
        private static bool _sawDodgeStart;
        private static bool _sawDodgeEnd;
        private static bool _crossedVaultWall;
        private static bool _sawVaultProbe;
        private static bool _sawVaultStart;
        private static bool _sawVaultEnd;
        private static bool _vaultStarted;
        private static bool _vaultingOnStart;
        private static Vector3 _vaultDirection;
        private static PlayerController _vaultMover;
        private static bool _dodgeBlockedFire;
        private static double _playStartedAt = -1d;
        private static double _exerciseStartedAt = -1d;
        private static bool _finished;
        private static Vector3 _dodgeFrom;
        private static ForwardJumpFilter _vaultFilter;
        private static bool _sawGrenadeThrown;
        private static bool _sawGrenadeExploded;
        private static bool _grenadeCountDropped;
        private static bool _grenadeExploded;
        private static ThrownGrenade _grenade;
        private static bool _sawFireShake;
        private static bool _sawExplosionShake;
        private static bool _sawDamageShake;
        private static IReadOnlyList<ITpsActionSmokeStep> _extraSteps;
        private static int _extraIndex;
        private static double _extraStartedAt = -1d;

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
            ThrowGrenade,
            WaitGrenadeExplosion,
            CloseExplosion,
            WaitCloseExplosionShake,
            ExtraSteps,
            Done,
        }

        /// <summary>
        /// 読み込み済みのアセンブリから見つけた足された手順を、<see cref="ITpsActionSmokeStep.Order"/> の順に新しく作って返す。
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// 手順の型に public な引数なしのコンストラクターが無い、名前が空、名前が重なる、または <see cref="ITpsActionSmokeStep.TimeoutSeconds"/> が 0 より大きい有限値でない。
        /// </exception>
        public static IReadOnlyList<ITpsActionSmokeStep> ExtraSteps()
        {
            var steps = ExtensionPoints.CreateAll<ITpsActionSmokeStep>(s => s.Order);
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var step in steps)
            {
                var type = step.GetType().FullName;
                if (string.IsNullOrWhiteSpace(step.Name) || !names.Add(step.Name))
                {
                    throw new InvalidOperationException($"{LogTag} {type} needs a name that no other smoke step uses (got '{step.Name}')");
                }

                if (!(step.TimeoutSeconds > 0f) || float.IsInfinity(step.TimeoutSeconds))
                {
                    throw new InvalidOperationException($"{LogTag} {type} must time out after a positive, finite number of seconds (got {step.TimeoutSeconds.ToString(CultureInfo.InvariantCulture)})");
                }
            }

            return steps;
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
                new CameraShakeSettingsAssetStep().Build();
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
            // 再読み込みで _extraSteps が null に戻っていると ForwardLog が足した手順にログを渡せないので、受け始める前に用意する。
            EnsureExtraSteps();
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
            _sawVaultProbe = false;
            _sawVaultStart = false;
            _sawVaultEnd = false;
            _vaultStarted = false;
            _vaultingOnStart = false;
            _vaultDirection = Vector3.zero;
            _vaultMover = null;
            _dodgeBlockedFire = false;
            _playStartedAt = -1d;
            _exerciseStartedAt = -1d;
            _finished = false;
            _dodgeFrom = Vector3.zero;
            _vaultFilter = null;
            _sawGrenadeThrown = false;
            _sawGrenadeExploded = false;
            _grenadeCountDropped = false;
            _grenadeExploded = false;
            _grenade = null;
            _sawFireShake = false;
            _sawExplosionShake = false;
            _sawDamageShake = false;
            _extraSteps = null;
            _extraIndex = 0;
            _extraStartedAt = -1d;
            EnsureExtraSteps();
        }

        // ドメインの再読み込みで静的な値が消えたときも、Play 中の最初の呼び出しで作り直す。
        private static IReadOnlyList<ITpsActionSmokeStep> EnsureExtraSteps()
        {
            if (_extraSteps == null)
            {
                var steps = ExtraSteps();
                foreach (var step in steps)
                {
                    step.Reset();
                }

                _extraSteps = steps;
            }

            return _extraSteps;
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
            else if (condition.StartsWith(_vaultProbeLog, StringComparison.Ordinal))
            {
                _sawVaultProbe = true;
            }
            else if (string.Equals(condition, _vaultStartLog, StringComparison.Ordinal))
            {
                _sawVaultStart = true;
            }
            else if (string.Equals(condition, _vaultEndLog, StringComparison.Ordinal))
            {
                _sawVaultEnd = true;
            }
            else if (condition.StartsWith(_grenadeThrownLog, StringComparison.Ordinal))
            {
                _sawGrenadeThrown = true;
            }
            else if (condition.StartsWith(_grenadeExplodedLog, StringComparison.Ordinal))
            {
                _sawGrenadeExploded = true;
            }
            else if (condition.StartsWith(_shakeTraumaLog, StringComparison.Ordinal))
            {
                _sawFireShake |= HasShakeSource(condition, CameraShake.SourceFire);
                _sawExplosionShake |= HasShakeSource(condition, CameraShake.SourceExplosion);
                _sawDamageShake |= HasShakeSource(condition, CameraShake.SourceDamage);
            }

            ForwardLog(condition);
        }

        private static bool HasShakeSource(string line, string source)
        {
            var field = " source=" + source;
            return line.Contains(field + " ", StringComparison.Ordinal) || line.EndsWith(field, StringComparison.Ordinal);
        }

        // ログの受け取りの中で Debug.Log* を呼ぶと受け取りが入れ子になるので、手順が投げたものはログに出さずに失敗として溜める。
        private static void ForwardLog(string condition)
        {
            if (_extraSteps == null)
            {
                return;
            }

            foreach (var step in _extraSteps)
            {
                try
                {
                    step.OnLog(condition);
                }
                catch (Exception e)
                {
                    _errors.Add($"{e.GetType().Name} in smoke step '{step.Name}' while reading a log: {e.Message}");
                }
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

            // 足された手順は手順ごとの上限で見るので、組み込みの手順の上限には含めない。
            if (problem == null && _step < Step.ExtraSteps && now - _exerciseStartedAt > ExerciseTimeoutSeconds)
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

                case Step.ThrowGrenade:
                    return ThrowGrenade(world);

                case Step.WaitGrenadeExplosion:
                    return WaitGrenadeExplosion();

                case Step.CloseExplosion:
                    return StartCloseExplosion(world);

                case Step.WaitCloseExplosionShake:
                    if (_sawExplosionShake && _sawDamageShake)
                    {
                        _step = Step.ExtraSteps;
                    }

                    return null;

                case Step.ExtraSteps:
                    return AdvanceExtraStep(world);

                default:
                    return null;
            }
        }

        private static string CheckView(BootstrapWorld world)
        {
            var actions = world.Rules.Actions;
            if (actions.Cover != Cover.On
                || actions.BlindFire != BlindFire.On
                || actions.Dodge != Dodge.On
                || actions.Vault != Vault.On
                || actions.Grenade != Grenade.On
                || actions.GrenadeCount != GameRulesAssetBuilder.TpsRangeGrenadeCount)
            {
                return $"the TPS range rules must turn cover, blind fire, dodge, vault and {GameRulesAssetBuilder.TpsRangeGrenadeCount.ToString(CultureInfo.InvariantCulture)} grenades on; rerun {GameRulesAssetBuilder.MenuPath} (got {PlayerActions.FormatLog(actions)})";
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
            var mover = world.Mover;
            _vaultFilter = new ForwardJumpFilter(mover.MoveFilter, mover, bounds.max.z + GameBootstrap.PlayerRadius);
            mover.MoveFilter = _vaultFilter;
            _vaultMover = mover;
            mover.VaultStarted += OnVaultStarted;
            _step = Step.CrossVaultWall;
            return null;
        }

        // IsVaulting は乗り越えている間しか true でなく、OnUpdate (Editor のフレーム) の間隔によっては見逃すので、始まった瞬間に読む。
        private static void OnVaultStarted(Vector3 direction)
        {
            _vaultStarted = true;
            _vaultDirection = direction;
            _vaultingOnStart = _vaultMover != null && _vaultMover.IsVaulting;
        }

        private static void StopWatchingVault()
        {
            if (_vaultMover != null)
            {
                _vaultMover.VaultStarted -= OnVaultStarted;
                _vaultMover = null;
            }
        }

        private static string CrossVaultWall(BootstrapWorld world)
        {
            var mover = world.Mover;
            if (_vaultFilter == null || !ReferenceEquals(mover.MoveFilter, _vaultFilter))
            {
                return "something replaced the move filter while crossing the vault wall";
            }

            // Jump は接地しているフレームにだけ押すので、押したのに乗り越え始めていなければ、そのフレームはふつうのジャンプになっている。
            if (_vaultFilter.JumpedWhileGrounded && !_vaultStarted)
            {
                return $"jumping in front of '{TpsRangeSceneBuilder.VaultWallName}' did not start a vault ({PlayerController.FormatVaultProbeLog(mover.LastVaultProbe)}, crouched={mover.IsCrouched}, dodging={mover.IsDodging})";
            }

            if (!_vaultStarted)
            {
                return null;
            }

            if (!_vaultingOnStart)
            {
                return "VaultStarted was raised while IsVaulting was false";
            }

            if (!_sawVaultProbe || !_sawVaultStart)
            {
                return $"the vault started without logging '{_vaultProbeLog}...' and '{_vaultStartLog}' (probe={_sawVaultProbe} start={_sawVaultStart})";
            }

            if (Vector3.Dot(_vaultDirection, Vector3.forward) < MinVaultDirectionDot)
            {
                return $"the vault started towards {Format(_vaultDirection)}, not over the wall (+Z)";
            }

            if (mover.IsVaulting || !_vaultFilter.IsPast || !mover.IsGrounded)
            {
                return null;
            }

            if (!_sawVaultEnd)
            {
                return $"the vault ended but no '{_vaultEndLog}' line was logged";
            }

            mover.MoveFilter = _vaultFilter.Inner;
            _vaultFilter = null;
            StopWatchingVault();
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

            _step = Step.ThrowGrenade;
            return null;
        }

        private static string ThrowGrenade(BootstrapWorld world)
        {
            var grenades = world.Grenades;
            if (grenades == null || !grenades.isActiveAndEnabled)
            {
                return "the player has no enabled GrenadeThrower";
            }

            if (!grenades.UsesGrenades || grenades.Capacity != GameRulesAssetBuilder.TpsRangeGrenadeCount)
            {
                return $"the player's GrenadeThrower was configured with rule {grenades.Rule} and {grenades.Capacity.ToString(CultureInfo.InvariantCulture)} grenades (expected {Grenade.On} and {GameRulesAssetBuilder.TpsRangeGrenadeCount.ToString(CultureInfo.InvariantCulture)})";
            }

            var before = grenades.Remaining;
            if (before != grenades.Capacity)
            {
                return $"the player had {before.ToString(CultureInfo.InvariantCulture)} of {grenades.Capacity.ToString(CultureInfo.InvariantCulture)} grenades before throwing any";
            }

            var mover = world.Mover;
            if (!grenades.CanThrow)
            {
                return $"the player could not throw a grenade in the open area (throwing={grenades.IsThrowing}, dodging={mover.IsDodging}, vaulting={mover.IsVaulting})";
            }

            grenades.Thrown += OnGrenadeThrown;
            bool thrown;
            try
            {
                thrown = grenades.TryThrow();
            }
            finally
            {
                grenades.Thrown -= OnGrenadeThrown;
            }

            if (!thrown || _grenade == null)
            {
                return $"TryThrow did not throw a grenade (thrown={thrown}, remaining {grenades.Remaining.ToString(CultureInfo.InvariantCulture)})";
            }

            if (grenades.Remaining != before - 1)
            {
                return $"throwing a grenade left {grenades.Remaining.ToString(CultureInfo.InvariantCulture)} of {before.ToString(CultureInfo.InvariantCulture)}";
            }

            _grenadeCountDropped = true;
            if (!_sawGrenadeThrown)
            {
                return $"the throw logged no '{_grenadeThrownLog}' line";
            }

            _step = Step.WaitGrenadeExplosion;
            return null;
        }

        private static void OnGrenadeThrown(ThrownGrenade grenade)
        {
            _grenade = grenade;
            if (grenade != null)
            {
                grenade.Exploded += OnGrenadeExploded;
            }
        }

        // 爆発したフレームのうちにグレネードは消されるので、OnUpdate で HasExploded を読む代わりにイベントで受ける。
        private static void OnGrenadeExploded(ThrownGrenade grenade, GrenadeExplosion explosion)
        {
            grenade.Exploded -= OnGrenadeExploded;
            _grenadeExploded = true;
            _grenade = null;
        }

        private static void StopWatchingGrenade()
        {
            if (_grenade != null)
            {
                _grenade.Exploded -= OnGrenadeExploded;
            }

            _grenade = null;
        }

        private static string WaitGrenadeExplosion()
        {
            if (!_grenadeExploded)
            {
                // Unity の null 比較で、爆発せずに消えた (Destroy された) グレネードも見分ける。
                return _grenade == null ? "the thrown grenade disappeared without exploding" : null;
            }

            if (!_sawGrenadeExploded)
            {
                return $"the grenade exploded but no '{_grenadeExplodedLog}' line was logged";
            }

            _step = Step.CloseExplosion;
            return null;
        }

        // 投げた手榴弾はどこまで転がるか決まらず、揺れの届く距離の外で爆発することもあるので、揺れは確実に届く近さの爆発で確かめる。
        private static string StartCloseExplosion(BootstrapWorld world)
        {
            if (!_sawFireShake)
            {
                return $"the player's shots logged no '{_shakeTraumaLog}... source={CameraShake.SourceFire}' line";
            }

            var shake = world.Player.GetComponent<CameraShake>();
            if (shake == null || !shake.isActiveAndEnabled || !shake.IsLocalView)
            {
                return $"the player has no active CameraShake on the local view; check {nameof(CharacterComponents)}.{nameof(CharacterComponents.Attach)}";
            }

            var body = world.Player.transform;
            var center = body.position + (body.forward * CloseExplosionDistance);
            ThrownGrenade.Spawn(null, center, Vector3.zero, 0f, new ExplosionDamage(CloseExplosionDamage, CloseExplosionRadius), 0);
            _step = Step.WaitCloseExplosionShake;
            return null;
        }

        private static string AdvanceExtraStep(BootstrapWorld world)
        {
            var steps = EnsureExtraSteps();
            if (_extraIndex >= steps.Count)
            {
                _step = Step.Done;
                return null;
            }

            var step = steps[_extraIndex];
            var now = EditorApplication.timeSinceStartup;
            if (_extraStartedAt < 0d)
            {
                _extraStartedAt = now;
            }

            bool done;
            string problem;
            try
            {
                done = step.Advance(world, out problem);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return $"{e.GetType().Name} in smoke step '{step.Name}': {e.Message}";
            }

            if (problem != null)
            {
                return $"smoke step '{step.Name}': {problem}";
            }

            if (!done)
            {
                return now - _extraStartedAt > step.TimeoutSeconds
                    ? $"smoke step '{step.Name}' did not finish within {step.TimeoutSeconds.ToString("0.##", CultureInfo.InvariantCulture)}s"
                    : null;
            }

            _extraIndex++;
            _extraStartedAt = -1d;
            if (_extraIndex >= steps.Count)
            {
                _step = Step.Done;
            }

            return null;
        }

        private static void CleanUpExtraSteps()
        {
            if (_extraSteps == null)
            {
                return;
            }

            foreach (var step in _extraSteps)
            {
                try
                {
                    step.Cleanup();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
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
                $"{LogTag} step={_step} cover enter={_sawCoverEnter} blind fire={_sawBlindFire} vault probe={_sawVaultProbe} vault start={_sawVaultStart} vaulting={_vaultingOnStart} vault end={_sawVaultEnd} vault crossed={_crossedVaultWall} dodge start={_sawDodgeStart} dodge end={_sawDodgeEnd} dodge blocked fire={_dodgeBlockedFire} grenade thrown={_sawGrenadeThrown} grenade count dropped={_grenadeCountDropped} grenade exploded={_grenadeExploded} grenade explosion log={_sawGrenadeExploded} shake fire={_sawFireShake} shake explosion={_sawExplosionShake} shake damage={_sawDamageShake} errors={_errors.Count.ToString(CultureInfo.InvariantCulture)}");

            var passed = problems.Count == 0
                && _step == Step.Done
                && _sawCoverEnter
                && _sawBlindFire
                && _sawVaultProbe
                && _sawVaultStart
                && _vaultingOnStart
                && _sawVaultEnd
                && _crossedVaultWall
                && _sawDodgeStart
                && _sawDodgeEnd
                && _dodgeBlockedFire
                && _sawGrenadeThrown
                && _grenadeCountDropped
                && _grenadeExploded
                && _sawGrenadeExploded
                && _sawFireShake
                && _sawExplosionShake
                && _sawDamageShake;
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
            StopWatchingVault();
            StopWatchingGrenade();
            CleanUpExtraSteps();
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
        /// 乗り越えの手順で、入力の代わりに前進を押し続け、乗り越え始めるまでは接地しているフレームにだけ Jump を押す差し込み。
        /// 乗り越え始めたら Jump をやめ、降りた先でふつうに跳ばないようにする。
        /// 先に差し込まれていたもの (カバー) は後から呼び、カバーの制限を上書きしない。
        /// </summary>
        private sealed class ForwardJumpFilter : IPlayerMoveFilter
        {
            private readonly PlayerController _mover;
            private readonly float _pastZ;

            public ForwardJumpFilter(IPlayerMoveFilter inner, PlayerController mover, float pastZ)
            {
                Inner = inner;
                _mover = mover != null ? mover : throw new ArgumentNullException(nameof(mover));
                _pastZ = pastZ;
            }

            public IPlayerMoveFilter Inner { get; }

            public bool IsPast => _mover.transform.position.z > _pastZ;

            /// <summary>接地しているフレームに Jump を押したか。乗り越えの条件のうち接地はここで満たしているので、押して乗り越えなければふつうのジャンプになっている。</summary>
            public bool JumpedWhileGrounded { get; private set; }

            public void Filter(ref Vector2 move, ref bool sprint, ref bool crouch, ref bool jumpPressed)
            {
                move = Vector2.up;
                sprint = false;
                crouch = false;
                jumpPressed = !_vaultStarted && !IsPast && _mover.IsGrounded;
                Inner?.Filter(ref move, ref sprint, ref crouch, ref jumpPressed);
                if (jumpPressed)
                {
                    JumpedWhileGrounded = true;
                }
            }
        }
    }
}
