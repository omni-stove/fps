using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 乗り物 1 種類の値 (Id・耐久・部位の倍率・傷み方と爆発・乗り込める距離・追うカメラの置き方) をアセットとして持つ。
    /// <see cref="Vehicle"/> に入れると、プレハブの Inspector の値の代わりにこれを使う。
    /// 座席の並びはシーンの <see cref="VehicleSeat"/> の側にあるので、<see cref="ToSpec"/> で座席の設定と合わせて <see cref="VehicleSpec"/> にする。
    /// 検証は <see cref="VehicleSpec"/> に任せ、失敗したらどのアセットの値かを添えて <see cref="VehicleDefinitionException"/> を投げる。
    /// </summary>
    [CreateAssetMenu(fileName = "Vehicle", menuName = "Fps/Vehicle Definition")]
    public sealed class VehicleDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("乗り物の Id。ゲームのデータがこの乗り物を指すのに使う")]
        private string _id = VehicleSpec.DefaultId;

        [SerializeField, Min(1), Tooltip("耐久")]
        private int _maxHealth = VehicleSpec.DefaultMaxHealth;

        [SerializeField, Min(0.01f), Tooltip("座席の乗る位置からこの距離 (m) までなら乗り込める")]
        private float _enterRange = VehicleSpec.DefaultEnterRange;

        [SerializeField, Min(0f), Tooltip("追うカメラを注視点からどれだけ離すか (m)")]
        private float _chaseDistance = VehicleSpec.DefaultChaseDistance;

        [SerializeField, Tooltip("追うカメラの注視点を、乗り物の原点からどれだけ上に置くか (m)")]
        private float _chaseHeight = VehicleSpec.DefaultChaseHeight;

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

        /// <summary>この値と座席の並び seats で <see cref="VehicleSpec"/> を作る。値が不正なら <see cref="VehicleDefinitionException"/>。</summary>
        public VehicleSpec ToSpec(IReadOnlyList<VehicleSeatSpec> seats)
        {
            if (seats == null)
            {
                throw new ArgumentNullException(nameof(seats));
            }

            var source = string.IsNullOrEmpty(name) ? "(unnamed vehicle)" : name;
            try
            {
                return new VehicleSpec(
                    _id, _maxHealth, seats, null, PartSpecs(), _enterRange, _chaseDistance, _chaseHeight, DurabilitySpec());
            }
            catch (ArgumentException e)
            {
                throw new VehicleDefinitionException($"vehicle '{source}' is invalid: {e.Message}", e);
            }
        }

        /// <summary>spec の Id・耐久・部位・傷み方と爆発・乗り込める距離・追うカメラの値を写す (座席・武器は持たないので写さない)。</summary>
        public void CopyFrom(VehicleSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _id = spec.Id;
            _maxHealth = spec.MaxHealth;
            _enterRange = spec.EnterRange;
            _chaseDistance = spec.ChaseDistance;
            _chaseHeight = spec.ChaseHeight;
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
