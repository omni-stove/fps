using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ExplosiveProp : MonoBehaviour, IStructureHittable
    {
        public const string LogTag = ExplosivePropLog.Tag;

        public const string WeaponId = "explosive";

        // StructureDamage は被害の出どころを渡さないので、置き物の爆発が被害を配っている最中かどうかで連鎖を見分ける。
        private static int _blastDepth;

        [SerializeField, Tooltip("ログの識別子。空なら GameObject の名前")]
        private string _id;

        [SerializeField, Tooltip("この置き物の値のアセット。配線すると下の耐久・ダメージ・半径・連鎖・燃え方の代わりにこれを読む。空なら下の値を使う")]
        private ExplosivePropDefinition _definition;

        [SerializeField, Min(1), Tooltip("耐久。被害でこれが 0 になると爆発する (燃えるなら燃え始める)")]
        private int _maxHealth = ExplosivePropSpec.DefaultMaxHealth;

        [SerializeField, Min(0), Tooltip("爆発の中心で人に与えるダメージ")]
        private int _maxDamage = ExplosivePropSpec.DefaultMaxDamage;

        [SerializeField, Min(0.01f), Tooltip("爆発が届く半径 (m)。建造物とほかの置き物への被害もこの半径")]
        private float _damageRadius = ExplosivePropSpec.DefaultDamageRadius;

        [SerializeField, Min(0f), Tooltip("減らさずに最大のダメージを与える半径 (m)。届く半径より小さくする")]
        private float _fullDamageRadius = ExplosivePropSpec.DefaultFullDamageRadius;

        [SerializeField, Min(0), Tooltip("爆発の中心で建造物とほかの置き物に与える被害。0 なら連鎖もしない")]
        private int _structureDamageAtCenter = ExplosivePropSpec.DefaultStructureDamageAtCenter;

        [SerializeField, Min(0f), Tooltip("ほかの置き物の爆発で耐久が 0 になったとき、爆発するまで待つ秒数の下限")]
        private float _chainDelaySeconds = ExplosivePropSpec.DefaultChainDelaySeconds;

        [SerializeField, Min(0f), Tooltip("耐久が 0 になってから燃え続け、爆発するまでの秒数。0 なら燃えずに爆発する")]
        private float _burnSeconds = ExplosivePropSpec.DefaultBurnSeconds;

        private readonly List<Collider> _ownColliders = new List<Collider>();
        private ExplosiveCharge _charge;
        private bool _invalid;
        private GameObject _instigator;
        private bool _exploded;

        public event Action<ExplosiveProp, ExplosiveChargeStep> Triggered;

        public event Action<ExplosiveProp, ExplosivePropExplosion> Exploded;

        public static bool IsBlastInProgress => _blastDepth > 0;

        public string Id
        {
            get => string.IsNullOrEmpty(_id) ? name : _id;
            set => _id = value;
        }

        public ExplosivePropDefinition Definition => _definition;

        public ExplosivePropSpec Spec => TryEnsureCharge() ? _charge.Spec : null;

        public int Health => TryEnsureCharge() ? _charge.Health : 0;

        public bool IsTriggered => TryEnsureCharge() && _charge.IsTriggered;

        public bool IsBurning => TryEnsureCharge() && _charge.IsBurning;

        public bool IsChained => TryEnsureCharge() && _charge.IsChained;

        public float RemainingSeconds => TryEnsureCharge() ? _charge.RemainingSeconds : 0f;

        public bool HasExploded => _exploded;

        private void Awake()
        {
            TryEnsureCharge();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        public void Configure(ExplosivePropSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (_exploded)
            {
                return;
            }

            _maxHealth = spec.MaxHealth;
            _maxDamage = spec.MaxDamage;
            _damageRadius = spec.DamageRadius;
            _fullDamageRadius = spec.FullDamageRadius;
            _structureDamageAtCenter = spec.StructureDamageAtCenter;
            _chainDelaySeconds = spec.ChainDelaySeconds;
            _burnSeconds = spec.BurnSeconds;
            _charge = new ExplosiveCharge(spec);
            _invalid = false;
            _instigator = null;
        }

        public void Configure(ExplosivePropDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (_exploded)
            {
                return;
            }

            var spec = definition.ToSpec();
            _definition = definition;
            Configure(spec);
        }

        public void ApplyStructureHit(Collider collider, Vector3 point, int damage)
        {
            ApplyDamage(damage, IsBlastInProgress);
        }

        public ExplosiveChargeStep ApplyDamage(int damage, bool chained)
        {
            if (damage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative");
            }

            if (_exploded || !TryEnsureCharge())
            {
                return ExplosiveChargeStep.None;
            }

            var step = _charge.ApplyDamage(damage, chained);
            switch (step)
            {
                case ExplosiveChargeStep.Damaged:
                    Debug.Log(ExplosivePropLog.Damaged(Id, damage, _charge), this);
                    break;
                case ExplosiveChargeStep.Triggered:
                case ExplosiveChargeStep.Ignited:
                    var attribution = CombatEvents.CurrentAttribution;
                    _instigator = attribution.HasValue ? CombatEvents.OfflineObject(attribution.Value.KillerId) : null;
                    Debug.Log(ExplosivePropLog.Triggered(Id, step, _charge), this);
                    Triggered?.Invoke(this, step);
                    break;
            }

            return step;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_exploded || !TryEnsureCharge() || !_charge.Tick(deltaSeconds))
            {
                return false;
            }

            Explode();
            return true;
        }

        private void Explode()
        {
            _exploded = true;
            var spec = _charge.Spec;
            var center = BlastCenter();

            // 自分のコライダーが中心から引く線を遮ったり、自分にもう一度被害を配ったりしないよう、先に当たり判定から外す。
            GetComponentsInChildren(_ownColliders);
            foreach (var own in _ownColliders)
            {
                own.enabled = false;
            }

            var damaged = 0;
            var killed = 0;
            var structures = 0;
            using (CombatEvents.Attribute(_instigator, WeaponId))
            {
                foreach (var target in FindTargets(center, spec.DamageRadius))
                {
                    if (TryDamage(target, center, spec.Explosion, out var killedTarget))
                    {
                        damaged++;
                        if (killedTarget)
                        {
                            killed++;
                        }
                    }
                }

                if (spec.StructureDamageAtCenter > 0)
                {
                    _blastDepth++;
                    try
                    {
                        structures = StructureDamage.ApplyAround(center, spec.StructureDamageAtCenter, spec.DamageRadius);
                    }
                    finally
                    {
                        _blastDepth--;
                    }
                }
            }

            var result = new ExplosivePropExplosion(center, _charge.IsChained, damaged, killed, structures);
            Debug.Log(ExplosivePropLog.Exploded(Id, result.Chained, center.x, center.y, center.z, damaged, killed, structures), this);

            ThrownGrenade.PlayExplosionEffects(center);
            ExplosionRangeDebug.Report(center, spec.DamageRadius, spec.FullDamageRadius);
            try
            {
                Exploded?.Invoke(this, result);
            }
            finally
            {
                DestroySafely(gameObject);
            }
        }

        private Vector3 BlastCenter()
        {
            GetComponentsInChildren(_ownColliders);
            var found = false;
            var bounds = default(Bounds);
            foreach (var own in _ownColliders)
            {
                if (!own.enabled || own.isTrigger)
                {
                    continue;
                }

                if (found)
                {
                    bounds.Encapsulate(own.bounds);
                }
                else
                {
                    bounds = own.bounds;
                    found = true;
                }
            }

            return found ? bounds.center : transform.position;
        }

        private bool TryDamage(Damageable target, Vector3 center, ExplosionDamage explosion, out bool killedTarget)
        {
            killedTarget = false;
            if (target.IsDead)
            {
                return false;
            }

            var distance = ThrownGrenade.DistanceTo(center, target);
            if (!explosion.Reaches(distance))
            {
                return false;
            }

            var damage = explosion.At(distance, ThrownGrenade.IsObstructed(center, target));
            if (damage <= 0)
            {
                return false;
            }

            target.TakeDamage(damage, center, target.AimPoint - center, DamageKind.Explosion);
            killedTarget = target.IsDead;
            Debug.Log(ExplosivePropLog.Hit(Id, target.Id, damage, distance), target);
            return true;
        }

        private static List<Damageable> FindTargets(Vector3 center, float radius)
        {
            var targets = new List<Damageable>();
            var seen = new HashSet<Damageable>();
            foreach (var collider in Physics.OverlapSphere(center, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (collider == null)
                {
                    continue;
                }

                var hitbox = Hitbox.FindFor(collider);
                var damageable = hitbox != null && hitbox.Owner != null ? hitbox.Owner : collider.GetComponentInParent<Damageable>();
                if (damageable != null && seen.Add(damageable))
                {
                    targets.Add(damageable);
                }
            }

            return targets;
        }

        // 定義か Inspector の値が不正なら、撃った側を止めないよう 1 度だけエラーを出して以後の被害を無視する。
        // 不正な定義の代わりに Inspector の値で爆発させると、作った人が狙った威力と違うまま気づかれないので使わない。
        private bool TryEnsureCharge()
        {
            if (_charge != null)
            {
                return true;
            }

            if (_invalid)
            {
                return false;
            }

            try
            {
                _charge = new ExplosiveCharge(CreateSpec());
                return true;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
            {
                _invalid = true;
                Debug.LogError($"{LogTag} ExplosiveProp '{name}' の設定が不正です: {e.Message}", this);
                return false;
            }
        }

        private ExplosivePropSpec CreateSpec()
        {
            if (_definition != null)
            {
                return _definition.ToSpec();
            }

            return new ExplosivePropSpec(
                _maxHealth,
                _maxDamage,
                _damageRadius,
                _fullDamageRadius,
                _structureDamageAtCenter,
                _chainDelaySeconds,
                _burnSeconds);
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

    public readonly struct ExplosivePropExplosion
    {
        public Vector3 Center { get; }

        public bool Chained { get; }

        public int Damaged { get; }

        public int Killed { get; }

        public int StructureHits { get; }

        public ExplosivePropExplosion(Vector3 center, bool chained, int damaged, int killed, int structureHits)
        {
            Center = center;
            Chained = chained;
            Damaged = damaged;
            Killed = killed;
            StructureHits = structureHits;
        }
    }
}
