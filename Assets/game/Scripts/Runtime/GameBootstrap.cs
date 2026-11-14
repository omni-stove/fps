using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// シーンの起点。Awake で床・プレイヤー (CharacterController + 目 + その子のカメラ + <see cref="ViewRig"/> + <see cref="FirstPersonLook"/>
    /// + <see cref="PlayerController"/> + カメラの <see cref="WeaponController"/> + 覗き込みの <see cref="AimDownSights"/> + 敵に撃たれる <see cref="Damageable"/> と胴の <see cref="Hitbox"/> + 自動回復の <see cref="HealthRegen"/>
    /// + 演出と手榴弾 (<see cref="CharacterComponents.Attach"/>) + 今のアクションをまとめる <see cref="PlayerActionReader"/> と、それを読む
    /// デバッグ表示 (<see cref="IHudFactory.AttachActionDebug"/>)・<see cref="PlayerMockBody"/> + 倒れてから戻るまでのカメラ <see cref="PlayerDeathCamera"/>)
    /// ・ライト・プレイヤーの前方に並べた <see cref="TargetDummyCount"/> 体の <see cref="TargetDummy"/>
    /// (<see cref="Damageable"/> + 頭と胴の <see cref="Hitbox"/>。倒すと 3 秒後に復活する)
    /// ・HUD (<see cref="GameUi.HudFactory"/> に登録された生成役が作る <see cref="IHud"/>) を
    /// コードで生成する。参照はすべてここで解決するので、シーンにはこのコンポーネントを置いた GameObject だけあればよい。
    /// 一人称か三人称かは <see cref="ViewMode"/> で選ぶ (狙いと当たり判定はどちらも目から)。
    /// 自動回復と予備弾の倍率・無限・落下ダメージは <see cref="RulesDefinition"/> のルール (<see cref="GameRulesOverride"/> があればそちら) で決める (標的ダミーには効かせない)。
    /// 標的ダミーのやられ演出 (<see cref="DeathBodyPresenter"/>) も同じルールの <see cref="GameRules.DeathBody"/> で、HUD に出す項目は
    /// <see cref="GameRules.HudDensity"/> (<see cref="IHud.ApplyDensity"/>) で、倒れてから戻るまでに映すものは <see cref="GameRules.DeathCamera"/> で決める。
    /// 生成物は <see cref="World"/> で読め、生成し終えたことは <see cref="Built"/> で受け取れる。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const string ReadyLog = "[Bootstrap] ready";

        public const string NoHudLog = "[Bootstrap] no HUD factory is registered with GameUi.HudFactory; running without a HUD";

        /// <summary>
        /// <see cref="GameRulesOverride"/> のルールを使うときのログ。definitionName は上書きされたシーンのルールの名前で、未設定なら null。
        /// </summary>
        public static string FormatOverrideLog(string definitionName)
        {
            var replaced = string.IsNullOrEmpty(definitionName) ? "(none)" : "'" + definitionName + "'";
            return $"[Bootstrap] rules overridden by GameRulesOverride; scene rules {replaced} are ignored";
        }

        public const string FloorName = "Floor";
        public const string PlayerName = "Player";

        /// <summary>プレイヤーに付ける Unity 組み込みのタグ。ミッションの範囲トリガーはこれでプレイヤーを見分ける。</summary>
        public const string PlayerTag = ObjectiveAreaBase.DefaultPlayerTag;
        public const string CameraName = "PlayerCamera";

        /// <summary>pitch を回し射撃の始点になる、プレイヤーの目の子 GameObject の名前。カメラはこの子。</summary>
        public const string EyeName = "PlayerEye";
        public const string LightName = "Directional Light";

        /// <summary>Plane は 10m 四方なので、5 倍で 50m 四方。</summary>
        public static readonly Vector3 FloorScale = new Vector3(5f, 1f, 5f);

        public const float PlayerHeight = 2f;
        public const float PlayerRadius = 0.5f;

        /// <summary>足元が床 (y = 0) に接する位置。CharacterController の中心はカプセルの中心。</summary>
        public static readonly Vector3 PlayerSpawn = new Vector3(0f, PlayerHeight / 2f, 0f);

        /// <summary>カプセル中心からの目の高さ。足元から 1.6m。</summary>
        public static readonly Vector3 EyeOffset = new Vector3(0f, 0.6f, 0f);

        public static readonly Vector3 LightEuler = new Vector3(50f, -30f, 0f);

        /// <summary>プレイヤーの最大体力。敵 (<see cref="EnemyAgent"/>) の射撃はプレイヤーの <see cref="Damageable"/> に届く。</summary>
        public const int PlayerMaxHealth = 100;

        /// <summary>標的ダミーをまとめる親の名前。</summary>
        public const string TargetsName = "TargetDummies";

        /// <summary>標的ダミーの名前 (ログの識別子) の頭。左から 1, 2, ... を付ける。</summary>
        public const string TargetDummyNamePrefix = "TargetDummy";

        /// <summary>プレイヤーの前方に並べる標的ダミーの数。</summary>
        public const int TargetDummyCount = 5;

        /// <summary>プレイヤーの足元から標的ダミーの列までの前方の距離 (m)。</summary>
        public const float TargetDummyDistance = 10f;

        /// <summary>隣り合う標的ダミーの間隔 (m)。胴の直径 1m に対して 1m の隙間を空ける。</summary>
        public const float TargetDummySpacing = 2f;

        public const int TargetDummyMaxHealth = TargetDummy.DefaultMaxHealth;
        public const float TargetDummyRespawnDelay = TargetDummy.DefaultRespawnDelay;

        public const string HudName = "HUD";

        [SerializeField, Tooltip("床を生成するか。NavMesh を焼いた床を持つシーンでは切る")]
        private bool _createFloor = true;

        [SerializeField, Tooltip("プレイヤーの視点。一人称か三人称 (肩越し)")]
        private ViewMode _viewMode = ViewMode.FirstPerson;

        [SerializeField, Tooltip("プレイヤーの WeaponController に渡す武器の定義の並び (既定のロードアウト)。シーン生成スクリプトが rifle, handgun を配線する")]
        private WeaponDefinition[] _weaponDefinitions = Array.Empty<WeaponDefinition>();

        [SerializeField, Tooltip("プレイヤーの自動回復と予備弾のルール。空なら GameRules.Default を使い警告を出す。シーン生成スクリプトが配線する")]
        private GameRulesDefinition _rules;

        [SerializeField, Tooltip("プレイヤーの能力の枠 (AbilityHost) に入れる能力。i 番目が i 番目の枠 (0 が Ability1)。使えるかはルールの Abilities で決まる")]
        private AbilityDefinition[] _abilityDefinitions = Array.Empty<AbilityDefinition>();

        private BootstrapWorld _world;

        /// <summary>生成済みのオブジェクト。生成前や失敗時は null。</summary>
        public BootstrapWorld World => _world;

        /// <summary>
        /// <see cref="Build"/> が生成し終えたときに、生成物を渡して 1 度だけ呼ばれる。生成に失敗したときは呼ばれない。
        /// 生成は Awake で済むので、購読する側は先に <see cref="World"/> を見て、null のときだけ購読して待つ。
        /// 購読者が例外を投げても生成は失敗にせず、例外をログに出して残りの購読者を呼ぶ。
        /// </summary>
        public event Action<BootstrapWorld> Built;

        /// <summary>
        /// <see cref="Build"/> で床を生成するか。シーン側に床 (NavMeshSurface を焼いたもの) があるときは false にする。
        /// </summary>
        public bool CreatesFloor
        {
            get => _createFloor;
            set => _createFloor = value;
        }

        /// <summary><see cref="Build"/> で作るプレイヤーの視点。生成後に変えるなら <see cref="BootstrapWorld.Rig"/> の Mode を変える。</summary>
        public ViewMode ViewMode
        {
            get => _viewMode;
            set => _viewMode = value;
        }

        /// <summary>
        /// <see cref="Build"/> で作るプレイヤーの <see cref="WeaponController.Definitions"/> (既定のロードアウト)。0 番を持って始める。
        /// Play 中に空なら WeaponController が初期化できず、<see cref="Build"/> は失敗する。null を設定すると空になる。
        /// </summary>
        public IReadOnlyList<WeaponDefinition> WeaponDefinitions
        {
            get => _weaponDefinitions ?? Array.Empty<WeaponDefinition>();
            set
            {
                if (value == null)
                {
                    _weaponDefinitions = Array.Empty<WeaponDefinition>();
                    return;
                }

                // 呼ぶ側が後から並びを書き換えても、配線した中身が変わらないよう写しを持つ
                var copy = new WeaponDefinition[value.Count];
                for (var i = 0; i < copy.Length; i++)
                {
                    copy[i] = value[i];
                }

                _weaponDefinitions = copy;
            }
        }

        /// <summary>
        /// <see cref="Build"/> で作るプレイヤーの能力の枠 (<see cref="AbilityHost"/>) に入れる能力。i 番目が i 番目の枠 (0 が Ability1)。
        /// 枠より多い分は入れない。空の要素はその枠を空にする。null を設定すると空になる。
        /// </summary>
        public IReadOnlyList<AbilityDefinition> AbilityDefinitions
        {
            get => _abilityDefinitions ?? Array.Empty<AbilityDefinition>();
            set
            {
                if (value == null)
                {
                    _abilityDefinitions = Array.Empty<AbilityDefinition>();
                    return;
                }

                // 呼ぶ側が後から並びを書き換えても、配線した中身が変わらないよう写しを持つ
                var copy = new AbilityDefinition[value.Count];
                for (var i = 0; i < copy.Length; i++)
                {
                    copy[i] = value[i];
                }

                _abilityDefinitions = copy;
            }
        }

        /// <summary>
        /// <see cref="Build"/> でプレイヤーに効かせるルール (自動回復の <see cref="HealthRegen"/> と、武器の予備弾) と、標的ダミーのやられ演出。
        /// null なら <see cref="GameRules.Default"/> を使い、Build 時に警告を出す。
        /// </summary>
        public GameRulesDefinition RulesDefinition
        {
            get => _rules;
            set => _rules = value;
        }

        private void Awake()
        {
            try
            {
                Build();
            }
            catch (Exception e)
            {
                Debug.LogError("[Bootstrap] failed to build the scene", this);
                Debug.LogException(e, this);
                enabled = false;
            }
        }

        /// <summary>
        /// 床・プレイヤー・ライト・標的ダミー・HUD を生成する。2 回目以降は何もせず最初の結果を返す。
        /// 途中で失敗したら作りかけのオブジェクトを片付けてから例外を投げ直す。
        /// <see cref="RulesDefinition"/> の値が不正なら何も作らずに <see cref="GameRulesDefinitionException"/> を投げる
        /// (<see cref="GameRulesOverride"/> で上書き中は <see cref="RulesDefinition"/> を読まない)。
        /// </summary>
        public BootstrapWorld Build()
        {
            if (_world != null)
            {
                return _world;
            }

            var rules = ResolveRules();

            GameObject floor = null;
            GameObject player = null;
            GameObject light = null;
            GameObject targets = null;
            GameObject hud = null;
            PanelSettings hudPanelSettings = null;
            try
            {
                floor = _createFloor ? CreateFloor() : null;
                player = CreatePlayer(
                    _viewMode, WeaponDefinitions, AbilityDefinitions, rules, out var controller, out var rig, out var camera, out var look, out var mover,
                    out var weapon, out var ads, out var playerDamageable, out var regen, out var actionReader, out var grenades, out var deathCamera,
                    out var abilities);
                light = CreateLight(out var lightComponent);
                targets = CreateTargetDummies(player.transform, out var dummies);
                BindDeathBodies(dummies, rules);
                // 専用サーバーには画面が無いので HUD を作らない。残り時間は MatchRunner が試合を始めたときに繋ぐ。
                IHud hudComponent = null;
                if (!NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
                {
                    CreateHud(playerDamageable, weapon, grenades, abilities, rules, out hudComponent, out hudPanelSettings, out hud);
                }

                _world = new BootstrapWorld(
                    floor, player, controller, camera, look, mover, lightComponent, weapon, targets, dummies, playerDamageable,
                    hudComponent, hudPanelSettings, rig, ads, rules, regen, actionReader, grenades, deathCamera, abilities);
            }
            catch
            {
                DestroySafely(hud);
                DestroySafely(hudPanelSettings);
                DestroySafely(targets);
                DestroySafely(light);
                DestroySafely(player);
                DestroySafely(floor);
                throw;
            }

            Debug.Log(ReadyLog, this);
            NotifyBuilt(_world);
            return _world;
        }

        /// <summary>
        /// <see cref="GameRulesOverride.Current"/> があればそれを使い、上書き中とログに出す。
        /// 無ければ <see cref="RulesDefinition"/> を変換する。未設定なら警告を出して <see cref="GameRules.Default"/>。
        /// </summary>
        private GameRules ResolveRules()
        {
            var overridden = GameRulesOverride.Current;
            if (overridden != null)
            {
                Debug.Log(FormatOverrideLog(_rules != null ? _rules.name : null), this);
                return overridden;
            }

            if (_rules == null)
            {
                Debug.LogWarning(
                    "[Bootstrap] GameRulesDefinition が設定されていないため GameRules.Default を使います。シーン生成スクリプトでルールを配線してください。",
                    this);
                return GameRules.Default;
            }

            return _rules.ToRules();
        }

        private void NotifyBuilt(BootstrapWorld world)
        {
            var handlers = Built;
            if (handlers == null)
            {
                return;
            }

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<BootstrapWorld>)handler)(world);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        private static GameObject CreateFloor()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = FloorName;
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.transform.localScale = FloorScale;
            return floor;
        }

        private static GameObject CreatePlayer(
            ViewMode viewMode,
            IReadOnlyList<WeaponDefinition> weaponDefinitions,
            IReadOnlyList<AbilityDefinition> abilityDefinitions,
            GameRules rules,
            out CharacterController controller,
            out ViewRig rig,
            out Camera camera,
            out FirstPersonLook look,
            out PlayerController mover,
            out WeaponController weapon,
            out AimDownSights ads,
            out Damageable damageable,
            out HealthRegen regen,
            out PlayerActionReader actionReader,
            out GrenadeThrower grenades,
            out PlayerDeathCamera deathCamera,
            out AbilityHost abilities)
        {
            var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            try
            {
                ConfigurePlayer(
                    player, viewMode, weaponDefinitions, rules, out controller, out rig, out camera, out look, out mover, out weapon, out ads);

                // 撃たれる側の受け皿。CharacterController のカプセルをそのまま胴の当たり判定にする。
                damageable = player.AddComponent<Damageable>();
                damageable.Initialize(PlayerName, PlayerMaxHealth);
                damageable.IsLocalPlayer = true;
                player.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
                CharacterComponents.Attach(player, throwsGrenades: true);

                // CharacterComponents は敵とネットのプレハブにも付けるので、一人称で手元に出すビューモデルは手元のプレイヤーにだけここで付ける。
                player.AddComponent<ViewmodelPresenter>();
                WireDeathAndRespawn(damageable, mover, ads);
                KnockbackMovement.Attach(mover, damageable);

                // 回復しないルール (RegenPerSecond = 0) でも付けておくのは、キャンペーンが難易度のルールに差し替えられるようにするため。
                regen = player.AddComponent<HealthRegen>();
                regen.Bind(damageable, rules);
                grenades = player.GetComponent<GrenadeThrower>();
                WireGrenades(grenades, mover, weapon, ads);

                // Attach が出す rule のログに入れた能力が載るよう、Attach より前に枠へ入れる。
                // ルールが Off でも入れておくのは、キャンペーンや差し替えたルールで On になったときにそのまま使えるようにするため。
                abilities = PlayerActions.GetOrAdd<AbilityHost>(player);
                abilities.Equip(abilityDefinitions);
                PlayerActions.Attach(player, rules);
                AttachVehicleRider(player, mover, weapon);
                mover.ApplyRules(rules);

                // 撃った・倒れた・体力を戻した (キャンペーンの再開) ときにダッシュをやめる。
                mover.BindSprintRelease(weapon, damageable);

                actionReader = AttachActionReader(player, mover, weapon, ads, damageable, rig);

                // ルールが None でも付けておくのは、キャンペーンが難易度のルールに差し替えられるようにするため (HealthRegen と同じ)。
                deathCamera = player.AddComponent<PlayerDeathCamera>();
                deathCamera.ApplyRules(rules);
            }
            catch
            {
                DestroySafely(player);
                throw;
            }

            return player;
        }

        private static void ConfigurePlayer(
            GameObject player,
            ViewMode viewMode,
            IReadOnlyList<WeaponDefinition> weaponDefinitions,
            GameRules rules,
            out CharacterController controller,
            out ViewRig rig,
            out Camera camera,
            out FirstPersonLook look,
            out PlayerController mover,
            out WeaponController weapon,
            out AimDownSights ads)
        {
            mover = null;
            player.name = PlayerName;
            player.tag = PlayerTag;
            player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.identity);

            // CharacterController 自体がカプセルのコライダーなので、プリミティブ付属のコライダーは二重になる。
            DestroySafely(player.GetComponent<CapsuleCollider>());

            controller = player.AddComponent<CharacterController>();
            controller.height = PlayerHeight;
            controller.radius = PlayerRadius;
            controller.center = Vector3.zero;

            // 本体 (yaw) > 目 (pitch、射撃の始点) > カメラ (目からのオフセットは ViewRig が視点ごとに決める)
            var eye = new GameObject(EyeName).transform;
            eye.SetParent(player.transform, false);
            eye.localPosition = EyeOffset;
            eye.localRotation = Quaternion.identity;

            var cameraObject = new GameObject(CameraName);
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(eye, false);
            cameraObject.transform.localPosition = Vector3.zero;
            cameraObject.transform.localRotation = Quaternion.identity;
            camera = cameraObject.AddComponent<Camera>();
            camera.nearClipPlane = 0.05f;
            cameraObject.AddComponent<AudioListener>();

            // 視点を先に決めておくと、Play 中の AddComponent で走る OnEnable が最初からその視点でカメラを置く。
            // Edit Mode では Awake / OnEnable が走らないので、参照と見た目はここで直接整える。
            rig = player.AddComponent<ViewRig>();
            rig.Mode = viewMode;
            if (!rig.IsInitialized)
            {
                rig.Initialize(eye, camera);
            }

            // 一人称なら自分の体が視界に入らないよう影だけ落とし、三人称なら描く。
            rig.ApplyBodyVisibility(true);
            rig.UpdateCameraPosition();

            // カメラと ViewRig を先に置いておくと、Play 中の AddComponent で走る Awake が自力で pitch の軸 (目) を解決できる。
            // Awake が走らない Edit Mode (シーン生成やテスト) では入力の解決を実行時の Awake に任せる。
            look = player.AddComponent<FirstPersonLook>();
            // 専用サーバーでは FirstPersonLook が自分を止めて初期化しないので、未初期化を失敗にしない。
            if (Application.isPlaying && !look.IsInitialized && !NetStartup.IsDedicatedServerBuild)
            {
                throw new InvalidOperationException(
                    "FirstPersonLook could not resolve its camera or Look action; see the error above");
            }

            // FirstPersonLook と CharacterController を先に付けておくと、PlayerController の Awake が自力で参照を解決できる。
            mover = player.AddComponent<PlayerController>();
            if (Application.isPlaying && !mover.IsInitialized)
            {
                throw new InvalidOperationException(
                    "PlayerController could not resolve its references or Move/Sprint/Crouch/Jump actions; see the error above");
            }

            // 射撃は視点の向きで撃つのでカメラに付ける。参照の解決は FirstPersonLook と同じく Play 中の Awake に任せる。
            // Awake は定義から武器を作るので、カメラを一度無効にして付け、定義を渡してから有効にする (そこで Awake が走る)。
            var cameraWasActive = cameraObject.activeSelf;
            cameraObject.SetActive(false);
            try
            {
                weapon = cameraObject.AddComponent<WeaponController>();
                weapon.Definitions = weaponDefinitions;
                weapon.Rules = rules;
            }
            finally
            {
                cameraObject.SetActive(cameraWasActive);
            }

            if (Application.isPlaying && !weapon.IsInitialized)
            {
                throw new InvalidOperationException(
                    "WeaponController could not resolve its WeaponDefinitions, camera or Attack/Reload actions; see the error above");
            }

            // カメラと FirstPersonLook を先に付けておくと、Play 中の AddComponent で走る Awake が自力で参照を解決できる。
            ads = player.AddComponent<AimDownSights>();
            if (Application.isPlaying && !ads.IsInitialized && !NetStartup.IsDedicatedServerBuild)
            {
                throw new InvalidOperationException(
                    "AimDownSights could not resolve its camera, FirstPersonLook or Aim action; see the error above");
            }

            WireAds(ads, weapon, mover);
            WireSpreadAndRecoil(weapon, ads, mover, look);
            WireDodgeAndVault(mover, weapon, ads);

            // 乗り物の座席などが操作を引き取ったら、移動と一緒に撃つ・持ち替える入力も止まるよう、同じ口を見させる。
            weapon.Handoff = mover.Handoff;
        }

        /// <summary>
        /// 回避・乗り越え・崖のぼりの間は撃たせず、覗き込みを解かせる (始めたときと、その最中に覗き込もうとしたとき)。
        /// 使うかはルールで決める (<see cref="PlayerActions.Attach"/>)。Off なら始まらないので、ここは常に配線しておく。
        /// </summary>
        private static void WireDodgeAndVault(PlayerController mover, WeaponController weapon, AimDownSights ads)
        {
            if (mover == null)
            {
                return;
            }

            if (weapon != null)
            {
                weapon.FireBlocker = () => mover != null && (mover.IsDodging || mover.IsVaulting || mover.IsMantling);
            }

            if (ads == null)
            {
                return;
            }

            mover.DodgeStarted += _ => ads.Release();
            mover.VaultStarted += _ => ads.Release();
            mover.MantleStarted += _ => ads.Release();
            ads.AimingChanged += aiming =>
            {
                if (aiming && mover != null && (mover.IsDodging || mover.IsVaulting || mover.IsMantling))
                {
                    ads.Release();
                }
            };
        }

        /// <summary>
        /// 回避・乗り越え・崖のぼりの間はグレネードを投げさせず、投げている間 (<see cref="GrenadeThrower.IsThrowing"/>) は撃たせず、
        /// 投げたら覗き込みを解かせる。投げられるかと数はルールで決める (<see cref="PlayerActions.Attach"/>)。Off なら投げないので、ここは常に配線しておく。
        /// <see cref="WireDodgeAndVault"/> の後に呼ぶ (その射撃の止め方に足す)。
        /// </summary>
        private static void WireGrenades(GrenadeThrower grenades, PlayerController mover, WeaponController weapon, AimDownSights ads)
        {
            if (grenades == null)
            {
                return;
            }

            grenades.ThrowBlocker = () => mover != null && (mover.IsDodging || mover.IsVaulting || mover.IsMantling);

            if (weapon != null)
            {
                var previous = weapon.FireBlocker;
                weapon.FireBlocker = () => (previous != null && previous()) || (grenades != null && grenades.IsThrowing);
            }

            if (ads != null)
            {
                grenades.Thrown += _ => ads.Release();
            }
        }

        /// <summary>
        /// 射撃のばらつきに覗き込みの進み具合と移動の速さ (歩きの速さで足し切る) を読ませ、射撃の反動を視点に足させる。
        /// WireAds と同じく、読むのは実行時なので Edit Mode では配線だけしておく。
        /// </summary>
        private static void WireSpreadAndRecoil(WeaponController weapon, AimDownSights ads, PlayerController mover, FirstPersonLook look)
        {
            if (weapon == null)
            {
                return;
            }

            weapon.AdsProgressSource = () => ads != null ? ads.Progress : 0f;
            weapon.MoveFractionSource = () =>
            {
                var controller = mover != null ? mover.Controller : null;
                if (controller == null)
                {
                    return 0f;
                }

                var velocity = controller.velocity;
                return SpreadPattern.MoveFraction(new Vector2(velocity.x, velocity.z).magnitude, mover.WalkSpeed);
            };

            if (look != null)
            {
                look.Recoil = weapon.Recoil;
            }
        }

        /// <summary>
        /// ADS に今持っている武器の視野角を読ませ、覗き込み始めたらダッシュを解除させ、リロードを始めたときと持ち替えたときに覗き込みをやめさせる。
        /// Edit Mode ではどれも未初期化のままのことがあるが、読むのは実行時なので配線だけしておく。
        /// </summary>
        private static void WireAds(AimDownSights ads, WeaponController weapon, PlayerController mover)
        {
            ads.WeaponSource = () => weapon != null && weapon.Weapon != null ? weapon.Weapon.Spec : null;
            ads.AimingChanged += aiming =>
            {
                if (aiming && mover != null)
                {
                    mover.ReleaseSprint(SprintInput.ReasonAim);
                }
            };

            if (weapon != null)
            {
                weapon.ReloadStarted += _ => ads.Release();
                weapon.WeaponChanged += _ => ads.Release();
            }
        }

        /// <summary>
        /// プレイヤーの今のアクションをまとめる <see cref="PlayerActionReader"/> と、それを読むデバッグ表示 (<see cref="GameUi.AttachActionDebug"/>)・
        /// 自機モック (<see cref="PlayerMockBody"/>) を付けて配線する。カバーとブラインドファイア (<see cref="PlayerActions.Attach"/>) の後に呼ぶ。
        /// Play 中は Reader の Awake が自力で読み元を解決するので、解決できなかったら失敗にする。Edit Mode では Awake が走らないのでここで渡す。
        /// </summary>
        private static PlayerActionReader AttachActionReader(
            GameObject player,
            PlayerController mover,
            WeaponController weapon,
            AimDownSights ads,
            Damageable damageable,
            ViewRig rig)
        {
            var reader = player.AddComponent<PlayerActionReader>();
            if (!reader.IsBound)
            {
                if (Application.isPlaying)
                {
                    throw new InvalidOperationException(
                        "PlayerActionReader could not resolve the player's components; see the error above");
                }

                reader.Bind(
                    mover, weapon, ads, player.GetComponent<CoverController>(), player.GetComponent<BlindFireController>(), damageable, rig);
            }

            GameUi.AttachActionDebug(player, reader);
            player.AddComponent<PlayerMockBody>().Bind(reader);
            return reader;
        }

        /// <summary>
        /// 乗り物に乗る部品 (<see cref="PlayerVehicleRider"/>) を付ける。乗っている間に止める覗き込み・カバー・グレネードを拾うので、
        /// それらを付けた後 (<see cref="PlayerActions.Attach"/> の後) に呼ぶ。Edit Mode では Awake が走らないのでここで渡す。
        /// </summary>
        private static void AttachVehicleRider(GameObject player, PlayerController mover, WeaponController weapon)
        {
            var rider = player.AddComponent<PlayerVehicleRider>();
            if (rider.IsBound)
            {
                return;
            }

            if (Application.isPlaying)
            {
                throw new InvalidOperationException("PlayerVehicleRider could not resolve the player's components; see the error above");
            }

            rider.Bind(mover, weapon);
        }

        /// <summary>
        /// 倒れたときと体力を作り直したとき (キャンペーンの再開) に、しゃがみを解除し、覗き込みを即座に腰だめへ戻す。
        /// </summary>
        private static void WireDeathAndRespawn(Damageable damageable, PlayerController mover, AimDownSights ads)
        {
            damageable.Died += _ => ResetStance(mover, ads, CrouchInput.ReasonDeath);
            damageable.HealthReset += _ => ResetStance(mover, ads, CrouchInput.ReasonRespawn);
        }

        private static void ResetStance(PlayerController mover, AimDownSights ads, string reason)
        {
            if (mover != null)
            {
                mover.ReleaseCrouch(reason);
            }

            // 専用サーバーや Edit Mode では ADS が初期化されておらず、戻す視野角も無い。
            if (ads != null && ads.IsInitialized)
            {
                ads.ResetView();
            }
        }

        /// <summary>index 番目 (0 始まり、左から) の標的ダミーの名前。</summary>
        public static string TargetDummyName(int index)
        {
            return TargetDummyNamePrefix + (index + 1);
        }

        /// <summary>
        /// 標的ダミーの足元の位置。プレイヤーの足元から前方へ <see cref="TargetDummyDistance"/> 進んだ所に、
        /// 横方向へ <see cref="TargetDummySpacing"/> 間隔で、プレイヤーから見て左から順に <see cref="TargetDummyCount"/> 個並べる。
        /// </summary>
        public static Vector3[] TargetDummyPositions(Vector3 playerFeet, Quaternion playerRotation)
        {
            var forward = HorizontalForward(playerRotation);
            var right = Vector3.Cross(Vector3.up, forward);

            var offsets = LineLayout.CenteredOffsets(TargetDummyCount, TargetDummySpacing);
            var positions = new Vector3[offsets.Length];
            for (var i = 0; i < offsets.Length; i++)
            {
                positions[i] = playerFeet + (forward * TargetDummyDistance) + (right * offsets[i]);
            }

            return positions;
        }

        /// <summary>向きの前方を水平面に落とした単位ベクトル。真上か真下を向いていれば +Z。</summary>
        private static Vector3 HorizontalForward(Quaternion rotation)
        {
            // 水平に取るのは、プレイヤーが傾いていても標的が宙に浮いたり埋まったりしないようにするため。
            var forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        private static GameObject CreateTargetDummies(Transform player, out TargetDummy[] dummies)
        {
            var feet = player.position + (Vector3.down * (PlayerHeight / 2f));
            var positions = TargetDummyPositions(feet, player.rotation);

            // 標的はプレイヤーの方を向かせる (並べた方向の逆)。
            var facing = Quaternion.LookRotation(-HorizontalForward(player.rotation), Vector3.up);

            var root = new GameObject(TargetsName);
            try
            {
                dummies = new TargetDummy[positions.Length];
                for (var i = 0; i < positions.Length; i++)
                {
                    dummies[i] = TargetDummy.Create(
                        TargetDummyName(i), positions[i], facing, root.transform, TargetDummyMaxHealth, TargetDummyRespawnDelay);
                }
            }
            catch
            {
                DestroySafely(root);
                throw;
            }

            return root;
        }

        /// <summary>標的ダミーのやられ演出を rules の <see cref="GameRules.DeathBody"/> にする (プレイヤー自身には付けない)。</summary>
        private static void BindDeathBodies(IReadOnlyList<TargetDummy> dummies, GameRules rules)
        {
            var targets = new List<Damageable>(dummies.Count);
            foreach (var dummy in dummies)
            {
                if (dummy != null)
                {
                    targets.Add(dummy.Damageable);
                }
            }

            DeathBodyPresenter.BindAll(targets, rules.DeathBody);
        }

        /// <summary>
        /// HUD を作り (<see cref="GameUi.CreateHud"/>)、プレイヤーの体力と武器を渡し、rules の <see cref="GameRules.HudDensity"/> で出す項目を決める。オフラインなので、ポーズ中は時間を止める
        /// (<see cref="PauseTimeMode.Freeze"/>)。グレネードの残り (<see cref="IHud.AttachGrenadeCount"/>) と能力の枠 (<see cref="IHud.AttachAbilitySlots"/>) も出す。
        /// 生成役が登録されていなければ、既定の UI を外して使う場合もあるので失敗にせず、out をすべて null にして専用サーバーと同じく HUD 無しで進める。
        /// 作った後の設定で失敗したら、呼び出し元が片付けられるよう out に入れてから例外を投げる。
        /// </summary>
        private static void CreateHud(
            Damageable player,
            WeaponController weapon,
            GrenadeThrower grenades,
            AbilityHost abilities,
            GameRules rules,
            out IHud hud,
            out PanelSettings panelSettings,
            out GameObject hudObject)
        {
            hud = GameUi.CreateHud(HudName, player, weapon, null);
            if (hud == null)
            {
                panelSettings = null;
                hudObject = null;
                Debug.Log(NoHudLog);
                return;
            }

            panelSettings = hud.OwnedPanelSettings;
            hudObject = hud.gameObject;
            if (grenades != null)
            {
                hud.AttachGrenadeCount().Show(grenades);
            }

            if (abilities != null)
            {
                hud.AttachAbilitySlots().Show(abilities);
            }

            hud.ApplyDensity(rules.HudDensity);
            hud.SetPauseTimeMode(PauseTimeMode.Freeze);
        }

        private static GameObject CreateLight(out Light light)
        {
            var lightObject = new GameObject(LightName);
            lightObject.transform.rotation = Quaternion.Euler(LightEuler);
            light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            return lightObject;
        }

        private static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }

    /// <summary><see cref="GameBootstrap.Build"/> が生成したオブジェクト。</summary>
    public sealed class BootstrapWorld
    {
        /// <summary>生成した床。<see cref="GameBootstrap.CreatesFloor"/> が false なら null。</summary>
        public GameObject Floor { get; }
        public GameObject Player { get; }
        public CharacterController Controller { get; }
        public Camera Camera { get; }

        /// <summary>プレイヤーの視点 (一人称・三人称) と目。</summary>
        public ViewRig Rig { get; }

        public FirstPersonLook Look { get; }
        public PlayerController Mover { get; }
        public Light Light { get; }
        public WeaponController Weapon { get; }

        /// <summary>プレイヤーの覗き込み (ADS)。</summary>
        public AimDownSights Ads { get; }

        /// <summary>標的ダミーをまとめた親。</summary>
        public GameObject TargetRoot { get; }

        /// <summary>プレイヤーの前方に並べた標的ダミー。プレイヤーから見て左から順。</summary>
        public IReadOnlyList<TargetDummy> Targets { get; }

        /// <summary>プレイヤーの体力。敵の射撃はここに届く。</summary>
        public Damageable PlayerDamageable { get; }

        /// <summary>HUD。専用サーバーと、<see cref="GameUi.HudFactory"/> に生成役が無いときは作らないので null。</summary>
        public IHud Hud { get; }

        /// <summary>生成時にプレイヤーへ効かせたルール。<see cref="GameBootstrap.RulesDefinition"/> が空なら <see cref="GameRules.Default"/>。</summary>
        public GameRules Rules { get; }

        /// <summary>プレイヤーの自動回復。</summary>
        public HealthRegen Regen { get; }

        /// <summary>プレイヤーの今のアクションをまとめたもの。デバッグ表示と自機モックはここから読む。</summary>
        public PlayerActionReader ActionReader { get; }

        /// <summary>プレイヤーのグレネード。投げられるかと数はルールで決まる。</summary>
        public GrenadeThrower Grenades { get; }

        /// <summary>プレイヤーが倒れてから戻るまでのカメラ。何を映すかはルールの <see cref="GameRules.DeathCamera"/> で決まる。</summary>
        public PlayerDeathCamera DeathCamera { get; }

        /// <summary>HUD 用に作った PanelSettings。<see cref="Hud"/> が破棄されるときに一緒に消える。<see cref="Hud"/> が無ければ null。</summary>
        public PanelSettings HudPanelSettings { get; }

        /// <summary>プレイヤーの能力の枠。使えるかはルールの <see cref="ActionRules.Abilities"/> で決まる。</summary>
        public AbilityHost Abilities { get; }

        public BootstrapWorld(
            GameObject floor,
            GameObject player,
            CharacterController controller,
            Camera camera,
            FirstPersonLook look,
            PlayerController mover,
            Light light,
            WeaponController weapon,
            GameObject targetRoot,
            IReadOnlyList<TargetDummy> targets,
            Damageable playerDamageable,
            IHud hud,
            PanelSettings hudPanelSettings,
            ViewRig rig = null,
            AimDownSights ads = null,
            GameRules rules = null,
            HealthRegen regen = null,
            PlayerActionReader actionReader = null,
            GrenadeThrower grenades = null,
            PlayerDeathCamera deathCamera = null,
            AbilityHost abilities = null)
        {
            Abilities = abilities;
            DeathCamera = deathCamera;
            ActionReader = actionReader;
            Grenades = grenades;
            Rig = rig;
            Ads = ads;
            Rules = rules ?? GameRules.Default;
            Regen = regen;
            Hud = hud;
            HudPanelSettings = hudPanelSettings;
            Floor = floor;
            Player = player;
            Controller = controller;
            Camera = camera;
            Look = look;
            Mover = mover;
            Light = light;
            Weapon = weapon;
            TargetRoot = targetRoot;
            Targets = targets ?? throw new ArgumentNullException(nameof(targets));
            PlayerDamageable = playerDamageable;
        }
    }
}
