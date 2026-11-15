using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="WeaponDefinition"/> が Inspector に出す、武器の部品の省略できる入れ子 (<see cref="BurstSpec"/> など) の値。
    /// <c>Enabled</c> を持つものは、切ってある間は値を残したまま部品に入れない。届け方 (<see cref="RoundDelivery"/>) や弾薬の種類
    /// (<see cref="AmmoKind"/>) で使うかが決まるものは <c>Enabled</c> を持たず、その届け方・種類のときだけ部品に入れる。
    /// </summary>
    [Serializable]
    public sealed class BurstSettings
    {
        [SerializeField, Tooltip("1 回引くと続けて撃つ")]
        private bool _enabled;

        [SerializeField, Min(BurstSpec.MinRoundsPerBurst), Tooltip("1 回引いて撃つ弾数")]
        private int _roundsPerBurst = 3;

        [SerializeField, Min(0f), Tooltip("バーストの中の弾の間隔 (秒)")]
        private float _roundIntervalSeconds = 0.08f;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public int RoundsPerBurst { get => _roundsPerBurst; set => _roundsPerBurst = value; }

        public float RoundIntervalSeconds { get => _roundIntervalSeconds; set => _roundIntervalSeconds = value; }

        public BurstSpec ToSpec() => _enabled ? new BurstSpec(_roundsPerBurst, _roundIntervalSeconds) : null;

        public void Set(BurstSpec spec)
        {
            _enabled = spec != null;
            if (spec != null)
            {
                _roundsPerBurst = spec.RoundsPerBurst;
                _roundIntervalSeconds = spec.RoundIntervalSeconds;
            }
        }
    }

    [Serializable]
    public sealed class ChargeSettings
    {
        [SerializeField, Tooltip("押している間に溜め、離して撃つ")]
        private bool _enabled;

        [SerializeField, Min(0.01f), Tooltip("溜め切るまでの秒数")]
        private float _chargeSeconds = 1f;

        [SerializeField, UnityEngine.Range(0f, 1f), Tooltip("撃てる最小の溜め (溜め切りに対する割合)")]
        private float _minChargeFraction = 0.25f;

        [SerializeField, Min(0f), Tooltip("溜め切って撃ったときのダメージの倍率")]
        private float _fullChargeDamageMultiplier = 2f;

        [SerializeField, Tooltip("溜め切ったら離さなくても撃つ")]
        private bool _firesWhenFull;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public float ChargeSeconds { get => _chargeSeconds; set => _chargeSeconds = value; }

        public float MinChargeFraction { get => _minChargeFraction; set => _minChargeFraction = value; }

        public float FullChargeDamageMultiplier { get => _fullChargeDamageMultiplier; set => _fullChargeDamageMultiplier = value; }

        public bool FiresWhenFull { get => _firesWhenFull; set => _firesWhenFull = value; }

        public ChargeSpec ToSpec() =>
            _enabled ? new ChargeSpec(_chargeSeconds, _minChargeFraction, _fullChargeDamageMultiplier, _firesWhenFull) : null;

        public void Set(ChargeSpec spec)
        {
            _enabled = spec != null;
            if (spec != null)
            {
                _chargeSeconds = spec.ChargeSeconds;
                _minChargeFraction = spec.MinChargeFraction;
                _fullChargeDamageMultiplier = spec.FullChargeDamageMultiplier;
                _firesWhenFull = spec.FiresWhenFull;
            }
        }
    }

    [Serializable]
    public sealed class SpinUpSettings
    {
        [SerializeField, Tooltip("撃ち始めは遅く、撃ち続けると連射間隔まで速くなる")]
        private bool _enabled;

        [SerializeField, Min(0.01f), Tooltip("押してから撃ち始めるまでの秒数")]
        private float _spinUpSeconds = 1f;

        [SerializeField, Min(1f), Tooltip("撃ち始めの間隔の、連射間隔に対する倍率")]
        private float _startIntervalMultiplier = 3f;

        [SerializeField, Min(0f), Tooltip("離してから回転が落ち切るまでの秒数")]
        private float _spinDownSeconds = 0.5f;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public float SpinUpSeconds { get => _spinUpSeconds; set => _spinUpSeconds = value; }

        public float StartIntervalMultiplier { get => _startIntervalMultiplier; set => _startIntervalMultiplier = value; }

        public float SpinDownSeconds { get => _spinDownSeconds; set => _spinDownSeconds = value; }

        public SpinUpSpec ToSpec() => _enabled ? new SpinUpSpec(_spinUpSeconds, _startIntervalMultiplier, _spinDownSeconds) : null;

        public void Set(SpinUpSpec spec)
        {
            _enabled = spec != null;
            if (spec != null)
            {
                _spinUpSeconds = spec.SpinUpSeconds;
                _startIntervalMultiplier = spec.StartIntervalMultiplier;
                _spinDownSeconds = spec.SpinDownSeconds;
            }
        }
    }

    [Serializable]
    public sealed class CycleSettings
    {
        [SerializeField, Tooltip("1 発ごとにボルト・ポンプを引く")]
        private bool _enabled;

        [SerializeField, Min(0.01f), Tooltip("1 発撃ってから次に撃てるまでの装填の秒数")]
        private float _cycleSeconds = 1f;

        [SerializeField, Tooltip("装填の間は覗き込みを解く")]
        private bool _leavesAds = true;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public float CycleSeconds { get => _cycleSeconds; set => _cycleSeconds = value; }

        public bool LeavesAds { get => _leavesAds; set => _leavesAds = value; }

        public CycleSpec ToSpec() => _enabled ? new CycleSpec(_cycleSeconds, _leavesAds) : null;

        public void Set(CycleSpec spec)
        {
            _enabled = spec != null;
            if (spec != null)
            {
                _cycleSeconds = spec.CycleSeconds;
                _leavesAds = spec.LeavesAds;
            }
        }
    }

    /// <summary>弾薬の種類が <see cref="AmmoKind.Heat"/> のときだけ使う。</summary>
    [Serializable]
    public sealed class HeatSettings
    {
        [SerializeField, UnityEngine.Range(0.01f, 1f), Tooltip("1 発で溜まる熱 (1 で撃てなくなる)")]
        private float _heatPerShot = 0.1f;

        [SerializeField, Min(0.01f), Tooltip("1 秒に冷める熱")]
        private float _coolPerSecond = 0.5f;

        [SerializeField, Min(0f), Tooltip("撃ち止めてから冷め始めるまでの秒数")]
        private float _coolDelaySeconds = 0.25f;

        [SerializeField, Min(0f), Tooltip("熱が 1 に達したときに撃てなくなる秒数")]
        private float _overheatSeconds = 1.5f;

        public float HeatPerShot { get => _heatPerShot; set => _heatPerShot = value; }

        public float CoolPerSecond { get => _coolPerSecond; set => _coolPerSecond = value; }

        public float CoolDelaySeconds { get => _coolDelaySeconds; set => _coolDelaySeconds = value; }

        public float OverheatSeconds { get => _overheatSeconds; set => _overheatSeconds = value; }

        public HeatSpec ToSpec() => new HeatSpec(_heatPerShot, _coolPerSecond, _coolDelaySeconds, _overheatSeconds);

        public void Set(HeatSpec spec)
        {
            if (spec != null)
            {
                _heatPerShot = spec.HeatPerShot;
                _coolPerSecond = spec.CoolPerSecond;
                _coolDelaySeconds = spec.CoolDelaySeconds;
                _overheatSeconds = spec.OverheatSeconds;
            }
        }
    }

    [Serializable]
    public sealed class PenetrationSettings
    {
        [SerializeField, Tooltip("当たった物を抜けて先へ進む")]
        private bool _enabled;

        [SerializeField, Min(1), Tooltip("抜けられる枚数")]
        private int _maxPenetrations = 1;

        [SerializeField, UnityEngine.Range(0f, 1f), Tooltip("1 枚抜けるごとにダメージに掛ける倍率")]
        private float _damageMultiplierPerPenetration = 0.7f;

        [SerializeField, Min(0f), Tooltip("抜けられる最大の厚さ (m)")]
        private float _maxThickness = 0.5f;

        [SerializeField, Tooltip("面の素材ごとの抜けやすさ。並べていない素材は 1")]
        private List<PenetrationMaterialSettings> _materials = new List<PenetrationMaterialSettings>();

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public int MaxPenetrations { get => _maxPenetrations; set => _maxPenetrations = value; }

        public float DamageMultiplierPerPenetration { get => _damageMultiplierPerPenetration; set => _damageMultiplierPerPenetration = value; }

        public float MaxThickness { get => _maxThickness; set => _maxThickness = value; }

        public List<PenetrationMaterialSettings> Materials => _materials ??= new List<PenetrationMaterialSettings>();

        public PenetrationSpec ToSpec()
        {
            if (!_enabled)
            {
                return null;
            }

            var materials = new List<PenetrationMaterialSpec>(Materials.Count);
            foreach (var material in Materials)
            {
                materials.Add(material?.ToSpec());
            }

            return new PenetrationSpec(_maxPenetrations, _damageMultiplierPerPenetration, _maxThickness, materials);
        }

        public void Set(PenetrationSpec spec)
        {
            _enabled = spec != null;
            if (spec != null)
            {
                _maxPenetrations = spec.MaxPenetrations;
                _damageMultiplierPerPenetration = spec.DamageMultiplierPerPenetration;
                _maxThickness = spec.MaxThickness;
                Materials.Clear();
                foreach (var material in spec.Materials)
                {
                    var settings = new PenetrationMaterialSettings();
                    settings.Set(material);
                    Materials.Add(settings);
                }
            }
        }
    }

    [Serializable]
    public sealed class PenetrationMaterialSettings
    {
        [SerializeField, Tooltip("面の素材の Id (SurfaceMaterial の Id。例: concrete, metal, wood)")]
        private string _materialId = SurfaceMaterial.DefaultId;

        [SerializeField, Min(0f), Tooltip("抜けられる最大の厚さに掛ける倍率。0 ならこの素材は抜けない")]
        private float _penetrability = 1f;

        public string MaterialId { get => _materialId ?? string.Empty; set => _materialId = value ?? string.Empty; }

        public float Penetrability { get => _penetrability; set => _penetrability = value; }

        public PenetrationMaterialSpec ToSpec() => new PenetrationMaterialSpec(MaterialId, _penetrability);

        public void Set(PenetrationMaterialSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _materialId = spec.MaterialId;
            _penetrability = spec.Penetrability;
        }
    }

    [Serializable]
    public sealed class RicochetSettings
    {
        [SerializeField, Tooltip("浅い角度で当たった弾が跳ねる")]
        private bool _enabled;

        [SerializeField, Min(1), Tooltip("跳ねられる回数")]
        private int _maxBounces = 1;

        [SerializeField, UnityEngine.Range(0.01f, 90f), Tooltip("跳ねる、面とのなす角の最大 (度)")]
        private float _maxGrazingAngleDegrees = 30f;

        [SerializeField, UnityEngine.Range(0f, 1f), Tooltip("1 回跳ねるごとにダメージに掛ける倍率")]
        private float _damageMultiplierPerBounce = 0.6f;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public int MaxBounces { get => _maxBounces; set => _maxBounces = value; }

        public float MaxGrazingAngleDegrees { get => _maxGrazingAngleDegrees; set => _maxGrazingAngleDegrees = value; }

        public float DamageMultiplierPerBounce { get => _damageMultiplierPerBounce; set => _damageMultiplierPerBounce = value; }

        public RicochetSpec ToSpec() => _enabled ? new RicochetSpec(_maxBounces, _maxGrazingAngleDegrees, _damageMultiplierPerBounce) : null;

        public void Set(RicochetSpec spec)
        {
            _enabled = spec != null;
            if (spec != null)
            {
                _maxBounces = spec.MaxBounces;
                _maxGrazingAngleDegrees = spec.MaxGrazingAngleDegrees;
                _damageMultiplierPerBounce = spec.DamageMultiplierPerBounce;
            }
        }
    }

    /// <summary>届け方が <see cref="RoundDelivery.Projectile"/> のときだけ使う。</summary>
    [Serializable]
    public sealed class ProjectileSettings
    {
        [SerializeField, Min(0.01f), Tooltip("弾の速さ (m/秒)")]
        private float _speed = 60f;

        [SerializeField, Min(0f), Tooltip("重力に掛ける倍率。0 ならまっすぐ飛ぶ")]
        private float _gravityMultiplier = 1f;

        [SerializeField, Min(0.01f), Tooltip("弾が消えるまでの秒数")]
        private float _lifetimeSeconds = 5f;

        [SerializeField, Tooltip("壁や床に当たったとき、止まる・刺さる・跳ねる")]
        private WorldHitResponse _onWorldHit = WorldHitResponse.Stop;

        [SerializeField, Tooltip("見た目の参照 Id。ネット対戦のクライアントが見た目を引くのに使う。空なら下の見た目")]
        private string _visualId = string.Empty;

        [SerializeField, Tooltip("飛ぶ弾の見た目。空ならコードで細い棒を作る")]
        private GameObject _visual;

        [SerializeField, Tooltip("飛んでいる間の音。空なら鳴らさない")]
        private AudioClip _sound;

        [SerializeField, Min(0f), Tooltip("刺さった弾を残しておく秒数 (見た目だけ)")]
        private float _stuckSeconds = ProjectileBody.DefaultStuckSeconds;

        public float Speed { get => _speed; set => _speed = value; }

        public float GravityMultiplier { get => _gravityMultiplier; set => _gravityMultiplier = value; }

        public float LifetimeSeconds { get => _lifetimeSeconds; set => _lifetimeSeconds = value; }

        public WorldHitResponse OnWorldHit { get => _onWorldHit; set => _onWorldHit = value; }

        public string VisualId { get => _visualId ?? string.Empty; set => _visualId = value ?? string.Empty; }

        public GameObject Visual { get => _visual; set => _visual = value; }

        public AudioClip Sound { get => _sound; set => _sound = value; }

        public float StuckSeconds { get => _stuckSeconds; set => _stuckSeconds = Mathf.Max(0f, value); }

        public ProjectileSpec ToSpec() => new ProjectileSpec(_speed, _gravityMultiplier, _lifetimeSeconds, _onWorldHit, VisualId);

        // 見た目と音はアセットの側にしか無いので、Spec から写すときは残す。
        public void Set(ProjectileSpec spec)
        {
            if (spec != null)
            {
                _speed = spec.Speed;
                _gravityMultiplier = spec.GravityMultiplier;
                _lifetimeSeconds = spec.LifetimeSeconds;
                _onWorldHit = spec.OnWorldHit;
                VisualId = spec.VisualId;
            }
        }
    }

    [Serializable]
    public sealed class HomingSettings
    {
        [SerializeField, Tooltip("狙い続けて捉えた相手を追う (届け方が Projectile のときだけ)")]
        private bool _enabled;

        [SerializeField, Min(0f), Tooltip("捉えるまで狙い続ける秒数")]
        private float _lockOnSeconds = 1f;

        [SerializeField, UnityEngine.Range(0.01f, 90f), Tooltip("捉えられる、狙いからの角度 (度)")]
        private float _lockOnConeDegrees = 15f;

        [SerializeField, Min(0.01f), Tooltip("弾が向きを変えられる速さ (度/秒)")]
        private float _turnDegreesPerSecond = 120f;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public float LockOnSeconds { get => _lockOnSeconds; set => _lockOnSeconds = value; }

        public float LockOnConeDegrees { get => _lockOnConeDegrees; set => _lockOnConeDegrees = value; }

        public float TurnDegreesPerSecond { get => _turnDegreesPerSecond; set => _turnDegreesPerSecond = value; }

        public HomingSpec ToSpec() => _enabled ? new HomingSpec(_lockOnSeconds, _lockOnConeDegrees, _turnDegreesPerSecond) : null;

        public void Set(HomingSpec spec)
        {
            _enabled = spec != null;
            if (spec != null)
            {
                _lockOnSeconds = spec.LockOnSeconds;
                _lockOnConeDegrees = spec.LockOnConeDegrees;
                _turnDegreesPerSecond = spec.TurnDegreesPerSecond;
            }
        }
    }

    /// <summary>届け方が <see cref="RoundDelivery.Beam"/> のときだけ使う。</summary>
    [Serializable]
    public sealed class BeamSettings
    {
        [SerializeField, Min(0.01f), Tooltip("当てている間にダメージを与える間隔 (秒)")]
        private float _tickSeconds = 0.1f;

        [SerializeField, Tooltip("見た目の参照 Id。ネット対戦のクライアントが見た目を引くのに使う。空なら下の見た目")]
        private string _visualId = string.Empty;

        [SerializeField, Tooltip("ビームの見た目。空ならコードで線を作る")]
        private GameObject _visual;

        [SerializeField, Tooltip("撃っている間に鳴らし続ける音。空なら鳴らさない")]
        private AudioClip _sound;

        public float TickSeconds { get => _tickSeconds; set => _tickSeconds = value; }

        public string VisualId { get => _visualId ?? string.Empty; set => _visualId = value ?? string.Empty; }

        public GameObject Visual { get => _visual; set => _visual = value; }

        public AudioClip Sound { get => _sound; set => _sound = value; }

        public BeamSpec ToSpec() => new BeamSpec(_tickSeconds, VisualId);

        public void Set(BeamSpec spec)
        {
            if (spec != null)
            {
                _tickSeconds = spec.TickSeconds;
                VisualId = spec.VisualId;
            }
        }
    }

    /// <summary>届け方が <see cref="RoundDelivery.Spray"/> (火炎) のときだけ使う。</summary>
    [Serializable]
    public sealed class SpraySettings
    {
        [SerializeField, Min(0.01f), Tooltip("浴びせている間にダメージを与える間隔 (秒)")]
        private float _tickSeconds = 0.1f;

        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("火炎が広がる円錐の角度 (度)")]
        private float _coneDegrees = 10f;

        [SerializeField, Tooltip("見た目の参照 Id。ネット対戦のクライアントが見た目を引くのに使う。空なら下の見た目")]
        private string _visualId = string.Empty;

        [SerializeField, Tooltip("火炎の見た目。空ならコードで粒を作る")]
        private GameObject _visual;

        [SerializeField, Tooltip("浴びせている間に鳴らし続ける音。空なら鳴らさない")]
        private AudioClip _sound;

        public float TickSeconds { get => _tickSeconds; set => _tickSeconds = value; }

        public float ConeDegrees { get => _coneDegrees; set => _coneDegrees = value; }

        public string VisualId { get => _visualId ?? string.Empty; set => _visualId = value ?? string.Empty; }

        public GameObject Visual { get => _visual; set => _visual = value; }

        public AudioClip Sound { get => _sound; set => _sound = value; }

        public SpraySpec ToSpec() => new SpraySpec(_tickSeconds, _coneDegrees, VisualId);

        public void Set(SpraySpec spec)
        {
            if (spec != null)
            {
                _tickSeconds = spec.TickSeconds;
                _coneDegrees = spec.ConeDegrees;
                VisualId = spec.VisualId;
            }
        }
    }

    [Serializable]
    public sealed class ExplosionSettings
    {
        [SerializeField, Tooltip("着弾で爆発する")]
        private bool _enabled;

        [SerializeField, Min(0), Tooltip("爆発の中心でのダメージ")]
        private int _maxDamage = 100;

        [SerializeField, Min(0.01f), Tooltip("爆発が届く半径 (m)")]
        private float _radius = 5f;

        [SerializeField, Min(0f), Tooltip("減らさずに与える半径 (m)。半径より小さい")]
        private float _fullDamageRadius = 1f;

        [SerializeField, Min(0), Tooltip("爆発の中心で建造物に与えるダメージ")]
        private int _structureDamageAtCenter;

        [SerializeField, Tooltip("起爆するとき。止まったとき・寿命が尽きたときは届け方が Projectile のときだけ")]
        private DetonationTrigger _detonation = DetonationTrigger.OnImpact;

        [SerializeField, Tooltip("爆発の見た目。空ならコードで球を作る")]
        private GameObject _visual;

        [SerializeField, Tooltip("爆発の音。空なら鳴らさない")]
        private AudioClip _sound;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public int MaxDamage { get => _maxDamage; set => _maxDamage = value; }

        public float Radius { get => _radius; set => _radius = value; }

        public float FullDamageRadius { get => _fullDamageRadius; set => _fullDamageRadius = value; }

        public int StructureDamageAtCenter { get => _structureDamageAtCenter; set => _structureDamageAtCenter = value; }

        public DetonationTrigger Detonation { get => _detonation; set => _detonation = value; }

        public GameObject Visual { get => _visual; set => _visual = value; }

        public AudioClip Sound { get => _sound; set => _sound = value; }

        public ExplosionSpec ToSpec() =>
            _enabled ? new ExplosionSpec(new ExplosionDamage(_maxDamage, _radius, _fullDamageRadius), _structureDamageAtCenter, _detonation) : null;

        public void Set(ExplosionSpec spec)
        {
            _enabled = spec != null;
            if (spec != null)
            {
                _maxDamage = spec.Damage.MaxDamage;
                _radius = spec.Damage.Radius;
                _fullDamageRadius = spec.Damage.FullDamageRadius;
                _structureDamageAtCenter = spec.StructureDamageAtCenter;
                _detonation = spec.Detonation;
            }
        }
    }

    [Serializable]
    public sealed class StatusEffectSettings
    {
        [SerializeField, Tooltip("当たった相手に状態異常 (燃焼など) を付ける")]
        private bool _enabled;

        [SerializeField, Min(0.01f), Tooltip("続く秒数")]
        private float _durationSeconds = 3f;

        [SerializeField, Min(0.01f), Tooltip("ダメージを与える間隔 (秒)")]
        private float _tickSeconds = 0.5f;

        [SerializeField, Min(0), Tooltip("1 回に与えるダメージ")]
        private int _damagePerTick = 5;

        [SerializeField, Tooltip("見た目の参照 Id")]
        private string _visualId = string.Empty;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public float DurationSeconds { get => _durationSeconds; set => _durationSeconds = value; }

        public float TickSeconds { get => _tickSeconds; set => _tickSeconds = value; }

        public int DamagePerTick { get => _damagePerTick; set => _damagePerTick = value; }

        public string VisualId { get => _visualId ?? string.Empty; set => _visualId = value ?? string.Empty; }

        public StatusEffectSpec ToSpec() => _enabled ? new StatusEffectSpec(_durationSeconds, _tickSeconds, _damagePerTick, VisualId) : null;

        public void Set(StatusEffectSpec spec)
        {
            _enabled = spec != null;
            if (spec != null)
            {
                _durationSeconds = spec.DurationSeconds;
                _tickSeconds = spec.TickSeconds;
                _damagePerTick = spec.DamagePerTick;
                VisualId = spec.VisualId;
            }
        }
    }

    [Serializable]
    public sealed class AreaEffectSettings
    {
        [SerializeField, Tooltip("着弾した場所にしばらく残る範囲 (火の海など) を作る")]
        private bool _enabled;

        [SerializeField, Min(0.01f), Tooltip("範囲の半径 (m)")]
        private float _radius = 4f;

        [SerializeField, Min(0.01f), Tooltip("残る秒数")]
        private float _durationSeconds = 6f;

        [SerializeField, Min(0.01f), Tooltip("中の相手にダメージを与える間隔 (秒)")]
        private float _tickSeconds = 1f;

        [SerializeField, Min(0), Tooltip("1 回に与えるダメージ")]
        private int _damagePerTick = 5;

        [SerializeField, Tooltip("中の相手に付ける状態異常")]
        private StatusEffectSettings _statusEffect = new StatusEffectSettings();

        [SerializeField, Tooltip("見た目の参照 Id")]
        private string _visualId = string.Empty;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public float Radius { get => _radius; set => _radius = value; }

        public float DurationSeconds { get => _durationSeconds; set => _durationSeconds = value; }

        public float TickSeconds { get => _tickSeconds; set => _tickSeconds = value; }

        public int DamagePerTick { get => _damagePerTick; set => _damagePerTick = value; }

        public StatusEffectSettings StatusEffect => _statusEffect ??= new StatusEffectSettings();

        public string VisualId { get => _visualId ?? string.Empty; set => _visualId = value ?? string.Empty; }

        public AreaEffectSpec ToSpec() =>
            _enabled ? new AreaEffectSpec(_radius, _durationSeconds, _tickSeconds, _damagePerTick, StatusEffect.ToSpec(), VisualId) : null;

        public void Set(AreaEffectSpec spec)
        {
            _enabled = spec != null;
            if (spec != null)
            {
                _radius = spec.Radius;
                _durationSeconds = spec.DurationSeconds;
                _tickSeconds = spec.TickSeconds;
                _damagePerTick = spec.DamagePerTick;
                StatusEffect.Set(spec.StatusEffect);
                VisualId = spec.VisualId;
            }
        }
    }

    [Serializable]
    public sealed class BreathHoldSettings
    {
        [SerializeField, Tooltip("覗き込み中に息を止めて揺れを抑えられる")]
        private bool _enabled;

        [SerializeField, Min(0.01f), Tooltip("息を止めていられる秒数")]
        private float _maxHoldSeconds = 4f;

        [SerializeField, Min(0f), Tooltip("止め切った後に息が戻るまでの秒数")]
        private float _recoverSeconds = 2f;

        [SerializeField, UnityEngine.Range(0f, 1f), Tooltip("息を止めている間の揺れの倍率")]
        private float _swayMultiplier = 0.2f;

        public bool Enabled { get => _enabled; set => _enabled = value; }

        public float MaxHoldSeconds { get => _maxHoldSeconds; set => _maxHoldSeconds = value; }

        public float RecoverSeconds { get => _recoverSeconds; set => _recoverSeconds = value; }

        public float SwayMultiplier { get => _swayMultiplier; set => _swayMultiplier = value; }

        public BreathHoldSpec ToSpec() => _enabled ? new BreathHoldSpec(_maxHoldSeconds, _recoverSeconds, _swayMultiplier) : null;

        public void Set(BreathHoldSpec spec)
        {
            _enabled = spec != null;
            if (spec != null)
            {
                _maxHoldSeconds = spec.MaxHoldSeconds;
                _recoverSeconds = spec.RecoverSeconds;
                _swayMultiplier = spec.SwayMultiplier;
            }
        }
    }

    /// <summary>弾の性質 (<see cref="RoundSpec"/>) 1 つ分。弾薬の種類 (<see cref="AmmoTypeSettings"/>) が武器の弾を上書きするのに使う。</summary>
    [Serializable]
    public sealed class RoundSettings
    {
        [SerializeField, Tooltip("弾の届け方")]
        private RoundDelivery _delivery = RoundDelivery.Hitscan;

        [SerializeField, Min(0.01f), Tooltip("射程 (m)")]
        private float _range = RoundSpec.DefaultRange;

        [SerializeField, Min(1), Tooltip("1 発で出る弾 (ペレット) の数")]
        private int _pelletCount = 1;

        [SerializeField, UnityEngine.Range(0f, 90f), Tooltip("ペレットが散らばる最大の角度 (度)")]
        private float _pelletSpreadDegrees;

        [SerializeField, Min(0f), Tooltip("弾の当たり判定の半径 (m)。0 なら細い線")]
        private float _radius;

        [SerializeField]
        private PenetrationSettings _penetration = new PenetrationSettings();

        [SerializeField]
        private RicochetSettings _ricochet = new RicochetSettings();

        [SerializeField]
        private ProjectileSettings _projectile = new ProjectileSettings();

        [SerializeField]
        private HomingSettings _homing = new HomingSettings();

        [SerializeField]
        private BeamSettings _beam = new BeamSettings();

        [SerializeField]
        private SpraySettings _spray = new SpraySettings();

        public RoundDelivery Delivery { get => _delivery; set => _delivery = value; }

        public float Range { get => _range; set => _range = value; }

        public int PelletCount { get => _pelletCount; set => _pelletCount = value; }

        public float PelletSpreadDegrees { get => _pelletSpreadDegrees; set => _pelletSpreadDegrees = value; }

        public float Radius { get => _radius; set => _radius = value; }

        public PenetrationSettings Penetration => _penetration ??= new PenetrationSettings();

        public RicochetSettings Ricochet => _ricochet ??= new RicochetSettings();

        public ProjectileSettings Projectile => _projectile ??= new ProjectileSettings();

        public HomingSettings Homing => _homing ??= new HomingSettings();

        public BeamSettings Beam => _beam ??= new BeamSettings();

        public SpraySettings Spray => _spray ??= new SpraySettings();

        public RoundSpec ToSpec()
        {
            return Build(_delivery, _range, _pelletCount, _pelletSpreadDegrees, _radius, Penetration, Ricochet, Projectile, Homing, Beam, Spray);
        }

        public void Set(RoundSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _delivery = spec.Delivery;
            _range = spec.Range;
            _pelletCount = spec.PelletCount;
            _pelletSpreadDegrees = spec.PelletSpreadDegrees;
            _radius = spec.Radius;
            Penetration.Set(spec.Penetration);
            Ricochet.Set(spec.Ricochet);
            Projectile.Set(spec.Projectile);
            Homing.Set(spec.Homing);
            Beam.Set(spec.Beam);
            Spray.Set(spec.Spray);
        }

        /// <summary>届け方に合う入れ子 (飛ぶ弾・ビーム・火炎) だけを入れて <see cref="RoundSpec"/> を作る。</summary>
        public static RoundSpec Build(
            RoundDelivery delivery,
            float range,
            int pelletCount,
            float pelletSpreadDegrees,
            float radius,
            PenetrationSettings penetration,
            RicochetSettings ricochet,
            ProjectileSettings projectile,
            HomingSettings homing,
            BeamSettings beam,
            SpraySettings spray)
        {
            return new RoundSpec(
                delivery,
                range,
                pelletCount,
                pelletSpreadDegrees,
                radius,
                penetration.ToSpec(),
                ricochet.ToSpec(),
                delivery == RoundDelivery.Projectile ? projectile.ToSpec() : null,
                homing.ToSpec(),
                delivery == RoundDelivery.Beam ? beam.ToSpec() : null,
                delivery == RoundDelivery.Spray ? spray.ToSpec() : null);
        }
    }

    /// <summary>着弾時の効果 (<see cref="ImpactSpec"/>) 1 つ分。弾薬の種類 (<see cref="AmmoTypeSettings"/>) が武器の着弾を上書きするのに使う。</summary>
    [Serializable]
    public sealed class ImpactSettings
    {
        [SerializeField, Min(0), Tooltip("1 発あたりの基本ダメージ")]
        private int _damage = ImpactSpec.DefaultDamage;

        [SerializeField, Min(0f), Tooltip("この距離までは減衰しない (m)")]
        private float _falloffStart = float.MaxValue;

        [SerializeField, Min(0f), Tooltip("この距離以上で最小倍率になる (m)")]
        private float _falloffEnd = float.MaxValue;

        [SerializeField, UnityEngine.Range(0f, 1f), Tooltip("最大まで減衰したときの倍率")]
        private float _falloffMinMultiplier = 1f;

        [SerializeField, Min(0f), Tooltip("建造物に当たったときに被害を広げる半径 (m)。0 なら当たった物だけ")]
        private float _structureRadius;

        [SerializeField]
        private ExplosionSettings _explosion = new ExplosionSettings();

        [SerializeField]
        private StatusEffectSettings _statusEffect = new StatusEffectSettings();

        [SerializeField]
        private AreaEffectSettings _areaEffect = new AreaEffectSettings();

        public int Damage { get => _damage; set => _damage = value; }

        public float FalloffStart { get => _falloffStart; set => _falloffStart = value; }

        public float FalloffEnd { get => _falloffEnd; set => _falloffEnd = value; }

        public float FalloffMinMultiplier { get => _falloffMinMultiplier; set => _falloffMinMultiplier = value; }

        public float StructureRadius { get => _structureRadius; set => _structureRadius = value; }

        public ExplosionSettings Explosion => _explosion ??= new ExplosionSettings();

        public StatusEffectSettings StatusEffect => _statusEffect ??= new StatusEffectSettings();

        public AreaEffectSettings AreaEffect => _areaEffect ??= new AreaEffectSettings();

        public ImpactSpec ToSpec()
        {
            return new ImpactSpec(
                _damage,
                new DamageFalloff(_falloffStart, _falloffEnd, _falloffMinMultiplier),
                _structureRadius,
                Explosion.ToSpec(),
                StatusEffect.ToSpec(),
                AreaEffect.ToSpec());
        }

        public void Set(ImpactSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _damage = spec.Damage;
            _falloffStart = spec.Falloff.StartDistance;
            _falloffEnd = spec.Falloff.EndDistance;
            _falloffMinMultiplier = spec.Falloff.MinMultiplier;
            _structureRadius = spec.StructureRadius;
            Explosion.Set(spec.Explosion);
            StatusEffect.Set(spec.StatusEffect);
            AreaEffect.Set(spec.AreaEffect);
        }
    }

    /// <summary>弾薬の種類 (<see cref="AmmoTypeSpec"/>) 1 つ分。上書きしない部品は武器のものを使う。</summary>
    [Serializable]
    public sealed class AmmoTypeSettings
    {
        [SerializeField, Tooltip("弾薬の種類の識別子。小文字・ハイフン区切りの UUID。作った後は変えない")]
        private string _id = string.Empty;

        [SerializeField, Tooltip("プレイヤーに見せる名前を引く表のキー (例: ammo.dmr.armorPiercing)。空なら Id を出す")]
        private string _nameKey = string.Empty;

        [SerializeField, Tooltip("武器の弾の性質を上書きする")]
        private bool _overridesRound;

        [SerializeField]
        private RoundSettings _round = new RoundSettings();

        [SerializeField, Tooltip("武器の着弾時の効果を上書きする")]
        private bool _overridesImpact;

        [SerializeField]
        private ImpactSettings _impact = new ImpactSettings();

        public string Id { get => _id ?? string.Empty; set => _id = value ?? string.Empty; }

        public string NameKey { get => _nameKey ?? string.Empty; set => _nameKey = value ?? string.Empty; }

        public bool OverridesRound { get => _overridesRound; set => _overridesRound = value; }

        public RoundSettings Round => _round ??= new RoundSettings();

        public bool OverridesImpact { get => _overridesImpact; set => _overridesImpact = value; }

        public ImpactSettings Impact => _impact ??= new ImpactSettings();

        public AmmoTypeSpec ToSpec()
        {
            return new AmmoTypeSpec(Id, NameKey, _overridesRound ? Round.ToSpec() : null, _overridesImpact ? Impact.ToSpec() : null);
        }

        public void Set(AmmoTypeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            Id = spec.Id;
            NameKey = spec.NameKey;
            _overridesRound = spec.Round != null;
            if (spec.Round != null)
            {
                Round.Set(spec.Round);
            }

            _overridesImpact = spec.Impact != null;
            if (spec.Impact != null)
            {
                Impact.Set(spec.Impact);
            }
        }

        internal static List<AmmoTypeSpec> ToSpecs(IReadOnlyList<AmmoTypeSettings> settings)
        {
            var specs = new List<AmmoTypeSpec>(settings?.Count ?? 0);
            if (settings != null)
            {
                foreach (var ammoType in settings)
                {
                    specs.Add(ammoType?.ToSpec());
                }
            }

            return specs;
        }
    }
}
