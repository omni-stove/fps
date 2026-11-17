using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public const string ReadyLog = "[Bootstrap] ready";

        public static string FormatOverrideLog(string definitionName)
        {
            var replaced = string.IsNullOrEmpty(definitionName) ? "(none)" : "'" + definitionName + "'";
            return $"[Bootstrap] rules overridden by GameRulesOverride; scene rules {replaced} are ignored";
        }

        public const string FloorName = "Floor";
        public const string PlayerName = "Player";

        public const string PlayerTag = ObjectiveAreaBase.DefaultPlayerTag;
        public const string CameraName = "PlayerCamera";

        public const string EyeName = "PlayerEye";
        public const string LightName = "Directional Light";

        // Plane は 10m 四方なので、5 倍で 50m 四方にする。
        public static readonly Vector3 FloorScale = new Vector3(5f, 1f, 5f);

        public const float PlayerHeight = 2f;
        public const float PlayerRadius = 0.5f;

        // CharacterController の中心はカプセルの中心なので、足元が床 (y = 0) に接するよう高さの半分だけ上げる。
        public static readonly Vector3 PlayerSpawn = new Vector3(0f, PlayerHeight / 2f, 0f);

        // カプセル中心から 0.6m 上で、足元から 1.6m の目の高さになる。
        public static readonly Vector3 EyeOffset = new Vector3(0f, 0.6f, 0f);

        public static readonly Vector3 LightEuler = new Vector3(50f, -30f, 0f);

        public const string TargetsName = "TargetDummies";

        public const string TargetDummyNamePrefix = "TargetDummy";

        public const int TargetDummyCount = 5;

        public const float TargetDummyDistance = 10f;

        // 胴の直径 1m に対して 1m の隙間を空ける。
        public const float TargetDummySpacing = 2f;

        public const int TargetDummyMaxHealth = TargetDummy.DefaultMaxHealth;
        public const float TargetDummyRespawnDelay = TargetDummy.DefaultRespawnDelay;

        [SerializeField, Tooltip("床を生成するか。NavMesh を焼いた床を持つシーンでは切る")]
        private bool _createFloor = true;

        [SerializeField, Tooltip("プレイヤーの WeaponController に渡す武器の定義の並び (既定のロードアウト)。空なら入口 (GameContent) のロードアウトのスロットの武器")]
        private WeaponDefinition[] _weaponDefinitions = Array.Empty<WeaponDefinition>();

        [SerializeField, Tooltip("プレイヤーの自動回復と予備弾のルール。空なら入口 (GameContent) のルール、それも無ければ GameRules.Default を使い警告を出す")]
        private GameRulesDefinition _rules;

        [SerializeField, Tooltip("プレイヤーの能力の枠 (AbilityHost) に入れる能力。i 番目が i 番目の枠 (0 が Ability1)。使えるかはルールの Abilities で決まる")]
        private AbilityDefinition[] _abilityDefinitions = Array.Empty<AbilityDefinition>();

        [SerializeField, Tooltip("生成する床。原点が床面の中心で、当たり判定を持たせる。大きさはプレハブのまま。空なら 50m 四方の仮の板を作る")]
        private GameObject _floorPrefab;

        [SerializeField, Tooltip("標的ダミーの見た目。足元が原点、前が +Z。当たり判定は外し、胴と頭の当たり判定は仮の形のまま。空なら仮のカプセルと球を作る")]
        private GameObject _targetDummyPrefab;

        [SerializeField, Tooltip("プレイヤーの自機モック (PlayerMockBody) の胴・頭・銃の見た目。空の部品だけ仮の形を作る")]
        private PlayerMockBodyPrefabs _mockBodyPrefabs = new PlayerMockBodyPrefabs();

        [SerializeField, Tooltip("プレイヤーが投げたグレネードの見た目 (GrenadeThrower に渡す)。空なら仮の球を作る")]
        private GameObject _grenadePrefab;

        [Header("プレイヤー")]
        [SerializeField, Min(1), Tooltip("ルールが無いときのプレイヤーの最大体力。ルールがあればその playerMaxHealth を使う。敵の射撃はプレイヤーの Damageable に届く")]
        private int _playerMaxHealth = Damageable.DefaultMaxHealth;

        [SerializeField, Tooltip("三人称のカメラの位置 (目のローカル座標、m)。z が目から後ろへの距離")]
        private Vector3 _thirdPersonOffset = ViewRig.DefaultThirdPersonOffset;

        [SerializeField, Min(0f), Tooltip("三人称のカメラが壁に寄ったときも、目からこれ以上は近づけない距離 (m)")]
        private float _minBoomLength = CameraBoom.DefaultMinLength;

        [SerializeField, Min(0f), Tooltip("腰だめから覗き込み切るまで (と戻るまで) の秒数")]
        private float _adsTransitionSeconds = AdsZoom.DefaultTransitionSeconds;

        private BootstrapWorld _world;
        private LocalPlayerSignal _publishedPlayer;
        private HudModel _publishedHudModel;
        private HudWeaponBinding _hudWeapon;
        private HudDensity? _publishedDensity;
        private bool _publishedPauseTimeMode;

        public BootstrapWorld World => _world;

        // シーンに置かれた敵や標的は GameBootstrap から組み立てられないので、自分で同じルールを探す。
        public static GameRules FindSceneRules()
        {
            var bootstrap = FindAnyObjectByType<GameBootstrap>();
            return bootstrap != null && bootstrap.World != null ? bootstrap.World.Rules : null;
        }

        public event Action<BootstrapWorld> Built;

        public bool CreatesFloor
        {
            get => _createFloor;
            set => _createFloor = value;
        }

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

        public IReadOnlyList<WeaponDefinition> EffectiveWeaponDefinitions
        {
            get
            {
                var wired = WeaponDefinitions;
                if (wired.Count > 0)
                {
                    return wired;
                }

                var loadout = GameContent.Pick(c => c.Loadout);
                if (loadout == null)
                {
                    return wired;
                }

                var weapons = new List<WeaponDefinition>();
                foreach (var slot in loadout.Slots)
                {
                    if (slot != null && slot.Weapon != null)
                    {
                        weapons.Add(slot.Weapon);
                    }
                }

                return weapons;
            }
        }

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

        public GameRulesDefinition RulesDefinition
        {
            get => _rules;
            set => _rules = value;
        }

        public GameObject FloorPrefab
        {
            get => _floorPrefab;
            set => _floorPrefab = value;
        }

        public GameObject TargetDummyPrefab
        {
            get => _targetDummyPrefab;
            set => _targetDummyPrefab = value;
        }

        public PlayerMockBodyPrefabs MockBodyPrefabs
        {
            get => _mockBodyPrefabs ?? (_mockBodyPrefabs = new PlayerMockBodyPrefabs());
            set => _mockBodyPrefabs = value ?? new PlayerMockBodyPrefabs();
        }

        public GameObject GrenadePrefab
        {
            get => _grenadePrefab;
            set => _grenadePrefab = value;
        }

        public int PlayerMaxHealth
        {
            get => _playerMaxHealth;
            set => _playerMaxHealth = ValidatePlayerMaxHealth(value, nameof(value));
        }

        public Vector3 ThirdPersonOffset
        {
            get => _thirdPersonOffset;
            set => _thirdPersonOffset = ValidateThirdPersonOffset(value, nameof(value));
        }

        public float MinBoomLength
        {
            get => _minBoomLength;
            set => _minBoomLength = ValidateNonNegativeFinite(value, nameof(value));
        }

        public float AdsTransitionSeconds
        {
            get => _adsTransitionSeconds;
            set => _adsTransitionSeconds = AdsZoom.ValidateTransitionSeconds(value, nameof(value));
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

        public BootstrapWorld Build()
        {
            if (_world != null)
            {
                return _world;
            }

            var rules = ResolveRules(out var configured);
            var tuning = ResolvePlayerTuning(configured ? rules : null);

            GameObject floor = null;
            GameObject player = null;
            GameObject light = null;
            GameObject targets = null;
            try
            {
                floor = _createFloor ? CreateFloor(_floorPrefab) : null;
                player = CreatePlayer(
                    EffectiveWeaponDefinitions, AbilityDefinitions, rules, tuning, out var controller, out var rig, out var camera, out var look, out var mover,
                    out var weapon, out var ads, out var playerDamageable, out var regen, out var actionReader, out var grenades, out var deathCamera,
                    out var abilities);
                ApplyPlayerVisuals(player, grenades, MockBodyPrefabs, _grenadePrefab);
                light = CreateLight(out var lightComponent);
                targets = CreateTargetDummies(player.transform, _targetDummyPrefab, out var dummies);
                BindDeathBodies(dummies, rules);
                ApplyZoneDamage(dummies, rules);
                // 残り時間は MatchRunner が試合を始めたときに繋ぐ。
                var hudModel = CreateHudModel(playerDamageable, weapon, player.GetComponent<PlayerVehicleRider>(), out _hudWeapon);
                _world = new BootstrapWorld(
                    floor, player, controller, camera, look, mover, lightComponent, weapon, targets, dummies, playerDamageable,
                    hudModel, rig, ads, rules, regen, actionReader, grenades, deathCamera, abilities);
            }
            catch
            {
                _hudWeapon?.Dispose();
                _hudWeapon = null;
                DestroySafely(targets);
                DestroySafely(light);
                DestroySafely(player);
                DestroySafely(floor);
                throw;
            }

            PublishSignals(_world);
            Debug.Log(ReadyLog, this);
            NotifyBuilt(_world);
            return _world;
        }

        private void OnDestroy()
        {
            ClearSignals();
            if (_world != null)
            {
                ReleaseHudModel(_world, _hudWeapon);
                _hudWeapon = null;
            }
        }

        public void ClearSignals()
        {
            if (_publishedPlayer != null)
            {
                GameSignals.LocalPlayer.ClearIfCurrent(_publishedPlayer);
                _publishedPlayer = null;
            }

            if (_publishedHudModel != null)
            {
                GameSignals.HudModel.ClearIfCurrent(_publishedHudModel);
                _publishedHudModel = null;
            }

            if (_publishedDensity.HasValue)
            {
                GameSignals.HudDensity.ClearIfCurrent(_publishedDensity);
                _publishedDensity = null;
            }

            if (_publishedPauseTimeMode)
            {
                GameSignals.PauseTimeMode.ClearIfCurrent(PauseTimeMode.Freeze);
                _publishedPauseTimeMode = false;
            }
        }

        // プレイヤーを最後に出す。プレイヤーを見て HUD を作る UI が、作ったときから HUD の値と量に合わせられるように
        private void PublishSignals(BootstrapWorld world)
        {
            _publishedHudModel = world.HudModel;
            GameSignals.HudModel.Publish(_publishedHudModel);
            _publishedDensity = world.Rules.HudDensity;
            GameSignals.HudDensity.Publish(_publishedDensity);
            GameSignals.PauseTimeMode.Publish(PauseTimeMode.Freeze);
            _publishedPauseTimeMode = true;
            _publishedPlayer = new LocalPlayerSignal(
                world.Player, world.PlayerDamageable, world.Weapon, world.Ads, world.Mover, world.Grenades, world.Abilities, world.ActionReader);
            GameSignals.LocalPlayer.Publish(_publishedPlayer);
        }

        private GameRules ResolveRules(out bool configured)
        {
            configured = true;
            var overridden = GameRulesOverride.Current;
            if (overridden != null)
            {
                Debug.Log(FormatOverrideLog(_rules != null ? _rules.name : null), this);
                return overridden;
            }

            var definition = GameContent.Pick(_rules, c => c.Rules);
            if (definition == null)
            {
                configured = false;
                Debug.LogWarning(
                    "[Bootstrap] GameRulesDefinition が設定されていないため GameRules.Default を使います。シーンか入口 (GameContent) にルールを設定してください。",
                    this);
                return GameRules.Default;
            }

            return definition.ToRules();
        }

        // Inspector の Min は NaN や無限大を止めないので、作り始める前に確かめる。
        // 最大体力の欄はルールがあれば使わないが、それでも確かめるのは、欄の値が壊れていることをルールを外すまで気づかせないため。
        private PlayerTuning ResolvePlayerTuning(GameRules configuredRules)
        {
            var inspectorMaxHealth = ValidatePlayerMaxHealth(_playerMaxHealth, nameof(PlayerMaxHealth));
            return new PlayerTuning(
                configuredRules != null ? configuredRules.PlayerMaxHealth : inspectorMaxHealth,
                ValidateThirdPersonOffset(_thirdPersonOffset, nameof(ThirdPersonOffset)),
                ValidateNonNegativeFinite(_minBoomLength, nameof(MinBoomLength)),
                AdsZoom.ValidateTransitionSeconds(_adsTransitionSeconds, nameof(AdsTransitionSeconds)));
        }

        private static int ValidatePlayerMaxHealth(int value, string paramName)
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(paramName, value, "the player's max health must be at least 1");
            }

            return value;
        }

        private static Vector3 ValidateThirdPersonOffset(Vector3 value, string paramName)
        {
            if (!IsFinite(value.x) || !IsFinite(value.y) || !IsFinite(value.z))
            {
                throw new ArgumentOutOfRangeException(paramName, value, "the third-person camera offset must be finite");
            }

            return value;
        }

        private static float ValidateNonNegativeFinite(float value, string paramName)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(paramName, value, paramName + " must be a non-negative finite number");
            }

            return value;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
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

        private static GameObject CreateFloor(GameObject prefab)
        {
            if (prefab != null)
            {
                // 床は立つための当たり判定が要るので、プレハブの当たり判定は残す。
                var placed = VisualPrefab.Instantiate(prefab, null, FloorName, stripColliders: false);
                placed.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return placed;
            }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = FloorName;
            floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.transform.localScale = FloorScale;
            return floor;
        }

        // 自機モックは Play 中の最初の LateUpdate で組み立てるので、それより前のここで渡せば間に合う。
        private static void ApplyPlayerVisuals(
            GameObject player, GrenadeThrower grenades, PlayerMockBodyPrefabs mockBodyPrefabs, GameObject grenadePrefab)
        {
            var mockBody = player.GetComponent<PlayerMockBody>();
            if (mockBody != null)
            {
                mockBody.Prefabs = mockBodyPrefabs;
            }

            if (grenades != null)
            {
                grenades.GrenadePrefab = grenadePrefab;
            }
        }

        private static GameObject CreatePlayer(
            IReadOnlyList<WeaponDefinition> weaponDefinitions,
            IReadOnlyList<AbilityDefinition> abilityDefinitions,
            GameRules rules,
            PlayerTuning tuning,
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
                    player, weaponDefinitions, rules, tuning, out controller, out rig, out camera, out look, out mover, out weapon, out ads);

                // 撃たれる側の受け皿。CharacterController のカプセルをそのまま胴の当たり判定にする。
                damageable = player.AddComponent<Damageable>();
                damageable.Initialize(PlayerName, tuning.MaxHealth);
                damageable.ApplyRules(rules);
                damageable.IsLocalPlayer = true;
                player.AddComponent<Hitbox>().Initialize(HitZone.Body, damageable);
                CharacterComponents.Attach(player, throwsGrenades: true);

                // CharacterComponents は敵とネットのプレハブにも付けるので、一人称で手元に出すビューモデルは手元のプレイヤーにだけここで付ける。
                player.AddComponent<ViewmodelPresenter>();
                AttachPlayerFeedback(player, rig, camera, damageable);
                WireDeathAndRespawn(damageable, mover, ads);
                KnockbackMovement.Attach(mover, damageable);

                // 使ったときに足す体力と同じ GameObject に置き、持ち物の重さを武器の重さと一緒に移動の遅さに数える
                var inventory = player.AddComponent<PlayerInventory>();
                inventory.Bind(weapon);
                mover.CarryLoad = new PlayerCarryLoad(weapon, inventory);

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

                mover.BindSprintRelease(weapon, damageable);

                actionReader = AttachActionReader(player, mover, weapon, ads, damageable, rig);

                // LockOnAim・AimAssist と並べて ConfigurePlayer で付けないのは、Play 中は付けた Awake で自分の Damageable を探すため。
                StreakController.Apply(player, rules);

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
            IReadOnlyList<WeaponDefinition> weaponDefinitions,
            GameRules rules,
            PlayerTuning tuning,
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
            rig.ThirdPersonOffset = tuning.ThirdPersonOffset;
            rig.MinBoomLength = tuning.MinBoomLength;
            rig.Mode = rules.ViewMode;
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

            ads.TransitionSeconds = tuning.AdsTransitionSeconds;

            WireAds(ads, weapon, mover);
            WireSpreadAndRecoil(weapon, ads, mover, look);
            WireDodgeAndVault(mover, weapon, ads);
            WireAimToFire(weapon, ads, mover);
            LockOnAim.Attach(player, weapon, ads);
            AimAssist.Attach(player, look, ads, weapon);
            WindUpSound.Attach(player, weapon);

            // 乗り物の座席などが操作を引き取ったら、移動と一緒に撃つ・持ち替える入力も止まるよう、同じ口を見させる。
            weapon.Handoff = mover.Handoff;
            mover.CarryLoad = weapon;
        }

        // 使うかはルールで決まり、Off なら始まらないので、ここは常に配線しておく。
        private static void WireDodgeAndVault(PlayerController mover, WeaponController weapon, AimDownSights ads)
        {
            if (mover == null)
            {
                return;
            }

            if (weapon != null)
            {
                weapon.FireBlocker = () => mover != null && (mover.IsDodging || mover.IsVaulting || mover.IsMantling || mover.IsSwimming);
            }

            if (ads == null)
            {
                return;
            }

            mover.DodgeStarted += _ => ads.Release();
            mover.VaultStarted += _ => ads.Release();
            mover.MantleStarted += _ => ads.Release();
            mover.SwimmingChanged += swimming =>
            {
                if (swimming)
                {
                    ads.Release();
                }
            };
            ads.AimingChanged += aiming =>
            {
                if (aiming
                    && ((mover != null && (mover.IsDodging || mover.IsVaulting || mover.IsMantling || mover.IsSwimming))
                        || (weapon != null && weapon.IsHolstered)))
                {
                    ads.Release();
                }
            };
        }

        // ルールを撃つときに WeaponController.Rules から読むのは、キャンペーンが差し替えたルールにも従うため。
        // WireDodgeAndVault の射撃の止め方に足すので、その後に呼ぶ。
        private static void WireAimToFire(WeaponController weapon, AimDownSights ads, PlayerController mover)
        {
            if (weapon == null)
            {
                return;
            }

            BlindFireController blindFire = null;
            var previous = weapon.FireBlocker;
            weapon.FireBlocker = () =>
            {
                if (previous != null && previous())
                {
                    return true;
                }

                if (weapon == null || weapon.Rules.Actions.AimToFire != AimToFire.On || (ads != null && ads.IsAiming))
                {
                    return false;
                }

                // BlindFireController はこの後 PlayerActions.Attach が付けるので、撃つときに引く
                if (blindFire == null && mover != null)
                {
                    blindFire = mover.GetComponent<BlindFireController>();
                }

                return blindFire == null || !blindFire.IsBlindFiring;
            };
        }

        // 投げられるかはルールで決まり、Off なら投げないので、ここは常に配線しておく。
        // WireDodgeAndVault の射撃の止め方に足すので、その後に呼ぶ。
        private static void WireGrenades(GrenadeThrower grenades, PlayerController mover, WeaponController weapon, AimDownSights ads)
        {
            if (grenades == null)
            {
                return;
            }

            grenades.ThrowBlocker = () =>
                (mover != null && (mover.IsDodging || mover.IsVaulting || mover.IsMantling)) || (weapon != null && weapon.IsHolstered);

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

        // 読むのは実行時なので、未初期化のことがある Edit Mode でも配線だけしておく。
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
            weapon.SpreadScaleSource = () => mover != null ? mover.SpreadScale : 1f;

            if (look != null)
            {
                look.Recoil = weapon.Recoil;
            }
        }

        // Edit Mode ではどれも未初期化のままのことがあるが、読むのは実行時なので配線だけしておく。
        private static void WireAds(AimDownSights ads, WeaponController weapon, PlayerController mover)
        {
            ads.WeaponSource = () => weapon != null && weapon.Weapon != null ? weapon.Weapon.Spec : null;
            // 泳ぎの状態は CharacterMotorState.CanAimDownSights と同じ答えにする (ネット対戦の予測と同じく、水の中では構えない)。
            ads.AdsBlocker = () =>
                (weapon != null && weapon.Weapon != null && weapon.Weapon.Trigger.BlocksAds)
                || (mover != null && mover.TryGetMotorState(out var motor) && !motor.CanAimDownSights);
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

        // カバーとブラインドファイア (PlayerActions.Attach) を読み元にするので、その後に呼ぶ。
        // Play 中は Reader の Awake が自力で読み元を解決するので、解決できなかったら失敗にする。Edit Mode では Awake が走らないのでここで渡す。
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

            player.AddComponent<PlayerMockBody>().Bind(reader);
            return reader;
        }

        // 手元のプレイヤーにだけ付ける。敵とネットのプレハブにも付く CharacterComponents に入れると、地図・振動・スティック・水中の画面が操作していない体の分まで動くため。
        // Play 中は付けた Awake / OnEnable が自力で参照を解決するが、Edit Mode では走らないので、要る参照はここで渡す。
        private static void AttachPlayerFeedback(GameObject player, ViewRig rig, Camera camera, Damageable damageable)
        {
            player.AddComponent<PlayerMotorStateSource>();
            player.AddComponent<MinimapPublisher>();

            var rumble = player.AddComponent<GamepadRumble>();
            if (rumble.Rig == null && rig != null)
            {
                rumble.Bind(rig);
            }

            player.AddComponent<StickDeadzoneApplier>();

            var underwater = player.AddComponent<Traversal.UnderwaterPresenter>();
            if (camera != null)
            {
                underwater.Eye = camera.transform;
            }

            underwater.Damageable = damageable;
        }

        // 乗っている間に止める覗き込み・カバー・グレネードを拾うので、それらを付けた後 (PlayerActions.Attach の後) に呼ぶ。
        // Edit Mode では Awake が走らないのでここで渡す。
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

        private static void WireDeathAndRespawn(Damageable damageable, PlayerController mover, AimDownSights ads)
        {
            damageable.Died += _ => ResetStance(mover, ads, CrouchInput.ReasonDeath, ProneInput.ReasonDeath);
            damageable.HealthReset += _ => ResetStance(mover, ads, CrouchInput.ReasonRespawn, ProneInput.ReasonRespawn);
            if (mover != null)
            {
                HoldControlWhileDead(damageable, mover);
            }
        }

        // 部品を止めるモードやキャンペーンが無い場面 (射撃場・機能シーン) でも、倒れたまま撃てないよう操作の口を引き取る。
        // 部品の有効・無効ではなく口で止めるのは、モードの SideMember.SetActing と取り合わないため。
        internal static object HoldControlWhileDead(Damageable damageable, PlayerController mover)
        {
            if (damageable == null)
            {
                throw new ArgumentNullException(nameof(damageable));
            }

            if (mover == null)
            {
                throw new ArgumentNullException(nameof(mover));
            }

            var holder = new DeadControlHold();
            damageable.Died += dead =>
            {
                if (mover != null && mover.Handoff.Claim(holder))
                {
                    Debug.Log(PlayerController.LogTag + " control held while " + dead.Id + " is dead", mover);
                }
            };
            damageable.HealthReset += _ =>
            {
                if (mover != null && mover.Handoff.Release(holder))
                {
                    Debug.Log(PlayerController.LogTag + " control returned to " + damageable.Id, mover);
                }
            };
            return holder;
        }

        private sealed class DeadControlHold
        {
        }

        private static void ResetStance(PlayerController mover, AimDownSights ads, string crouchReason, string proneReason)
        {
            if (mover != null)
            {
                mover.ReleaseCrouch(crouchReason);
                mover.ReleaseProne(proneReason);
            }

            // 専用サーバーや Edit Mode では ADS が初期化されておらず、戻す視野角も無い。
            if (ads != null && ads.IsInitialized)
            {
                ads.ResetView();
            }
        }

        public static string TargetDummyName(int index)
        {
            return TargetDummyNamePrefix + (index + 1);
        }

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

        private static Vector3 HorizontalForward(Quaternion rotation)
        {
            // 水平に取るのは、プレイヤーが傾いていても標的が宙に浮いたり埋まったりしないようにするため。
            var forward = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        private static GameObject CreateTargetDummies(Transform player, GameObject visualPrefab, out TargetDummy[] dummies)
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
                        TargetDummyName(i), positions[i], facing, root.transform, TargetDummyMaxHealth, TargetDummyRespawnDelay, visualPrefab);
                }
            }
            catch
            {
                DestroySafely(root);
                throw;
            }

            return root;
        }

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

        private static void ApplyZoneDamage(IReadOnlyList<TargetDummy> dummies, GameRules rules)
        {
            foreach (var dummy in dummies)
            {
                if (dummy != null && dummy.Damageable != null)
                {
                    dummy.Damageable.ApplyRules(rules);
                }
            }
        }

        private static HudModel CreateHudModel(Damageable player, WeaponController weapon, PlayerVehicleRider rider, out HudWeaponBinding weaponBinding)
        {
            var model = new HudModel();
            if (player != null)
            {
                player.HealthReset += model.BindHealth;
                model.BindHealth(player.Health);
            }

            weaponBinding = new HudWeaponBinding(model, weapon, rider);
            return model;
        }

        private static void ReleaseHudModel(BootstrapWorld world, HudWeaponBinding weaponBinding)
        {
            weaponBinding?.Dispose();
            if (world.PlayerDamageable != null)
            {
                world.PlayerDamageable.HealthReset -= world.HudModel.BindHealth;
            }

            world.HudModel.UnbindAll();
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

        private readonly struct PlayerTuning
        {
            public PlayerTuning(int maxHealth, Vector3 thirdPersonOffset, float minBoomLength, float adsTransitionSeconds)
            {
                MaxHealth = maxHealth;
                ThirdPersonOffset = thirdPersonOffset;
                MinBoomLength = minBoomLength;
                AdsTransitionSeconds = adsTransitionSeconds;
            }

            public int MaxHealth { get; }

            public Vector3 ThirdPersonOffset { get; }

            public float MinBoomLength { get; }

            public float AdsTransitionSeconds { get; }
        }
    }

    public sealed class BootstrapWorld
    {
        public GameObject Floor { get; }
        public GameObject Player { get; }
        public CharacterController Controller { get; }
        public Camera Camera { get; }

        public ViewRig Rig { get; }

        public FirstPersonLook Look { get; }
        public PlayerController Mover { get; }
        public Light Light { get; }
        public WeaponController Weapon { get; }

        public AimDownSights Ads { get; }

        public GameObject TargetRoot { get; }

        public IReadOnlyList<TargetDummy> Targets { get; }

        public Damageable PlayerDamageable { get; }

        public HudModel HudModel { get; }

        public GameRules Rules { get; }

        public HealthRegen Regen { get; }

        public PlayerActionReader ActionReader { get; }

        public GrenadeThrower Grenades { get; }

        public PlayerDeathCamera DeathCamera { get; }

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
            HudModel hudModel,
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
            HudModel = hudModel ?? new HudModel();
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
