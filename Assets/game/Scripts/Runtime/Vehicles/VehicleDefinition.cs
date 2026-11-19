using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 座席の並びはシーンの VehicleSeat の側にあるので、アセットには持たず ToSpec で座席の設定と合わせる。
    [CreateAssetMenu(fileName = "Vehicle", menuName = "Shooter/Vehicle Definition")]
    public sealed class VehicleDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("乗り物の Id。ゲームのデータがこの乗り物を指すのに使う")]
        private string _id = VehicleSpec.DefaultId;

        [SerializeField, Min(1), Tooltip("耐久")]
        private int _maxHealth = VehicleSpec.DefaultMaxHealth;

        [SerializeField, Min(0.01f), Tooltip("座席の乗る位置からこの距離 (m) までなら乗り込める")]
        private float _enterRange = VehicleSpec.DefaultEnterRange;

        [SerializeField, Min(0f), Tooltip("プレイヤーが乗り込みを始めてから座席に着くまでの秒数。0 ならすぐ座る")]
        private float _enterSeconds = VehicleSpec.DefaultEnterSeconds;

        [SerializeField, Min(0f), Tooltip("プレイヤーが降りると決めてから降りるまでの秒数。0 ならすぐ降りる")]
        private float _exitSeconds = VehicleSpec.DefaultExitSeconds;

        [SerializeField, Min(0f), Tooltip("追うカメラを注視点からどれだけ離すか (m)")]
        private float _chaseDistance = VehicleSpec.DefaultChaseDistance;

        [SerializeField, Tooltip("追うカメラの注視点を、乗り物の原点からどれだけ上に置くか (m)")]
        private float _chaseHeight = VehicleSpec.DefaultChaseHeight;

        [SerializeField, Min(0f), Tooltip("前へ進む速さ (m/s) がこれを超えると画角を広げ始める")]
        private float _speedFieldOfViewStartSpeed = VehicleSpeedFieldOfViewSpec.DefaultStartSpeed;

        [SerializeField, Min(0f), Tooltip("前へ進む速さ (m/s) がこれに達すると足す画角を全部足す。広げ始める速さより大きくする")]
        private float _speedFieldOfViewFullSpeed = VehicleSpeedFieldOfViewSpec.DefaultFullSpeed;

        [SerializeField, Range(0f, VehicleSpeedFieldOfViewSpec.MaxExtraDegrees), Tooltip("速いときにカメラの画角へ足す角度 (度)。0 なら画角を変えない")]
        private float _speedFieldOfViewExtraDegrees = VehicleSpeedFieldOfViewSpec.DefaultExtraDegrees;

        [SerializeField, Min(0f), Tooltip("画角を目標へ寄せる速さ。おおよそこの秒数で 6 割寄る。0 ならすぐに合わせる")]
        private float _speedFieldOfViewBlendSeconds = VehicleSpeedFieldOfViewSpec.DefaultBlendSeconds;

        [SerializeField, Tooltip("部位 (エンジン・燃料など) の Id と、そこに当たったダメージに掛ける倍率。シーンの VehiclePart が Id で指す")]
        private Part[] _parts = Array.Empty<Part>();

        [SerializeField, Range(0f, 1f), Tooltip("耐久の残りがこの割合以下で煙が出る。0 なら煙の段階を飛ばす")]
        private float _smokeHealthFraction = VehicleDurabilitySpec.DefaultSmokeHealthFraction;

        [SerializeField, Range(0f, 1f), Tooltip("耐久の残りがこの割合以下で燃え、時間で耐久が減り続ける。煙の割合以下。0 なら燃えない")]
        private float _burnHealthFraction = VehicleDurabilitySpec.DefaultBurnHealthFraction;

        [SerializeField, Min(0f), Tooltip("燃えている間に 1 秒で減る耐久")]
        private float _burnDamagePerSecond = VehicleDurabilitySpec.DefaultBurnDamagePerSecond;

        [SerializeField, Min(0), Tooltip("耐久が 0 で爆発したとき、中心で人に与えるダメージ")]
        private int _explosionMaxDamage = VehicleDurabilitySpec.DefaultExplosionMaxDamage;

        [SerializeField, Min(0.01f), Tooltip("爆発が届く半径 (m)")]
        private float _explosionRadius = VehicleDurabilitySpec.DefaultExplosionRadius;

        [SerializeField, Min(0f), Tooltip("減らさずに最大のダメージを与える半径 (m)。届く半径より小さくする")]
        private float _explosionFullDamageRadius = VehicleDurabilitySpec.DefaultExplosionFullDamageRadius;

        [SerializeField, Min(0), Tooltip("爆発の中心で建造物に与える被害")]
        private int _explosionStructureDamage = VehicleDurabilitySpec.DefaultExplosionStructureDamage;

        [SerializeField, Tooltip("空中で降りたとき・壊れたときの脱出")]
        private BailOutSettings _bailOut = new BailOutSettings();

        public string Id
        {
            get => _id;
            set => _id = value;
        }

        public int MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = value;
        }

        public float EnterRange
        {
            get => _enterRange;
            set => _enterRange = value;
        }

        public float EnterSeconds
        {
            get => _enterSeconds;
            set => _enterSeconds = value;
        }

        public float ExitSeconds
        {
            get => _exitSeconds;
            set => _exitSeconds = value;
        }

        public float ChaseDistance
        {
            get => _chaseDistance;
            set => _chaseDistance = value;
        }

        public float ChaseHeight
        {
            get => _chaseHeight;
            set => _chaseHeight = value;
        }

        public float SpeedFieldOfViewStartSpeed
        {
            get => _speedFieldOfViewStartSpeed;
            set => _speedFieldOfViewStartSpeed = value;
        }

        public float SpeedFieldOfViewFullSpeed
        {
            get => _speedFieldOfViewFullSpeed;
            set => _speedFieldOfViewFullSpeed = value;
        }

        public float SpeedFieldOfViewExtraDegrees
        {
            get => _speedFieldOfViewExtraDegrees;
            set => _speedFieldOfViewExtraDegrees = value;
        }

        public float SpeedFieldOfViewBlendSeconds
        {
            get => _speedFieldOfViewBlendSeconds;
            set => _speedFieldOfViewBlendSeconds = value;
        }

        public Part[] Parts
        {
            get => _parts;
            set => _parts = value ?? Array.Empty<Part>();
        }

        public float SmokeHealthFraction
        {
            get => _smokeHealthFraction;
            set => _smokeHealthFraction = value;
        }

        public float BurnHealthFraction
        {
            get => _burnHealthFraction;
            set => _burnHealthFraction = value;
        }

        public float BurnDamagePerSecond
        {
            get => _burnDamagePerSecond;
            set => _burnDamagePerSecond = value;
        }

        public int ExplosionMaxDamage
        {
            get => _explosionMaxDamage;
            set => _explosionMaxDamage = value;
        }

        public float ExplosionRadius
        {
            get => _explosionRadius;
            set => _explosionRadius = value;
        }

        public float ExplosionFullDamageRadius
        {
            get => _explosionFullDamageRadius;
            set => _explosionFullDamageRadius = value;
        }

        public int ExplosionStructureDamage
        {
            get => _explosionStructureDamage;
            set => _explosionStructureDamage = value;
        }

        public BailOutSettings BailOut => _bailOut ??= new BailOutSettings();

        public VehicleSpec ToSpec(IReadOnlyList<VehicleSeatSpec> seats)
        {
            return ToSpec(seats, null);
        }

        // 武器も座席と同じく置き場所がシーンの側 (VehicleWeaponMount) にあるので、その並びを受け取って合わせる。
        public VehicleSpec ToSpec(IReadOnlyList<VehicleSeatSpec> seats, IReadOnlyList<VehicleWeaponSpec> weapons)
        {
            if (seats == null)
            {
                throw new ArgumentNullException(nameof(seats));
            }

            var source = string.IsNullOrEmpty(name) ? "(unnamed vehicle)" : name;
            try
            {
                return new VehicleSpec(
                    _id,
                    _maxHealth,
                    seats,
                    weapons,
                    PartSpecs(),
                    _enterRange,
                    _chaseDistance,
                    _chaseHeight,
                    DurabilitySpec(),
                    new VehicleSpeedFieldOfViewSpec(
                        _speedFieldOfViewStartSpeed,
                        _speedFieldOfViewFullSpeed,
                        _speedFieldOfViewExtraDegrees,
                        _speedFieldOfViewBlendSeconds),
                    BailOut.ToSpec(),
                    _enterSeconds,
                    _exitSeconds);
            }
            catch (ArgumentException e)
            {
                throw new VehicleDefinitionException($"vehicle '{source}' is invalid: {e.Message}", e);
            }
        }

        public void CopyFrom(VehicleSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _id = spec.Id;
            _maxHealth = spec.MaxHealth;
            _enterRange = spec.EnterRange;
            _enterSeconds = spec.EnterSeconds;
            _exitSeconds = spec.ExitSeconds;
            _chaseDistance = spec.ChaseDistance;
            _chaseHeight = spec.ChaseHeight;
            var speedFieldOfView = spec.SpeedFieldOfView;
            _speedFieldOfViewStartSpeed = speedFieldOfView.StartSpeed;
            _speedFieldOfViewFullSpeed = speedFieldOfView.FullSpeed;
            _speedFieldOfViewExtraDegrees = speedFieldOfView.ExtraDegrees;
            _speedFieldOfViewBlendSeconds = speedFieldOfView.BlendSeconds;
            _parts = new Part[spec.Parts.Count];
            for (var i = 0; i < _parts.Length; i++)
            {
                _parts[i] = new Part(spec.Parts[i].Id, spec.Parts[i].DamageMultiplier);
            }

            var durability = spec.Durability;
            _smokeHealthFraction = durability.SmokeHealthFraction;
            _burnHealthFraction = durability.BurnHealthFraction;
            _burnDamagePerSecond = durability.BurnDamagePerSecond;
            _explosionMaxDamage = durability.Explosion.Damage.MaxDamage;
            _explosionRadius = durability.Explosion.Damage.Radius;
            _explosionFullDamageRadius = durability.Explosion.Damage.FullDamageRadius;
            _explosionStructureDamage = durability.Explosion.StructureDamageAtCenter;
            BailOut.CopyFrom(spec.BailOut);
        }

        private VehiclePartSpec[] PartSpecs()
        {
            var parts = _parts ?? Array.Empty<Part>();
            var specs = new VehiclePartSpec[parts.Length];
            for (var i = 0; i < specs.Length; i++)
            {
                var part = parts[i] ?? throw new ArgumentException($"parts[{i}] is empty");
                specs[i] = new VehiclePartSpec(part.Id, part.DamageMultiplier);
            }

            return specs;
        }

        private VehicleDurabilitySpec DurabilitySpec()
        {
            return new VehicleDurabilitySpec(
                _smokeHealthFraction,
                _burnHealthFraction,
                _burnDamagePerSecond,
                new ExplosionSpec(
                    new ExplosionDamage(_explosionMaxDamage, _explosionRadius, _explosionFullDamageRadius),
                    _explosionStructureDamage));
        }

        [Serializable]
        public sealed class Part
        {
            [SerializeField, Tooltip("部位の Id。1 台の中で重ならない")]
            private string _id = string.Empty;

            [SerializeField, Min(0f), Tooltip("この部位に当たったダメージに掛ける倍率")]
            private float _damageMultiplier = 1f;

            public Part()
            {
            }

            public Part(string id, float damageMultiplier)
            {
                _id = id;
                _damageMultiplier = damageMultiplier;
            }

            public string Id => _id;

            public float DamageMultiplier => _damageMultiplier;
        }
    }

    // ArgumentException から派生させるのは、VehicleSpec の不正な値を ArgumentException で受ける呼び出し側を変えずに済ませるため。
    public sealed class VehicleDefinitionException : ArgumentException
    {
        public VehicleDefinitionException(string message)
            : base(message)
        {
        }

        public VehicleDefinitionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
