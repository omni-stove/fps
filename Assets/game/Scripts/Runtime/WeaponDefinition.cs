using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Weapon", menuName = "Shooter/Weapon Definition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("武器の識別子。小文字・ハイフン区切りの UUID (例: 3f2b8c1e-0a4d-4c6e-9b1a-7d2e5f8c0b13)。空なら自動で振る。作った後は変えない")]
        private string _id = string.Empty;

        // 表示名をそのまま持っていた頃のアセットも読めるようにする。その値はキーとして引かれ、表に無ければそのまま出る
        [SerializeField, FormerlySerializedAs("_displayName"), Tooltip("プレイヤーに見せる名前を引く表のキー (例: weapon.rifle)。文は Resources/Localization/<言語>/*.json に書く。空なら Id を出す")]
        private string _nameKey = string.Empty;

        [SerializeField, TextArea, Tooltip("作る人向けのメモ (どの銃を模したか・どんな役回りか)。プレイヤーには見せない")]
        private string _description = string.Empty;

        [SerializeField, Tooltip("武器の種別。ロードアウトの制約と表示にだけ使う")]
        private WeaponCategory _category = WeaponCategory.AssaultRifle;

        [SerializeField, Tooltip("射撃モード")]
        private FireMode _fireMode = FireMode.Auto;

        [SerializeField, Min(1), Tooltip("弾倉の容量")]
        private int _magazineSize = 30;

        [SerializeField, Min(0), Tooltip("予備弾の初期値")]
        private int _reserveAmmo = 90;

        [SerializeField, Min(0f), Tooltip("射撃から次に撃てるまでの秒数")]
        private float _fireInterval = 0.1f;

        [SerializeField, Min(0f), Tooltip("リロード開始から補充までの秒数")]
        private float _reloadSeconds = 2f;

        [SerializeField, Min(0), Tooltip("1 発あたりの基本ダメージ")]
        private int _damage = 25;

        [SerializeField, Min(0.01f), Tooltip("射程 (m)")]
        private float _range = 100f;

        [Header("距離減衰")]
        [SerializeField, Min(0f), Tooltip("この距離までは減衰しない (m)")]
        private float _falloffStart = 20f;

        [SerializeField, Min(0f), Tooltip("この距離以上で最小倍率になる (m)")]
        private float _falloffEnd = 60f;

        [SerializeField, UnityEngine.Range(0f, 1f), Tooltip("最大まで減衰したときの倍率")]
        private float _falloffMinMultiplier = 0.5f;

        [Header("建造物")]
        [SerializeField, Min(0f), Tooltip("建造物に当たったときに被害を広げる半径 (m)。0 なら当たった物だけ")]
        private float _explosionRadius;

        [Header("覗き込み (ADS)")]
        [SerializeField, Min(0f), Tooltip("覗き込み切ったときのカメラの縦の視野角 (度)。0 なら共通の初期値 (Shooter.Core.AdsZoom.DefaultFieldOfView)")]
        private float _adsFieldOfView = UnsetAdsFieldOfView;

        [Header("ブラインドファイア")]
        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("隠れたまま撃ったときに、狙った向きから弾が散らばる最大の角度 (度)")]
        private float _blindFireSpreadDegrees;

        [Header("アクティブリロード")]
        [SerializeField, UnityEngine.Range(0f, 0.99f), Tooltip("判定の幅が始まる時点の、リロード時間に対する割合")]
        private float _activeReloadWindowStart = WeaponSpec.DefaultActiveReloadWindowStart;

        [SerializeField, UnityEngine.Range(0.01f, 1f), Tooltip("判定の幅の長さの、リロード時間に対する割合。Start との和は 1 以下")]
        private float _activeReloadWindowLength = WeaponSpec.DefaultActiveReloadWindowLength;

        [SerializeField, Min(0f), Tooltip("判定の幅を外したときにリロードに足される秒数")]
        private float _activeReloadFailSeconds = WeaponSpec.DefaultActiveReloadFailSeconds;

        [Header("反動")]
        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("1 発ごとに視線が上へ跳ねる角度 (度)")]
        private float _recoilPitchDegrees;

        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("1 発ごとに視線が左右へぶれる最大の角度 (度)")]
        private float _recoilYawDegrees;

        [SerializeField, Min(0f), Tooltip("撃ち止めてから跳ねた視線が戻る速さ (度/秒)。0 なら戻らない")]
        private float _recoilRecoveryPerSecond;

        [SerializeField, Min(0f), Tooltip("最後に撃ってから跳ねた視線が戻り始めるまでの秒数")]
        private float _recoilRecoveryDelaySeconds = WeaponSpec.DefaultRecoilRecoveryDelaySeconds;

        [Header("弾の散らばり")]
        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("腰だめで撃ったときに弾が散らばる最大の角度 (度)")]
        private float _hipSpreadDegrees;

        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("覗き込み切って撃ったときに弾が散らばる最大の角度 (度)")]
        private float _adsSpreadDegrees;

        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("動きながら撃ったときに散らばりへ足す最大の角度 (度)")]
        private float _moveSpreadDegrees;

        [Header("音")]
        [SerializeField, Min(0f), Tooltip("銃声が敵に聞こえる距離 (m)。消音した武器は小さくする。0 なら聞こえない")]
        private float _shotNoiseRadius = WeaponSpec.DefaultShotNoiseRadius;

        [Header("持ち運び")]
        [SerializeField, Min(0f), Tooltip("重さ (kg)。移動の速さへの効き方は移動の設定 (MotorSettings) の持ち物の項目が決める")]
        private float _weight = WeaponSpec.DefaultWeight;

        [SerializeField, Min(0f), Tooltip("持っている間に移動の速さへ掛ける倍率。どの武器の倍率を掛けるかは移動の設定が決める")]
        private float _moveSpeedMultiplier = WeaponSpec.DefaultMoveSpeedMultiplier;

        [Header("持ち替え")]
        [SerializeField, Tooltip("この武器を構える秒数を武器ごとに決めるか。切るとロードアウトの持ち替え秒数を使う")]
        private bool _hasDrawSeconds;

        [SerializeField, Min(0f), Tooltip("この武器へ持ち替えて構えるまでの秒数")]
        private float _drawSeconds;

        [Header("撃ち方")]
        [SerializeField]
        private BurstSettings _burst = new BurstSettings();

        [SerializeField]
        private ChargeSettings _charge = new ChargeSettings();

        [SerializeField]
        private SpinUpSettings _spinUp = new SpinUpSettings();

        [SerializeField]
        private CycleSettings _cycle = new CycleSettings();

        [Header("弾薬")]
        [SerializeField, Tooltip("弾倉で撃つか、熱が溜まるまで撃てるか")]
        private AmmoKind _ammoKind = AmmoKind.Magazine;

        [SerializeField, Tooltip("弾倉をまとめて替えるか、1 発ずつ込めるか")]
        private ReloadStyle _reloadStyle = ReloadStyle.Whole;

        [SerializeField, Min(0f), Tooltip("1 発ずつ込めるときの 1 発あたりの秒数")]
        private float _reloadSecondsPerRound;

        [SerializeField, Tooltip("弾薬の種類が Heat のときの熱の溜まり方")]
        private HeatSettings _heat = new HeatSettings();

        [Header("弾")]
        [SerializeField, Tooltip("弾の届け方")]
        private RoundDelivery _delivery = RoundDelivery.Hitscan;

        [SerializeField, Min(1), Tooltip("1 発で出る弾 (ペレット) の数")]
        private int _pelletCount = 1;

        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("ペレットが散らばる最大の角度 (度)")]
        private float _pelletSpreadDegrees;

        [SerializeField, Min(0f), Tooltip("弾の当たり判定の半径 (m)。0 なら細い線")]
        private float _roundRadius;

        [SerializeField]
        private PenetrationSettings _penetration = new PenetrationSettings();

        [SerializeField]
        private RicochetSettings _ricochet = new RicochetSettings();

        [SerializeField, Tooltip("届け方が Projectile のときの飛ぶ弾")]
        private ProjectileSettings _projectile = new ProjectileSettings();

        [SerializeField]
        private HomingSettings _homing = new HomingSettings();

        [SerializeField, Tooltip("届け方が Beam のときのビーム")]
        private BeamSettings _beam = new BeamSettings();

        [SerializeField, Tooltip("届け方が Spray のときの火炎")]
        private SpraySettings _spray = new SpraySettings();

        [Header("着弾")]
        [SerializeField]
        private ExplosionSettings _explosion = new ExplosionSettings();

        [SerializeField]
        private StatusEffectSettings _statusEffect = new StatusEffectSettings();

        [SerializeField]
        private AreaEffectSettings _areaEffect = new AreaEffectSettings();

        [Header("照準")]
        [SerializeField, Tooltip("覗き込みの視野角の次から切り替えられる、スコープの倍率ごとの縦の視野角 (度)")]
        private float[] _extraZoomLevels = Array.Empty<float>();

        [SerializeField]
        private BreathHoldSettings _breathHold = new BreathHoldSettings();

        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("覗き込み中に照準が揺れる最大の角度 (度)")]
        private float _swayDegrees;

        [SerializeField]
        private SightSettings _sight = new SightSettings();

        [Header("弾薬の種類")]
        [SerializeField, Tooltip("切り替えて撃てる弾薬の種類。空なら切り替えない")]
        private AmmoTypeSettings[] _ammoTypes = Array.Empty<AmmoTypeSettings>();

        // この項目が無かった頃に保存したアセットも読み込むと 0 になるので、0 を未設定とする。
        public const float UnsetAdsFieldOfView = 0f;

        public string Id
        {
            get => _id ?? string.Empty;
            set => _id = value ?? string.Empty;
        }

        public string NameKey
        {
            get => _nameKey ?? string.Empty;
            set => _nameKey = value ?? string.Empty;
        }

        public string Description
        {
            get => _description ?? string.Empty;
            set => _description = value ?? string.Empty;
        }

        public WeaponCategory Category
        {
            get => _category;
            set => _category = value;
        }

        public FireMode FireMode
        {
            get => _fireMode;
            set => _fireMode = value;
        }

        public int MagazineSize
        {
            get => _magazineSize;
            set => _magazineSize = value;
        }

        public int ReserveAmmo
        {
            get => _reserveAmmo;
            set => _reserveAmmo = value;
        }

        public float FireInterval
        {
            get => _fireInterval;
            set => _fireInterval = value;
        }

        public float ReloadSeconds
        {
            get => _reloadSeconds;
            set => _reloadSeconds = value;
        }

        public int Damage
        {
            get => _damage;
            set => _damage = value;
        }

        public float Range
        {
            get => _range;
            set => _range = value;
        }

        public float FalloffStart
        {
            get => _falloffStart;
            set => _falloffStart = value;
        }

        public float FalloffEnd
        {
            get => _falloffEnd;
            set => _falloffEnd = value;
        }

        public float FalloffMinMultiplier
        {
            get => _falloffMinMultiplier;
            set => _falloffMinMultiplier = value;
        }

        public float ExplosionRadius
        {
            get => _explosionRadius;
            set => _explosionRadius = value;
        }

        public float AdsFieldOfView
        {
            get => _adsFieldOfView;
            set => _adsFieldOfView = value;
        }

        public float BlindFireSpreadDegrees
        {
            get => _blindFireSpreadDegrees;
            set => _blindFireSpreadDegrees = value;
        }

        public float ActiveReloadWindowStart
        {
            get => _activeReloadWindowStart;
            set => _activeReloadWindowStart = value;
        }

        public float ActiveReloadWindowLength
        {
            get => _activeReloadWindowLength;
            set => _activeReloadWindowLength = value;
        }

        public float ActiveReloadFailSeconds
        {
            get => _activeReloadFailSeconds;
            set => _activeReloadFailSeconds = value;
        }

        public float RecoilPitchDegrees
        {
            get => _recoilPitchDegrees;
            set => _recoilPitchDegrees = value;
        }

        public float RecoilYawDegrees
        {
            get => _recoilYawDegrees;
            set => _recoilYawDegrees = value;
        }

        public float RecoilRecoveryPerSecond
        {
            get => _recoilRecoveryPerSecond;
            set => _recoilRecoveryPerSecond = value;
        }

        public float RecoilRecoveryDelaySeconds
        {
            get => _recoilRecoveryDelaySeconds;
            set => _recoilRecoveryDelaySeconds = value;
        }

        public float ShotNoiseRadius
        {
            get => _shotNoiseRadius;
            set => _shotNoiseRadius = value;
        }

        public float Weight
        {
            get => _weight;
            set => _weight = value;
        }

        public float MoveSpeedMultiplier
        {
            get => _moveSpeedMultiplier;
            set => _moveSpeedMultiplier = value;
        }

        public float? DrawSeconds
        {
            get => _hasDrawSeconds ? _drawSeconds : (float?)null;
            set
            {
                _hasDrawSeconds = value.HasValue;
                _drawSeconds = value ?? 0f;
            }
        }

        public float HipSpreadDegrees
        {
            get => _hipSpreadDegrees;
            set => _hipSpreadDegrees = value;
        }

        public float AdsSpreadDegrees
        {
            get => _adsSpreadDegrees;
            set => _adsSpreadDegrees = value;
        }

        public float MoveSpreadDegrees
        {
            get => _moveSpreadDegrees;
            set => _moveSpreadDegrees = value;
        }

        public BurstSettings Burst => _burst ??= new BurstSettings();

        public ChargeSettings Charge => _charge ??= new ChargeSettings();

        public SpinUpSettings SpinUp => _spinUp ??= new SpinUpSettings();

        public CycleSettings Cycle => _cycle ??= new CycleSettings();

        public AmmoKind AmmoKind
        {
            get => _ammoKind;
            set => _ammoKind = value;
        }

        public ReloadStyle ReloadStyle
        {
            get => _reloadStyle;
            set => _reloadStyle = value;
        }

        public float ReloadSecondsPerRound
        {
            get => _reloadSecondsPerRound;
            set => _reloadSecondsPerRound = value;
        }

        public HeatSettings Heat => _heat ??= new HeatSettings();

        public RoundDelivery Delivery
        {
            get => _delivery;
            set => _delivery = value;
        }

        public int PelletCount
        {
            get => _pelletCount;
            set => _pelletCount = value;
        }

        public float PelletSpreadDegrees
        {
            get => _pelletSpreadDegrees;
            set => _pelletSpreadDegrees = value;
        }

        public float RoundRadius
        {
            get => _roundRadius;
            set => _roundRadius = value;
        }

        public PenetrationSettings Penetration => _penetration ??= new PenetrationSettings();

        public RicochetSettings Ricochet => _ricochet ??= new RicochetSettings();

        public ProjectileSettings Projectile => _projectile ??= new ProjectileSettings();

        public HomingSettings Homing => _homing ??= new HomingSettings();

        public BeamSettings Beam => _beam ??= new BeamSettings();

        public SpraySettings Spray => _spray ??= new SpraySettings();

        public ExplosionSettings Explosion => _explosion ??= new ExplosionSettings();

        public StatusEffectSettings StatusEffect => _statusEffect ??= new StatusEffectSettings();

        public AreaEffectSettings AreaEffect => _areaEffect ??= new AreaEffectSettings();

        public float[] ExtraZoomLevels
        {
            get => _extraZoomLevels ?? Array.Empty<float>();
            set => _extraZoomLevels = value ?? Array.Empty<float>();
        }

        public BreathHoldSettings BreathHold => _breathHold ??= new BreathHoldSettings();

        public float SwayDegrees
        {
            get => _swayDegrees;
            set => _swayDegrees = value;
        }

        public SightSettings Sight => _sight ??= new SightSettings();

        public AmmoTypeSettings[] AmmoTypes
        {
            get => _ammoTypes ?? Array.Empty<AmmoTypeSettings>();
            set => _ammoTypes = value ?? Array.Empty<AmmoTypeSettings>();
        }

        public bool AssignIdIfEmpty()
        {
            if (!string.IsNullOrWhiteSpace(_id))
            {
                return false;
            }

            _id = Guid.NewGuid().ToString("D");
            return true;
        }

        private void Reset()
        {
            AssignIdIfEmpty();
        }

        private void OnValidate()
        {
            var assigned = AssignIdIfEmpty();
#if UNITY_EDITOR
            // OnValidate での書き換えは dirty にならず、保存されないまま開くたび別の Id になるため
            if (assigned)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        public WeaponSpec ToSpec()
        {
            var source = string.IsNullOrEmpty(name) ? "(unnamed weapon)" : name;
            try
            {
                var trigger = new TriggerSpec(_fireMode, _fireInterval, Burst.ToSpec(), Charge.ToSpec(), SpinUp.ToSpec(), Cycle.ToSpec());
                var ammo = new AmmoSpec(
                    _magazineSize,
                    _reserveAmmo,
                    _reloadSeconds,
                    _activeReloadWindowStart,
                    _activeReloadWindowLength,
                    _activeReloadFailSeconds,
                    _reloadStyle,
                    _reloadSecondsPerRound,
                    _ammoKind,
                    _ammoKind == AmmoKind.Heat ? Heat.ToSpec() : null);
                var round = RoundSettings.Build(
                    _delivery, _range, _pelletCount, _pelletSpreadDegrees, _roundRadius, Penetration, Ricochet, Projectile, Homing, Beam, Spray);
                var impact = new ImpactSpec(
                    _damage,
                    new DamageFalloff(_falloffStart, _falloffEnd, _falloffMinMultiplier),
                    _explosionRadius,
                    Explosion.ToSpec(),
                    StatusEffect.ToSpec(),
                    AreaEffect.ToSpec());
                var aim = new AimSpec(
                    _hipSpreadDegrees,
                    _adsSpreadDegrees,
                    _moveSpreadDegrees,
                    _recoilPitchDegrees,
                    _recoilYawDegrees,
                    _recoilRecoveryPerSecond,
                    _recoilRecoveryDelaySeconds,
                    _blindFireSpreadDegrees,
                    ZoomLevels(),
                    BreathHold.ToSpec(),
                    _swayDegrees,
                    Sight.ToSpec());
                return new WeaponSpec(
                    Id, NameKey, _category, trigger, ammo, round, impact, aim, AmmoTypeSettings.ToSpecs(AmmoTypes), _shotNoiseRadius, _weight, _moveSpeedMultiplier, Description, DrawSeconds);
            }
            catch (ArgumentException e)
            {
                throw new WeaponDefinitionException($"weapon '{source}' (id '{Id}') is invalid: {e.Message}", e);
            }
        }

        private float[] ZoomLevels()
        {
            var extra = ExtraZoomLevels;

            // 負の値などは未設定と見なさず、WeaponSpec の検査で不正として弾く。
            if (_adsFieldOfView == UnsetAdsFieldOfView && extra.Length == 0)
            {
                return null;
            }

            var levels = new float[extra.Length + 1];
            levels[0] = _adsFieldOfView == UnsetAdsFieldOfView ? AdsZoom.DefaultFieldOfView : _adsFieldOfView;
            Array.Copy(extra, 0, levels, 1, extra.Length);
            return levels;
        }
    }

    public sealed class WeaponDefinitionException : Exception
    {
        public WeaponDefinitionException(string message)
            : base(message)
        {
        }

        public WeaponDefinitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
