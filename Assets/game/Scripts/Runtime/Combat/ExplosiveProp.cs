using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 撃たれたり爆発に巻き込まれたりすると耐久が減り、0 で爆発する置き物 (樽・ガソリン缶など)。
    /// 被害は建造物と同じ口 (<see cref="IStructureHittable"/>) で受けるので、弾 (<see cref="StructureDamage.Apply"/>) にも
    /// グレネードやほかの置き物の爆発 (<see cref="StructureDamage.ApplyAround"/>) にも当たる。値は <see cref="ExplosivePropSpec"/>。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>耐久が 0 になると、<see cref="ExplosivePropSpec.DetonationDelay"/> の秒数だけ待ってから <see cref="Update"/> (<see cref="Tick"/>) で爆発する。
    /// ほかの置き物の爆発で 0 になったとき (連鎖) は <see cref="ExplosivePropSpec.ChainDelaySeconds"/> 以上待つ。
    /// <see cref="ExplosivePropSpec.BurnSeconds"/> があれば、その間は燃えてから爆発する。</item>
    /// <item>爆発は半径内の <see cref="Damageable"/> に <see cref="ExplosionDamage"/> を与え (遮り方はグレネードと同じ <see cref="ThrownGrenade.IsObstructed"/>)、
    /// <see cref="StructureDamage.ApplyAround"/> で建造物とほかの置き物に被害を配る。建造物が壊れるかは建造物の側
    /// (<see cref="DestructibleStructure.DestructionEnabled"/>、ルールの <see cref="GameRules.Destruction"/>) で決まる。</item>
    /// <item>演出はグレネードと同じ <see cref="ThrownGrenade.PlayExplosionEffects"/> を出し、置き物の GameObject を消す。</item>
    /// <item>耐久を 0 にしたときの帰属 (<see cref="CombatEvents.CurrentAttribution"/>) を覚え、爆発で倒した相手はその者が
    /// <see cref="WeaponId"/> で倒したことにする。連鎖した置き物にも同じ帰属が渡る。</item>
    /// </list>
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ExplosiveProp : MonoBehaviour, IStructureHittable
    {
        public const string LogTag = ExplosivePropLog.Tag;

        /// <summary>爆発で倒したときのキルフィードの武器の Id。</summary>
        public const string WeaponId = "explosive";

        // StructureDamage は被害の出どころを渡さないので、置き物の爆発が被害を配っている最中かどうかで連鎖を見分ける。
        private static int _blastDepth;

        [SerializeField, Tooltip("ログの識別子。空なら GameObject の名前")]
        private string _id;

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

        /// <summary>耐久が 0 になり、爆発を待ち始めた (燃え始めた) ときに 1 度だけ呼ばれる。</summary>
        public event Action<ExplosiveProp, ExplosiveChargeStep> Triggered;

        /// <summary>爆発したときに、この置き物と結果を渡して 1 度だけ呼ばれる。呼んだ後に GameObject を消す。</summary>
        public event Action<ExplosiveProp, ExplosivePropExplosion> Exploded;

        /// <summary>ほかの置き物の爆発が被害を配っている最中なら true。この間に受けた被害は連鎖として扱う。</summary>
        public static bool IsBlastInProgress => _blastDepth > 0;

        /// <summary>ログの識別子。未設定なら GameObject の名前。</summary>
        public string Id
        {
            get => string.IsNullOrEmpty(_id) ? name : _id;
            set => _id = value;
        }

        /// <summary>今の値。Inspector の値が不正なら null。</summary>
        public ExplosivePropSpec Spec => TryEnsureCharge() ? _charge.Spec : null;

        /// <summary>残りの耐久。Inspector の値が不正なら 0。</summary>
        public int Health => TryEnsureCharge() ? _charge.Health : 0;

        /// <summary>耐久が 0 になり、爆発を待っているか爆発したなら true。</summary>
        public bool IsTriggered => TryEnsureCharge() && _charge.IsTriggered;

        /// <summary>燃えていて、まだ爆発していないなら true。</summary>
        public bool IsBurning => TryEnsureCharge() && _charge.IsBurning;

        /// <summary>ほかの置き物の爆発で耐久が 0 になったなら true。</summary>
        public bool IsChained => TryEnsureCharge() && _charge.IsChained;

        /// <summary>爆発までの残り秒数。爆発を待っていない間と爆発した後は 0。</summary>
        public float RemainingSeconds => TryEnsureCharge() ? _charge.RemainingSeconds : 0f;

        /// <summary>爆発したなら true。</summary>
        public bool HasExploded => _exploded;

        private void Awake()
        {
            TryEnsureCharge();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>
        /// 値を差し替え、耐久を満タンに戻す。ゲームのデータから値を上書きするときに使う。爆発した後は何もしない。
        /// </summary>
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

        /// <summary>
        /// 弾や爆発の被害を受ける (<see cref="IStructureHittable"/>)。ほかの置き物の爆発の最中 (<see cref="IsBlastInProgress"/>) なら連鎖として受ける。
        /// collider と point は使わない。
        /// </summary>
        public void ApplyStructureHit(Collider collider, Vector3 point, int damage)
        {
            ApplyDamage(damage, IsBlastInProgress);
        }

        /// <summary>
        /// 耐久を damage だけ減らし、0 になったら爆発を待ち始めて <see cref="Triggered"/> を呼ぶ。起きたことをログに出す。
        /// 爆発を待ち始めた後・爆発した後・Inspector の値が不正なときは何もしない。
        /// </summary>
        /// <param name="damage">被害。0 以上。</param>
        /// <param name="chained">ほかの置き物の爆発による被害なら true。</param>
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

        /// <summary>
        /// 爆発までの時間を deltaSeconds 進め、残りが無くなったら爆発する。爆発したら true。
        /// Play 中は <see cref="Update"/> から毎フレーム呼ぶ。Edit Mode のテストでは直接呼ぶ。
        /// </summary>
        /// <param name="deltaSeconds">前の呼び出しからの秒数。0 以上の有限値。</param>
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

        // Inspector の値が不正なら、撃った側を止めないよう 1 度だけエラーを出して以後の被害を無視する。
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
                _charge = new ExplosiveCharge(new ExplosivePropSpec(
                    _maxHealth,
                    _maxDamage,
                    _damageRadius,
                    _fullDamageRadius,
                    _structureDamageAtCenter,
                    _chainDelaySeconds,
                    _burnSeconds));
                return true;
            }
            catch (ArgumentException e)
            {
                _invalid = true;
                Debug.LogError($"{LogTag} ExplosiveProp '{name}' の設定が不正です: {e.Message}", this);
                return false;
            }
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

    /// <summary><see cref="ExplosiveProp"/> の爆発 1 回の結果。</summary>
    public readonly struct ExplosivePropExplosion
    {
        /// <summary>爆発の中心 (置き物のコライダーを合わせた範囲の中心)。</summary>
        public Vector3 Center { get; }

        /// <summary>ほかの置き物の爆発で耐久が 0 になって起きた爆発なら true。</summary>
        public bool Chained { get; }

        /// <summary>ダメージを与えた <see cref="Damageable"/> の数。</summary>
        public int Damaged { get; }

        /// <summary>この爆発で倒した <see cref="Damageable"/> の数。</summary>
        public int Killed { get; }

        /// <summary>被害を配った建造物の部品とほかの置き物の数 (<see cref="StructureDamage.ApplyAround"/> の戻り値)。</summary>
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
