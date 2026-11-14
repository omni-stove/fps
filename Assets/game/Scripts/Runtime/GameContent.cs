using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Runtime
{
    // シーンのコンポーネントに直接配線した参照は、シーンごとに変えたいときのために、入口より先に使う。
    // 入口は、配線が空のときにキットが引く「ゲーム全体の中身」で、入口の欄も空なら従来の Resources のパス、それも無ければ既定値で動く。
    [CreateAssetMenu(fileName = "GameContent", menuName = "Fps/Game Content")]
    public sealed class GameContent : ScriptableObject
    {
        public const string LogTag = "[GameContent]";

        // 名前ではなく型で探すので、ゲームは Resources/FpsKit/ に好きな名前で 1 つ置けばよい。
        public const string ResourceFolder = "FpsKit";

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

        [Header("武器・キャンペーン")]
        [SerializeField, Tooltip("使える武器の一覧")]
        private WeaponCatalogAsset _weaponCatalog;

        [SerializeField, Tooltip("最初に持つ武器")]
        private LoadoutDefinition _loadout;

        [SerializeField, Tooltip("メニューに出すキャンペーンのミッション")]
        private MissionCatalog _missions;

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
        private HordeDefinition _horde;

        [SerializeField]
        private GunGameLadderDefinition _gunGameLadder;

        [Header("キャラクター・文言・メニュー")]
        [SerializeField, Tooltip("プレイヤー・NetPlayer・敵に置く人型のリグ (根に Animator)。空なら Resources/Characters/CharacterRig")]
        private GameObject _characterRig;

        [SerializeField, Tooltip("言語ごとの文言の表。Resources/Localization/<言語>/ の表の後に重ね、同じキーはこちらが勝つ")]
        private List<GameContentTextTables> _texts = new List<GameContentTextTables>();

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

        [SerializeField]
        private AwarenessIndicatorCatalog _awarenessIndicator;

        [SerializeField]
        private CameraShakeSettings _cameraShake;

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

        public MissionCatalog Missions
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

        public GameObject CharacterRig
        {
            get => _characterRig;
            set => _characterRig = value;
        }

        public IReadOnlyList<GameContentTextTables> Texts => _texts ??= new List<GameContentTextTables>();

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

        public AwarenessIndicatorCatalog AwarenessIndicator
        {
            get => _awarenessIndicator;
            set => _awarenessIndicator = value;
        }

        public CameraShakeSettings CameraShake
        {
            get => _cameraShake;
            set => _cameraShake = value;
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

        // 起動のコードやテストが、Resources に置いたものの代わりに使わせる。null で Resources のものに戻す。
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

        public IReadOnlyList<TextAsset> TextsFor(string languageCode)
        {
            var tables = new List<TextAsset>();
            foreach (var entry in Texts)
            {
                if (entry == null || !string.Equals(entry.Language.Trim(), languageCode, StringComparison.Ordinal))
                {
                    continue;
                }

                tables.AddRange(entry.Tables.Where(t => t != null));
            }

            return tables;
        }

        public void SetEnemies(IEnumerable<EnemyDefinition> enemies)
        {
            _enemies = enemies == null ? new List<EnemyDefinition>() : new List<EnemyDefinition>(enemies);
        }

        public void SetTexts(IEnumerable<GameContentTextTables> texts)
        {
            _texts = texts == null ? new List<GameContentTextTables>() : new List<GameContentTextTables>(texts);
        }

        // 文言と調整値は読んだものを持ち続けるので、入口が替わったら読み直させる。
        private static void ForgetLoaded()
        {
            Localizer.Reload();
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
