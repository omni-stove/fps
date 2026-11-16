using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 投げられたグレネード 1 個。<see cref="Spawn"/> で Rigidbody の球として作られて跳ね、信管の秒数が経つと <see cref="Explode"/> して、
    /// 種類の <see cref="ImpactSpec"/> の爆発と範囲効果 (<see cref="AreaEffectZone"/>)、目くらまし (<see cref="Flash"/>) を起こす。くっつき方は <c>ThrownGrenade.Sticky.cs</c> が受け持つ。
    /// 爆発は半径内の <see cref="Damageable"/> に <see cref="ExplosionDamage"/> を与え (中心から体の狙う点・頭の高さ・足元までのどれかの線が
    /// <see cref="Damageable"/> を持たない物に遮られていなければ)、<see cref="StructureDamage.ApplyAround"/> で建造物にも範囲で当てる。
    /// 演出は <see cref="PresentationCatalog"/> の爆発 (エフェクトと音) を出し、カタログか素材が無ければ出さない。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed partial class ThrownGrenade : MonoBehaviour
    {
        public const string ObjectName = "Grenade";

        /// <summary>差し替えた見た目のプレハブを置く子の名前。</summary>
        public const string VisualName = "Visual";

        /// <summary>グレネードの球の半径 (m)。</summary>
        public const float BodyRadius = 0.08f;

        /// <summary>グレネードの重さ (kg)。</summary>
        public const float Mass = 0.4f;

        /// <summary>跳ね返りの強さ。</summary>
        public const float Bounciness = 0.35f;

        /// <summary>爆発のエフェクトを消すまでの秒数。プレハブが自分で消えなかったときの後始末。</summary>
        public const float EffectLifetimeSeconds = Fps.Runtime.Explosion.EffectLifetimeSeconds;

        /// <summary>
        /// 爆発の音が聞こえなくなる距離 (m)。<see cref="AudioSource.PlayClipAtPoint(AudioClip, Vector3)"/> が作る音源の既定の maxDistance に合わせ、
        /// 字幕 (<see cref="SubtitleFeed"/>) をこれより遠い人には出さない。
        /// </summary>
        public const float ExplosionAudibleDistance = Fps.Runtime.Explosion.AudibleDistance;

        // 上を向いた面とみなす接触の法線の y。これより急な壁に触れても転がりは減らさない。
        private const float GroundNormalMinY = 0.5f;

        private static PhysicsMaterial _material;
        private static readonly List<ThrownGrenade> _live = new List<ThrownGrenade>();

        private readonly List<Collider> _ownColliders = new List<Collider>();
        private GameObject _thrower;
        private ImpactSpec _impact;
        private GrenadeStickiness _stickiness;
        private string _typeId = string.Empty;
        private float _remaining;
        private float _rollDamping;
        private bool _grounded;
        private Rigidbody _body;
        private bool _armed;
        private bool _exploded;

        /// <summary>投げた者の本体。倒した記録 (キルログ) とヒットの確認に使う。投げた者が消えていれば null。</summary>
        public GameObject Thrower => _thrower;

        /// <summary>爆発までの残り秒数。爆発した後は 0。</summary>
        public float RemainingSeconds => _exploded ? 0f : Mathf.Max(0f, _remaining);

        /// <summary>爆発したか。</summary>
        public bool HasExploded => _exploded;

        /// <summary>この爆発が与えるダメージ。<see cref="Arm"/> の前と、爆発しない種類では null。</summary>
        public ExplosionDamage Explosion => _impact?.Explosion?.Damage;

        /// <summary>信管が切れたときの効果。<see cref="Arm"/> の前は null。</summary>
        public ImpactSpec Impact => _impact;

        public GrenadeStickiness Stickiness => _stickiness;

        /// <summary>投げた種類の Id (<see cref="GrenadeSpec.Id"/>)。種類を渡さずに作ったときは空文字。</summary>
        public string TypeId => _typeId;

        /// <summary>地面に触れている間の転がりの減り方 (1/s、<see cref="GrenadeSpec.RollDamping"/>)。</summary>
        public float RollDamping => _rollDamping;

        /// <summary>今シーンにある、まだ爆発していないグレネード。警告や範囲の表示が毎フレーム読む。</summary>
        public static IReadOnlyList<ThrownGrenade> Live => _live;

        /// <summary>爆発したときに、このグレネードと結果を渡して 1 度だけ呼ばれる。</summary>
        public event Action<ThrownGrenade, GrenadeExplosion> Exploded;

        /// <summary>
        /// position に球のグレネードを作り、velocity で飛ばして <see cref="Arm"/> する。thrower の体のコライダーとはぶつからない。
        /// </summary>
        /// <param name="thrower">投げた者の本体。null 可 (帰属の無い爆発になる)。</param>
        /// <param name="position">出す位置。</param>
        /// <param name="velocity">初速 (m/s)。</param>
        /// <param name="fuseSeconds">爆発までの秒数。0 以上の有限値。</param>
        /// <param name="explosion">爆発のダメージ。</param>
        /// <param name="structureDamage">中心で建造物に与える被害。0 以上。</param>
        public static ThrownGrenade Spawn(
            GameObject thrower,
            Vector3 position,
            Vector3 velocity,
            float fuseSeconds,
            ExplosionDamage explosion,
            int structureDamage)
        {
            return Spawn(thrower, position, velocity, fuseSeconds, explosion, structureDamage, null);
        }

        /// <summary>
        /// <see cref="Spawn(GameObject, Vector3, Vector3, float, ExplosionDamage, int)"/> と同じく投げ、見た目を visualPrefab にする。
        /// 跳ね方と当たり判定は仮の球と同じ半径 <see cref="BodyRadius"/> の球のままにし、プレハブは当たり判定を外して子に置く。
        /// </summary>
        /// <param name="visualPrefab">グレネードの見た目。中心が原点。null なら仮の球を作る。</param>
        public static ThrownGrenade Spawn(
            GameObject thrower,
            Vector3 position,
            Vector3 velocity,
            float fuseSeconds,
            ExplosionDamage explosion,
            int structureDamage,
            GameObject visualPrefab)
        {
            return Create(thrower, position, velocity, visualPrefab, grenade => grenade.Arm(thrower, fuseSeconds, explosion, structureDamage));
        }

        /// <summary>spec の種類のグレネードを投げる。信管・効果・くっつき方は spec に従う。</summary>
        /// <param name="visualPrefab">グレネードの見た目。中心が原点。null なら仮の球を作る。</param>
        public static ThrownGrenade Spawn(GameObject thrower, Vector3 position, Vector3 velocity, GrenadeSpec spec, GameObject visualPrefab)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return Create(thrower, position, velocity, visualPrefab, grenade => grenade.Arm(thrower, spec));
        }

        private static ThrownGrenade Create(GameObject thrower, Vector3 position, Vector3 velocity, GameObject visualPrefab, Action<ThrownGrenade> arm)
        {
            var go = visualPrefab != null ? new GameObject(ObjectName) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            try
            {
                go.name = ObjectName;
                go.transform.SetPositionAndRotation(position, Quaternion.identity);
                SphereCollider collider;
                if (visualPrefab != null)
                {
                    // 根を拡大するとプレハブの大きさまで変わるので、根は等倍のまま球の半径で合わせる。
                    collider = go.AddComponent<SphereCollider>();
                    collider.radius = BodyRadius;
                    VisualPrefab.Instantiate(visualPrefab, go.transform, VisualName, stripColliders: true);
                }
                else
                {
                    go.transform.localScale = Vector3.one * (BodyRadius * 2f);
                    collider = go.GetComponent<SphereCollider>();
                }

                collider.sharedMaterial = SharedPhysicsMaterial();
                if (thrower != null)
                {
                    foreach (var other in thrower.GetComponentsInChildren<Collider>())
                    {
                        Physics.IgnoreCollision(collider, other);
                    }
                }

                var body = go.AddComponent<Rigidbody>();
                body.mass = Mass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                // 投げた直後は速く小さいので、薄い壁をすり抜けないよう連続判定にする。
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = velocity;

                var grenade = go.AddComponent<ThrownGrenade>();
                arm(grenade);
                return grenade;
            }
            catch
            {
                DestroySafely(go);
                throw;
            }
        }

        /// <summary>信管と爆発の値を決める。<see cref="Spawn"/> が呼ぶ。2 回目以降は値を差し替える (爆発した後は何もしない)。</summary>
        public void Arm(GameObject thrower, float fuseSeconds, ExplosionDamage explosion, int structureDamage)
        {
            if (explosion == null)
            {
                throw new ArgumentNullException(nameof(explosion));
            }

            if (structureDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(structureDamage), structureDamage, "structureDamage must not be negative");
            }

            var impact = new ImpactSpec(damage: 0, explosion: new ExplosionSpec(explosion, structureDamage, DetonationTrigger.OnLifetimeEnd));
            Arm(thrower, fuseSeconds, impact, GrenadeStickiness.None, string.Empty, GrenadeSpec.DefaultRollDamping);
        }

        /// <summary>spec の信管・効果・くっつき方にする。2 回目以降は値を差し替える (爆発した後は何もしない)。</summary>
        public void Arm(GameObject thrower, GrenadeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            Arm(thrower, spec.FuseSeconds, spec.Impact, spec.Stickiness, spec.Id, spec.RollDamping);
        }

        private void Arm(GameObject thrower, float fuseSeconds, ImpactSpec impact, GrenadeStickiness stickiness, string typeId, float rollDamping)
        {
            if (!(fuseSeconds >= 0f) || float.IsInfinity(fuseSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(fuseSeconds), fuseSeconds, "fuseSeconds must be a non-negative finite number");
            }

            if (_exploded)
            {
                return;
            }

            _thrower = thrower;
            _impact = impact;
            _stickiness = stickiness;
            _typeId = typeId;
            _rollDamping = rollDamping;
            _remaining = fuseSeconds;
            _armed = true;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_armed && !_exploded && _stickiness != GrenadeStickiness.None)
            {
                Stick(collision);
            }
        }

        partial void Stick(Collision collision);

        private void OnEnable()
        {
            _live.Add(this);
        }

        private void OnDisable()
        {
            _live.Remove(this);
        }

        // 球の当たり判定は転がりの抵抗を持たず、摩擦だけでは平らな床をいつまでも転がり続けるので、床に触れている間だけ速さと回転を減らす。
        private void OnCollisionStay(Collision collision)
        {
            for (var i = 0; i < collision.contactCount; i++)
            {
                if (collision.GetContact(i).normal.y >= GroundNormalMinY)
                {
                    _grounded = true;
                    return;
                }
            }
        }

        private void FixedUpdate()
        {
            var grounded = _grounded;
            _grounded = false;
            if (!grounded || _rollDamping <= 0f || _exploded)
            {
                return;
            }

            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }

            if (_body == null || _body.isKinematic)
            {
                return;
            }

            DampRoll(_body, _rollDamping, Time.fixedDeltaTime);
        }

        /// <summary>body の水平の速さと回転を e^(-rollDamping * deltaSeconds) 倍にする。縦の速さ (跳ねと落下) は変えない。</summary>
        public static void DampRoll(Rigidbody body, float rollDamping, float deltaSeconds)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            var factor = Mathf.Exp(-Mathf.Max(0f, rollDamping) * Mathf.Max(0f, deltaSeconds));
            var velocity = body.linearVelocity;
            body.linearVelocity = new Vector3(velocity.x * factor, velocity.y, velocity.z * factor);
            body.angularVelocity *= factor;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        /// <summary>信管を deltaSeconds 進め、残りが 0 になったら <see cref="Explode"/> する。爆発したら true。</summary>
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!_armed || _exploded)
            {
                return false;
            }

            _remaining -= deltaSeconds;
            if (_remaining > 0f)
            {
                return false;
            }

            Explode();
            return true;
        }

        /// <summary>
        /// 今の位置で効果を起こし、グレネードを消す。爆発する種類なら半径内の生きている <see cref="Damageable"/> に遮られていなければダメージを与え
        /// (投げた者の帰属で、投げた本人も巻き込む)、建造物に範囲で被害を与え、演出を出す。範囲効果を持つ種類なら <see cref="AreaEffectZone"/> を置く。
        /// 2 回目以降と <see cref="Arm"/> の前は何もせず default を返す。
        /// </summary>
        public GrenadeExplosion Explode()
        {
            if (!_armed || _exploded)
            {
                return default;
            }

            _exploded = true;
            var center = transform.position;

            // 自分の球が中心から引く線を遮らないよう、先に当たり判定から外す。
            GetComponentsInChildren(_ownColliders);
            foreach (var own in _ownColliders)
            {
                own.enabled = false;
            }

            var result = _impact.Explosion != null
                ? Fps.Runtime.Explosion.Detonate(center, _impact.Explosion, _thrower, GrenadeThrower.WeaponId)
                : new GrenadeExplosion(center, 0, 0, 0);
            if (_impact.AreaEffect != null)
            {
                AreaEffectZone.Spread(_impact.AreaEffect, center, _thrower, GrenadeThrower.WeaponId);
            }

            if (_impact.Blind != null)
            {
                Flash.Detonate(center, _impact.Blind, _thrower, GrenadeThrower.WeaponId);
            }

            try
            {
                Exploded?.Invoke(this, result);
            }
            finally
            {
                DestroySafely(gameObject);
            }

            return result;
        }

        /// <summary>爆発のログ。例: <c>[Grenade] exploded at=(1.0, 0.1, 9.5) damaged=2 killed=1 structures=0</c>。</summary>
        public static string FormatLog(GrenadeExplosion explosion)
        {
            return Fps.Runtime.Explosion.FormatLog(explosion);
        }

        /// <summary>
        /// center から target までの線が遮られているか。target 自身と、<see cref="Damageable"/> を持つ物 (人や標的) とトリガーは遮らない。
        /// 置いた物 (盾など <see cref="Deployable"/>) は <see cref="Damageable"/> を持っていても遮る。
        /// target の狙う点と、体の上端の少し下と、足元の少し上の 3 本を引き、すべて遮られていれば true。
        /// </summary>
        public static bool IsObstructed(Vector3 center, Damageable target)
        {
            return Fps.Runtime.Explosion.IsObstructed(center, target);
        }

        /// <summary>center から target の体 (コライダーを合わせた範囲) のいちばん近い点までの距離。コライダーが無ければ Transform の位置まで。</summary>
        public static float DistanceTo(Vector3 center, Damageable target)
        {
            return Fps.Runtime.Explosion.DistanceTo(center, target);
        }

        /// <summary>
        /// center に <see cref="PresentationCatalog"/> の爆発のエフェクトと音を出し、音を鳴らしたら字幕のために <see cref="SubtitleFeed.Raise"/> で知らせる。
        /// Play 中でない・専用サーバー・カタログか素材が無いときは何もしない。
        /// ネット対戦ではサーバーから爆発を受け取ったクライアント (<c>Fps.Net.NetCombatFeed</c>) も呼ぶ。
        /// </summary>
        public static void PlayExplosionEffects(Vector3 center)
        {
            Fps.Runtime.Explosion.PlayEffects(center);
        }

        /// <summary>グレネードの球の跳ね方 (<see cref="Bounciness"/>)。ネット対戦のグレネード (<c>Fps.Net.NetGrenade</c>) も同じものを使う。</summary>
        public static PhysicsMaterial SharedPhysicsMaterial()
        {
            if (_material == null)
            {
                _material = new PhysicsMaterial(ObjectName)
                {
                    bounciness = Bounciness,
                    bounceCombine = PhysicsMaterialCombine.Maximum,
                    dynamicFriction = 0.6f,
                    staticFriction = 0.6f,
                    hideFlags = HideFlags.HideAndDontSave,
                };
            }

            return _material;
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

    /// <summary><see cref="ThrownGrenade.Explode"/> の結果。</summary>
    public readonly struct GrenadeExplosion
    {
        /// <summary>爆発の中心。</summary>
        public Vector3 Center { get; }

        /// <summary>ダメージを与えた <see cref="Damageable"/> の数。</summary>
        public int Damaged { get; }

        /// <summary>この爆発で倒した <see cref="Damageable"/> の数。</summary>
        public int Killed { get; }

        /// <summary>被害を与えた建造物の部品の数 (<see cref="StructureDamage.ApplyAround"/> の戻り値)。</summary>
        public int StructureHits { get; }

        public GrenadeExplosion(Vector3 center, int damaged, int killed, int structureHits)
        {
            Center = center;
            Damaged = damaged;
            Killed = killed;
            StructureHits = structureHits;
        }
    }
}
