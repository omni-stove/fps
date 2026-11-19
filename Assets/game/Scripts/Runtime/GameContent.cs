using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Runtime
{
    // シーンのコンポーネントに直接配線した参照は、シーンごとに変えたいときのために、入口より先に使う。
    // 入口は、配線が空のときにキットが引く「ゲーム全体の中身」で、入口の欄も空なら従来の Resources のパス、それも無ければ既定値で動く。
    [CreateAssetMenu(fileName = "GameContent", menuName = "Shooter/Game Content")]
    public sealed class GameContent : ScriptableObject
    {
        public const string LogTag = "[GameContent]";

        // 名前ではなく型で探すので、ゲームは Resources/ShooterKit/ に好きな名前で 1 つ置けばよい。
        public const string ResourceFolder = "ShooterKit";

        [Header("ルール・移動・敵")]
        [SerializeField, Tooltip("シーンにルールが配線されていないときに使うルール")]
        private GameRulesDefinition _rules;

        [SerializeField, Tooltip("プレイヤーの移動 (オフラインとネット対戦の両方)。空なら Resources/Movement/MotorSettings、それも無ければ既定値")]
        private MotorSettings _motor;

        [SerializeField, Tooltip("敵の種類ごとのデータ。敵にデータが配線されていないとき、種類が同じ最初のものを使う")]
        private List<EnemyDefinition> _enemies = new List<EnemyDefinition>();

        [SerializeField, Tooltip("グレネードの調整値の JSON (Tuning/grenade.json と同じ形)。空なら Resources/Tuning/grenade")]
        private TextAsset _grenadeTuning;

        [SerializeField, Tooltip("印の調整値の JSON (Tuning/ping.json と同じ形)。空なら Resources/Tuning/ping")]
        private TextAsset _pingTuning;

        [SerializeField, Tooltip("吹き飛ばしの調整値の JSON (Tuning/knockback.json と同じ形)。空なら Resources/Tuning/knockback")]
        private TextAsset _knockbackTuning;

        [SerializeField, Tooltip("アイテムと持ち物の決め方の JSON (Tuning/inventory.json と同じ形)。空なら Resources/Tuning/inventory")]
        private TextAsset _inventoryTuning;

        [SerializeField, Tooltip("制圧の調整値の JSON。空なら Resources/Tuning/suppression")]
        private TextAsset _suppressionTuning;

        [SerializeField, Tooltip("分隊の調整値の JSON。空なら Resources/Tuning/squad")]
        private TextAsset _squadTuning;

        [SerializeField, Tooltip("よろめきの調整値の JSON。空なら Resources/Tuning/stagger")]
        private TextAsset _staggerTuning;

        [SerializeField, Tooltip("近接攻撃の調整値の JSON。空なら Resources/Tuning/melee")]
        private TextAsset _meleeTuning;

        [SerializeField, Tooltip("スライディング・パラシュート・ジップラインの調整値の JSON。空なら Resources/Tuning/traversal")]
        private TextAsset _traversalTuning;

        [SerializeField, Tooltip("隠密の調整値の JSON。空なら Resources/Tuning/stealth")]
        private TextAsset _stealthTuning;

        [SerializeField, Tooltip("ボットの調整値の JSON (サーバーの bots.json と同じ形)。空なら Resources/Tuning/bots")]
        private TextAsset _botsTuning;

        [SerializeField, Tooltip("ストリークの調整値の JSON。空なら Resources/Tuning/streaks")]
        private TextAsset _streaksTuning;

        [SerializeField, Tooltip("照準の補助の調整値の JSON。空なら Resources/Tuning/aim-assist")]
        private TextAsset _aimAssistTuning;

        [SerializeField, Tooltip("成長の調整値の JSON。空なら Resources/Tuning/progression")]
        private TextAsset _progressionTuning;

        [SerializeField, Tooltip("実力の値の調整値の JSON。空なら Resources/Tuning/skill")]
        private TextAsset _skillTuning;

        [SerializeField, Tooltip("ミニマップの調整値の JSON。空なら Resources/Tuning/minimap")]
        private TextAsset _minimapTuning;

        [SerializeField, Tooltip("キルのアシストの調整値の JSON。空なら Resources/Tuning/assist")]
        private TextAsset _assistTuning;

        [SerializeField, Tooltip("コントローラーの振動の調整値の JSON。空なら Resources/Tuning/rumble")]
        private TextAsset _rumbleTuning;

        [Header("運営・記録・見た目・天候")]
        [SerializeField, Tooltip("チャットで伏せる語の JSON。空なら Resources/Tuning/chat-words")]
        private TextAsset _chatWordsTuning;

        [SerializeField, Tooltip("実績の一覧の JSON。空なら Resources/Tuning/achievements")]
        private TextAsset _achievementsTuning;

        [SerializeField, Tooltip("リーダーボードの一覧の JSON。空なら Resources/Tuning/leaderboards")]
        private TextAsset _leaderboardsTuning;

        [SerializeField, Tooltip("スキン・迷彩のカタログの JSON。空なら Resources/Tuning/cosmetics")]
        private TextAsset _cosmeticsTuning;

        [SerializeField, Tooltip("エモートの一覧 (Id・表示名のキー・長さ・繰り返すか・動いたら止めるか) の JSON。空なら Resources/Tuning/emotes、それも無ければエモートは無い")]
        private TextAsset _emotesTuning;

        [SerializeField, Tooltip("天候と時刻の組の JSON。空なら Resources/Tuning/weather")]
        private TextAsset _weatherTuning;

        [SerializeField, Tooltip("クラッシュ報告とテレメトリの送り先の JSON。空なら Resources/Tuning/telemetry")]
        private TextAsset _telemetryTuning;

        [Header("Infected・脱出")]
        [SerializeField, Tooltip("Infected の調整値の JSON。空なら Resources/Tuning/infected")]
        private TextAsset _infectedTuning;

        [SerializeField, Tooltip("箱から出る物の表の JSON。空なら Resources/Tuning/loot")]
        private TextAsset _lootTuning;

        [SerializeField, Tooltip("倉庫の広さと最初の中身の JSON。空なら Resources/Tuning/stash")]
        private TextAsset _stashTuning;

        [SerializeField, Tooltip("脱出の調整値の JSON。空なら Resources/Tuning/extraction")]
        private TextAsset _extractionTuning;

        [SerializeField, Tooltip("リプレイの再生の調整値の JSON。空なら Resources/Tuning/replay")]
        private TextAsset _replayTuning;

        [Header("Payload・お金・補給・復帰・ルールの変え方・プレイリスト")]
        [SerializeField, Tooltip("Payload の調整値の JSON。空なら Resources/Tuning/payload")]
        private TextAsset _payloadTuning;

        [SerializeField, Tooltip("お金と購入の調整値の JSON。空なら Resources/Tuning/economy")]
        private TextAsset _economyTuning;

        [SerializeField, Tooltip("補給の投下の調整値の JSON。空なら Resources/Tuning/supply-drops")]
        private TextAsset _supplyDropsTuning;

        [SerializeField, Tooltip("Battle Royale で倒されたときにパラシュートで戻る調整値の JSON。空なら Resources/Tuning/redeploy")]
        private TextAsset _redeployTuning;

        [SerializeField, Tooltip("ルールを変える組 (ミューテーター) の一覧の JSON。空なら Resources/Tuning/mutators")]
        private TextAsset _mutatorsTuning;

        [SerializeField, Tooltip("プレイリスト (モードとマップの巡り方) の JSON。空なら Resources/Tuning/playlists")]
        private TextAsset _playlistsTuning;

        [Header("武器・キャンペーン")]
        [SerializeField, Tooltip("使える武器の一覧")]
        private WeaponCatalogAsset _weaponCatalog;

        [SerializeField, Tooltip("最初に持つ武器")]
        private LoadoutDefinition _loadout;

        // キャンペーンを本体の外から足せるように、型は ScriptableObject にとどめる。入れる型は GameContentSlots に登録した枠が決める。
        [SerializeField, Tooltip("メニューに出すキャンペーンのミッション (MissionCatalog)")]
        private ScriptableObject _missions;

        [Header("モード")]
        [SerializeField]
        private TeamDeathmatchDefinition _teamDeathmatch;

        [SerializeField]
        private DominationDefinition _domination;

        [SerializeField]
        private HardpointDefinition _hardpoint;

        [SerializeField]
        private KillConfirmedDefinition _killConfirmed;

        [SerializeField]
        private SearchAndDestroyDefinition _searchAndDestroy;

        [SerializeField]
        private CaptureTheFlagDefinition _captureTheFlag;

        [SerializeField]
        private HordeDefinition _horde;

        [SerializeField]
        private GunGameLadderDefinition _gunGameLadder;

        [SerializeField]
        private FreeForAllDefinition _freeForAll;

        [SerializeField]
        private GuardianDefinition _guardian;

        [SerializeField]
        private ExecutionDefinition _execution;

        [SerializeField]
        private BattleRoyaleDefinition _battleRoyale;

        [Header("キャラクター・メニュー")]
        [SerializeField, Tooltip("プレイヤー・敵に置く人型のリグ (根に Animator)。空なら Resources/Characters/CharacterRig")]
        private GameObject _characterRig;

        [SerializeField, Tooltip("メニューから移るシーンの名前。空なら Resources/Menu/MenuSceneSettings")]
        private MenuSceneSettings _menuScenes;

        [Header("見た目・音")]
        [SerializeField, Tooltip("武器ごとの音とエフェクト、ヒットマーカー")]
        private PresentationCatalog _presentation;

        [SerializeField]
        private ViewmodelCatalog _viewmodels;

        [SerializeField]
        private WeaponExtrasCatalog _weaponExtras;

        [SerializeField]
        private AttachmentModelCatalog _attachmentModels;

        [SerializeField]
        private HitFeedbackCatalog _hitFeedback;

        [SerializeField]
        private DamageFeedbackCatalog _damageFeedback;

        [SerializeField]
        private SurfaceImpactCatalog _surfaceImpacts;

        [SerializeField]
        private FootstepSurfaceCatalog _footsteps;

        [SerializeField]
        private RagdollCatalog _ragdoll;

        [SerializeField]
        private SuppressionCatalog _suppression;

        // 隠密を本体の外から足せるように、型は ScriptableObject にとどめる。入れる型は GameContentSlots に登録した枠が決める。
        [SerializeField, Tooltip("敵が気づいたときの印 (AwarenessIndicatorCatalog)")]
        private ScriptableObject _awarenessIndicator;

        [SerializeField]
        private CameraShakeSettings _cameraShake;

        [SerializeField, Tooltip("当てたときのダメージの数値。空なら Resources/Presentation/DamageNumberCatalog、それも無ければ出さない")]
        private DamageNumberCatalog _damageNumbers;

        [SerializeField, Tooltip("目からの射線が遮られているときの照準の知らせ方。空なら Resources/Presentation/AimBlockCatalog、それも無ければ既定値で出す")]
        private AimBlockCatalog _aimBlock;

        [SerializeField, Tooltip("アイテムのアイコン (アイコンの Id ごと)。空なら Resources/Presentation/ItemIconCatalog、それも無ければ持ち物の画面が名前の箱を描く")]
        private ItemIconCatalog _itemIcons;

        [SerializeField, Tooltip("ストリークの報酬のアイコン (報酬の Id ごと)。空なら Resources/Presentation/StreakIconCatalog、それも無ければ HUD が名前の箱を描く")]
        private StreakIconCatalog _streakIcons;

        [SerializeField, Tooltip("水中の色・音・水しぶき。空なら Resources/Presentation/WaterCatalog、それも無ければ既定の色だけで覆う")]
        private WaterCatalog _water;

        [SerializeField, Tooltip("照準器とスコープの照準・縁の見た目 (照準の Id ごと)。空なら Resources/Presentation/ReticleCatalog、それも無ければ既定の形を描く")]
        private ReticleCatalog _reticles;

        [SerializeField, Tooltip("状態異常の種類ごとの見た目。空なら Resources/Presentation/StatusEffectVisualsCatalog、それも無ければコードで形を作る")]
        private StatusEffectVisualCatalog _statusEffectVisuals;

        [SerializeField, Tooltip("目くらましの覆いと耳鳴り。空なら Resources/Presentation/FlashOverlayCatalog、それも無ければ既定の色だけで覆う")]
        private FlashOverlayCatalog _flashOverlay;

        [SerializeField, Tooltip("スキンの体と迷彩の材質 (見た目の visualId ごと)。空なら Resources/Presentation/CosmeticVisualCatalog、それも無ければ見た目を替えない")]
        private CosmeticVisualCatalog _cosmeticVisuals;

        [SerializeField, Tooltip("エモートのアニメーション (エモートの Id ごと)。空なら Resources/Presentation/EmoteCatalog、それも無ければ体を動かさずにエモートを出す")]
        private EmoteCatalog _emoteAnimations;

        [SerializeField, Tooltip("暗視と熱源視の覆いと音。空なら Resources/Presentation/VisionVisualsCatalog、それも無ければ既定の色だけで覆う")]
        private VisionVisualCatalog _visionVisuals;

        // ドメインの再読み込みを切っているので、Play の開始時に ResetOnPlay で戻す。
        private static GameContent _override;
        private static GameContent _found;
        private static bool _searched;

        public GameRulesDefinition Rules
        {
            get => _rules;
            set => _rules = value;
        }

        public MotorSettings Motor
        {
            get => _motor;
            set => _motor = value;
        }

        public IReadOnlyList<EnemyDefinition> Enemies => _enemies ??= new List<EnemyDefinition>();

        public TextAsset GrenadeTuning
        {
            get => _grenadeTuning;
            set => _grenadeTuning = value;
        }

        public TextAsset PingTuning
        {
            get => _pingTuning;
            set => _pingTuning = value;
        }

        public TextAsset KnockbackTuning
        {
            get => _knockbackTuning;
            set => _knockbackTuning = value;
        }

        public TextAsset InventoryTuning
        {
            get => _inventoryTuning;
            set => _inventoryTuning = value;
        }

        public TextAsset SuppressionTuning
        {
            get => _suppressionTuning;
            set => _suppressionTuning = value;
        }

        public TextAsset SquadTuning
        {
            get => _squadTuning;
            set => _squadTuning = value;
        }

        public TextAsset StaggerTuning
        {
            get => _staggerTuning;
            set => _staggerTuning = value;
        }

        public TextAsset MeleeTuning
        {
            get => _meleeTuning;
            set => _meleeTuning = value;
        }

        public TextAsset TraversalTuning
        {
            get => _traversalTuning;
            set => _traversalTuning = value;
        }

        public TextAsset StealthTuning
        {
            get => _stealthTuning;
            set => _stealthTuning = value;
        }

        public TextAsset BotsTuning
        {
            get => _botsTuning;
            set => _botsTuning = value;
        }

        public TextAsset StreaksTuning
        {
            get => _streaksTuning;
            set => _streaksTuning = value;
        }

        public TextAsset AimAssistTuning
        {
            get => _aimAssistTuning;
            set => _aimAssistTuning = value;
        }

        public TextAsset ProgressionTuning
        {
            get => _progressionTuning;
            set => _progressionTuning = value;
        }

        public TextAsset SkillTuning
        {
            get => _skillTuning;
            set => _skillTuning = value;
        }

        public TextAsset MinimapTuning
        {
            get => _minimapTuning;
            set => _minimapTuning = value;
        }

        public TextAsset AssistTuning
        {
            get => _assistTuning;
            set => _assistTuning = value;
        }

        public TextAsset RumbleTuning
        {
            get => _rumbleTuning;
            set => _rumbleTuning = value;
        }

        public TextAsset ChatWordsTuning
        {
            get => _chatWordsTuning;
            set => _chatWordsTuning = value;
        }

        public TextAsset AchievementsTuning
        {
            get => _achievementsTuning;
            set => _achievementsTuning = value;
        }

        public TextAsset LeaderboardsTuning
        {
            get => _leaderboardsTuning;
            set => _leaderboardsTuning = value;
        }

        public TextAsset CosmeticsTuning
        {
            get => _cosmeticsTuning;
            set => _cosmeticsTuning = value;
        }

        public TextAsset EmotesTuning
        {
            get => _emotesTuning;
            set => _emotesTuning = value;
        }

        public TextAsset WeatherTuning
        {
            get => _weatherTuning;
            set => _weatherTuning = value;
        }

        public TextAsset TelemetryTuning
        {
            get => _telemetryTuning;
            set => _telemetryTuning = value;
        }

        public TextAsset InfectedTuning
        {
            get => _infectedTuning;
            set => _infectedTuning = value;
        }

        public TextAsset LootTuning
        {
            get => _lootTuning;
            set => _lootTuning = value;
        }

        public TextAsset StashTuning
        {
            get => _stashTuning;
            set => _stashTuning = value;
        }

        public TextAsset ExtractionTuning
        {
            get => _extractionTuning;
            set => _extractionTuning = value;
        }

        public TextAsset ReplayTuning
        {
            get => _replayTuning;
            set => _replayTuning = value;
        }

        public TextAsset PayloadTuning
        {
            get => _payloadTuning;
            set => _payloadTuning = value;
        }

        public TextAsset EconomyTuning
        {
            get => _economyTuning;
            set => _economyTuning = value;
        }

        public TextAsset SupplyDropsTuning
        {
            get => _supplyDropsTuning;
            set => _supplyDropsTuning = value;
        }

        public TextAsset RedeployTuning
        {
            get => _redeployTuning;
            set => _redeployTuning = value;
        }

        public TextAsset MutatorsTuning
        {
            get => _mutatorsTuning;
            set => _mutatorsTuning = value;
        }

        public TextAsset PlaylistsTuning
        {
            get => _playlistsTuning;
            set => _playlistsTuning = value;
        }

        public WeaponCatalogAsset WeaponCatalog
        {
            get => _weaponCatalog;
            set => _weaponCatalog = value;
        }

        public LoadoutDefinition Loadout
        {
            get => _loadout;
            set => _loadout = value;
        }

        public ScriptableObject Missions
        {
            get => _missions;
            set => _missions = value;
        }

        public TeamDeathmatchDefinition TeamDeathmatch
        {
            get => _teamDeathmatch;
            set => _teamDeathmatch = value;
        }

        public DominationDefinition Domination
        {
            get => _domination;
            set => _domination = value;
        }

        public HardpointDefinition Hardpoint
        {
            get => _hardpoint;
            set => _hardpoint = value;
        }

        public KillConfirmedDefinition KillConfirmed
        {
            get => _killConfirmed;
            set => _killConfirmed = value;
        }

        public SearchAndDestroyDefinition SearchAndDestroy
        {
            get => _searchAndDestroy;
            set => _searchAndDestroy = value;
        }

        public CaptureTheFlagDefinition CaptureTheFlag
        {
            get => _captureTheFlag;
            set => _captureTheFlag = value;
        }

        public HordeDefinition Horde
        {
            get => _horde;
            set => _horde = value;
        }

        public GunGameLadderDefinition GunGameLadder
        {
            get => _gunGameLadder;
            set => _gunGameLadder = value;
        }

        public FreeForAllDefinition FreeForAll
        {
            get => _freeForAll;
            set => _freeForAll = value;
        }

        public GuardianDefinition Guardian
        {
            get => _guardian;
            set => _guardian = value;
        }

        public ExecutionDefinition Execution
        {
            get => _execution;
            set => _execution = value;
        }

        public BattleRoyaleDefinition BattleRoyale
        {
            get => _battleRoyale;
            set => _battleRoyale = value;
        }

        public GameObject CharacterRig
        {
            get => _characterRig;
            set => _characterRig = value;
        }

        public MenuSceneSettings MenuScenes
        {
            get => _menuScenes;
            set => _menuScenes = value;
        }

        public PresentationCatalog Presentation
        {
            get => _presentation;
            set => _presentation = value;
        }

        public ViewmodelCatalog Viewmodels
        {
            get => _viewmodels;
            set => _viewmodels = value;
        }

        public WeaponExtrasCatalog WeaponExtras
        {
            get => _weaponExtras;
            set => _weaponExtras = value;
        }

        public AttachmentModelCatalog AttachmentModels
        {
            get => _attachmentModels;
            set => _attachmentModels = value;
        }

        public HitFeedbackCatalog HitFeedback
        {
            get => _hitFeedback;
            set => _hitFeedback = value;
        }

        public DamageFeedbackCatalog DamageFeedback
        {
            get => _damageFeedback;
            set => _damageFeedback = value;
        }

        public SurfaceImpactCatalog SurfaceImpacts
        {
            get => _surfaceImpacts;
            set => _surfaceImpacts = value;
        }

        public FootstepSurfaceCatalog Footsteps
        {
            get => _footsteps;
            set => _footsteps = value;
        }

        public RagdollCatalog Ragdoll
        {
            get => _ragdoll;
            set => _ragdoll = value;
        }

        public SuppressionCatalog Suppression
        {
            get => _suppression;
            set => _suppression = value;
        }

        public ScriptableObject AwarenessIndicator
        {
            get => _awarenessIndicator;
            set => _awarenessIndicator = value;
        }

        public CameraShakeSettings CameraShake
        {
            get => _cameraShake;
            set => _cameraShake = value;
        }

        public DamageNumberCatalog DamageNumbers
        {
            get => _damageNumbers;
            set => _damageNumbers = value;
        }

        public AimBlockCatalog AimBlock
        {
            get => _aimBlock;
            set => _aimBlock = value;
        }

        public ItemIconCatalog ItemIcons
        {
            get => _itemIcons;
            set => _itemIcons = value;
        }

        public StreakIconCatalog StreakIcons
        {
            get => _streakIcons;
            set => _streakIcons = value;
        }

        public WaterCatalog Water
        {
            get => _water;
            set => _water = value;
        }

        public ReticleCatalog Reticles
        {
            get => _reticles;
            set => _reticles = value;
        }

        public StatusEffectVisualCatalog StatusEffectVisuals
        {
            get => _statusEffectVisuals;
            set => _statusEffectVisuals = value;
        }

        public FlashOverlayCatalog FlashOverlay
        {
            get => _flashOverlay;
            set => _flashOverlay = value;
        }

        public CosmeticVisualCatalog CosmeticVisuals
        {
            get => _cosmeticVisuals;
            set => _cosmeticVisuals = value;
        }

        public EmoteCatalog EmoteAnimations
        {
            get => _emoteAnimations;
            set => _emoteAnimations = value;
        }

        public VisionVisualCatalog VisionVisuals
        {
            get => _visionVisuals;
            set => _visionVisuals = value;
        }

        public static GameContent Current
        {
            get
            {
                if (_override != null)
                {
                    return _override;
                }

                if (!_searched)
                {
                    _found = Find(Resources.LoadAll<GameContent>(ResourceFolder));
                    _searched = true;
                }

                return _found != null ? _found : null;
            }
        }

        public static void Use(GameContent content)
        {
            _override = content;
            ForgetLoaded();
        }

        public static void Reload()
        {
            _found = null;
            _searched = false;
            ForgetLoaded();
        }

        // 入口の欄が空なら null を返すので、呼ぶ側は ?? ではなく != null で次の引き先へ進む (Unity の破棄済みの参照も null にそろえる)。
        public static T Pick<T>(Func<GameContent, T> select)
            where T : Object
        {
            if (select == null)
            {
                throw new ArgumentNullException(nameof(select));
            }

            var content = Current;
            if (content == null)
            {
                return null;
            }

            var picked = select(content);
            return picked != null ? picked : null;
        }

        public static T Pick<T>(T assigned, Func<GameContent, T> select)
            where T : Object
        {
            return assigned != null ? assigned : Pick(select);
        }

        public static GameContent Find(IReadOnlyList<GameContent> candidates)
        {
            var found = (candidates ?? Array.Empty<GameContent>()).Where(c => c != null).OrderBy(c => c.name, StringComparer.Ordinal).ToList();
            if (found.Count == 0)
            {
                return null;
            }

            if (found.Count > 1)
            {
                Debug.LogError(
                    $"{LogTag} {found.Count} assets in Resources/{ResourceFolder}/ ({string.Join(", ", found.Select(c => c.name))}); using '{found[0].name}'. Keep only one.",
                    found[0]);
            }
            else
            {
                Debug.Log($"{LogTag} using '{found[0].name}' from Resources/{ResourceFolder}/", found[0]);
            }

            return found[0];
        }

        public EnemyDefinition EnemyFor(EnemyArchetype archetype)
        {
            foreach (var enemy in Enemies)
            {
                if (enemy != null && enemy.Archetype == archetype)
                {
                    return enemy;
                }
            }

            return null;
        }

        public void SetEnemies(IEnumerable<EnemyDefinition> enemies)
        {
            _enemies = enemies == null ? new List<EnemyDefinition>() : new List<EnemyDefinition>(enemies);
        }

        // 調整値は読んだものを持ち続けるので、入口が替わったら読み直させる。
        private static void ForgetLoaded()
        {
            KitTuning.Reload();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _override = null;
            _found = null;
            _searched = false;
        }
    }
}
