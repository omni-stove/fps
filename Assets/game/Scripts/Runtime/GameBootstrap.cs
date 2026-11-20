using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed partial class GameBootstrap : MonoBehaviour
    {
        public const string ReadyLog = "[Bootstrap] ready";

        public static string FormatOverrideLog(string definitionName)
        {
            var replaced = string.IsNullOrEmpty(definitionName) ? "(none)" : "'" + definitionName + "'";
            return $"[Bootstrap] rules overridden by GameRulesOverride; scene rules {replaced} are ignored";
        }

        public const string FloorName = "Floor";
        public const string PlayerName = "Player";

        public const string PlayerTag = "Player";
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

        [SerializeField, Tooltip("プレイヤーの WeaponController に渡す武器の定義の並び (既定のロードアウト)。空ならロードアウトのスロットの武器")]
        private WeaponDefinition[] _weaponDefinitions = Array.Empty<WeaponDefinition>();

        [SerializeField, Tooltip("プレイヤーのロードアウト。持ち替え秒数と、武器・能力の並びが空のときの武器・能力を引く。空なら入口 (GameContent) のロードアウト")]
        private LoadoutDefinition _loadout;

        [SerializeField, Tooltip("プレイヤーの自動回復と予備弾のルール。空なら入口 (GameContent) のルール、それも無ければ GameRules.Default を使い警告を出す")]
        private GameRulesDefinition _rules;

        [SerializeField, Tooltip("ルールの上にこの順で重ねるミューテーターの Id (mutators.json の id)。GameRulesOverride に積んだものはこの後に重ねる。空なら重ねない")]
        private string[] _mutatorIds = Array.Empty<string>();

        [SerializeField, Tooltip("プレイヤーの能力の枠 (AbilityHost) に入れる能力。i 番目が i 番目の枠 (0 が Ability1)。空ならロードアウトの能力。使えるかはルールの Abilities で決まる")]
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

        [SerializeField, Min(0f), Tooltip("三人称の覗き込みでカメラが目へ寄る間、照準の下に保つ点の、目から最も近い距離 (m)。これより近い点はこの距離にあるものとして保つ")]
        private float _aimHoldMinDistance = AimHold.DefaultMinDistance;

        [SerializeField, Min(0f), Tooltip("腰だめから覗き込み切るまで (と戻るまで) の秒数")]
        private float _adsTransitionSeconds = AdsZoom.DefaultTransitionSeconds;

        [SerializeField, Tooltip("このシーンのプレイヤーの移動の調整値。空なら入口 (GameContent) の Motor、Resources の MotorSettings、既定の値の順に引く")]
        private MotorSettings _motorSettings;

        [SerializeField, Tooltip("このシーンのプレイヤーの泳ぎと壁走りの調整値 (traversal の JSON)。空なら入口 (GameContent) か Resources の traversal.json。JSON に書いていない項目はキットの既定の値")]
        private TextAsset _traversalTuning;

        private LoadoutPreferenceStore _loadoutPreferences = new LoadoutPreferenceStore();
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

        public LoadoutDefinition Loadout
        {
            get => _loadout;
            set => _loadout = value;
        }

        public LoadoutDefinition EffectiveLoadout => GameContent.Pick(_loadout, c => c.Loadout);

        public float EffectiveSwitchSeconds
        {
            get
            {
                var loadout = EffectiveLoadout;
                return loadout != null ? loadout.SwitchSeconds : LoadoutDefinition.DefaultSwitchSeconds;
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

                var loadout = EffectiveLoadout;
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

        public IReadOnlyList<AbilityDefinition> EffectiveAbilityDefinitions
        {
            get
            {
                var wired = AbilityDefinitions;
                if (wired.Count > 0)
                {
                    return wired;
                }

                var loadout = EffectiveLoadout;
                return loadout != null ? loadout.Abilities : wired;
            }
        }

        // 装備画面で保存した選択の読み先。null なら保存した選択を読まず、シーンとロードアウトの並びのまま組み立てる
        public LoadoutPreferenceStore LoadoutPreferences
        {
            get => _loadoutPreferences;
            set => _loadoutPreferences = value;
        }

        // 組み立てたときにプレイヤーへ渡した武器と能力。組み立てる前は null
        public AppliedLoadout PlayerLoadout { get; private set; }

        public GameRulesDefinition RulesDefinition
        {
            get => _rules;
            set => _rules = value;
        }

        public IReadOnlyList<string> MutatorIds
        {
            get => _mutatorIds ?? Array.Empty<string>();
            set => _mutatorIds = value == null ? Array.Empty<string>() : new List<string>(value).ToArray();
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

        public float AimHoldMinDistance
        {
            get => _aimHoldMinDistance;
            set => _aimHoldMinDistance = ValidateNonNegativeFinite(value, nameof(value));
        }

        public float AdsTransitionSeconds
        {
            get => _adsTransitionSeconds;
            set => _adsTransitionSeconds = AdsZoom.ValidateTransitionSeconds(value, nameof(value));
        }

        public MotorSettings MotorSettings
        {
            get => _motorSettings;
            set => _motorSettings = value;
        }

        // TraversalTuning や Traversal と名付けないのは、同じ名前の静的クラス (ゲーム全体の traversal の引き先) と名前空間をこのクラスの中から指せなくなるため。
        public TextAsset TraversalTuningJson
        {
            get => _traversalTuning;
            set => _traversalTuning = value;
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
            var playerLoadout = ResolvePlayerLoadout();

            GameObject floor = null;
            GameObject player = null;
            GameObject light = null;
            GameObject targets = null;
            try
            {
                floor = _createFloor ? CreateFloor(_floorPrefab) : null;
                player = CreatePlayer(
                    playerLoadout.Weapons, playerLoadout.Abilities, rules, EffectiveSwitchSeconds, tuning, out var controller, out var rig, out var camera, out var look, out var mover,
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

            PlayerLoadout = playerLoadout;
            PublishSignals(_world);
            Debug.Log(ReadyLog, this);
            NotifyBuilt(_world);
            return _world;
        }

        // 能力をシーンに直接つないだシーン (射撃場など) は、そのシーンで試させる並びなので、保存した選択で置き換えない
        private AppliedLoadout ResolvePlayerLoadout()
        {
            var weapons = EffectiveWeaponDefinitions;
            var abilities = EffectiveAbilityDefinitions;
            if (AbilityDefinitions.Count > 0 || _loadoutPreferences == null)
            {
                return new AppliedLoadout(weapons, abilities, false, false);
            }

            return LoadoutPreferenceApplier.Apply(
                LoadSavedLoadout(),
                EffectiveLoadout,
                GameContent.Pick(c => c.WeaponCatalog),
                GameContent.Pick(c => c.AbilityCatalog),
                weapons,
                abilities,
                this);
        }

        // 読めない保存で試合を始められなくしないよう、保存が無いときと同じく既定の並びにする
        private LoadoutChoice LoadSavedLoadout()
        {
            try
            {
                return _loadoutPreferences.Load();
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                Debug.LogWarning(LoadoutPreferenceLog.NotApplied(e.Message), this);
                return null;
            }
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
            var baseRules = ResolveBaseRules(out configured);
            var mutators = SceneMutators();
            mutators.AddRange(GameRulesOverride.Mutators);
            if (mutators.Count == 0)
            {
                return baseRules;
            }

            var rules = GameRulesOverride.ApplyMutators(baseRules, mutators);

            // ルールが無いときは Inspector の最大体力を使うが、ミューテーターが体力を書き換えたならそれを効かせる。
            configured |= rules.PlayerMaxHealth != baseRules.PlayerMaxHealth;
            return rules;
        }

        private GameRules ResolveBaseRules(out bool configured)
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

        // 知らない Id で試合を止めず、エラーを出してその 1 つだけを飛ばす (GameRulesOverride.ApplyMutators と同じ扱い)。
        private List<GameRulesMutator> SceneMutators()
        {
            var mutators = new List<GameRulesMutator>();
            var ids = MutatorIds;
            if (ids.Count == 0)
            {
                return mutators;
            }

            var available = KitTuning.Mutators;
            foreach (var id in ids)
            {
                var mutator = GameRulesMutator.Find(available, id);
                if (mutator == null)
                {
                    Debug.LogError(MutatorLog.UnknownId(id, available), this);
                    continue;
                }

                mutators.Add(mutator);
            }

            return mutators;
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
                ValidateNonNegativeFinite(_aimHoldMinDistance, nameof(AimHoldMinDistance)),
                AdsZoom.ValidateTransitionSeconds(_adsTransitionSeconds, nameof(AdsTransitionSeconds)),
                _motorSettings,
                ResolveTraversal());
        }

        // 読めない JSON で試合を止めず、ほかの調整値と同じく (KitTuning) エラーを出してゲーム全体の traversal の値で続ける。
        private TraversalSpec ResolveTraversal()
        {
            if (_traversalTuning == null)
            {
                return null;
            }

            return KitTuning.Read(_traversalTuning.text, _traversalTuning.name, TraversalJson.Parse, TraversalTuning.Current);
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
    }
}
