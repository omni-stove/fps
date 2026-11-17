using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed partial class ThrownGrenade : MonoBehaviour
    {
        public const string ObjectName = "Grenade";

        public const string VisualName = "Visual";

        public const float EffectLifetimeSeconds = Shooter.Runtime.Explosion.EffectLifetimeSeconds;

        public const float ExplosionAudibleDistance = Shooter.Runtime.Explosion.AudibleDistance;

        // 上を向いた面とみなす接触の法線の y。これより急な壁に触れても転がりは減らさない。
        private const float GroundNormalMinY = 0.5f;

        // 投げるたびに作ると増え続けるので、跳ね返りと摩擦の組ごとに 1 つを使い回す。
        private static readonly Dictionary<(float Bounciness, float Friction), PhysicsMaterial> _materials =
            new Dictionary<(float Bounciness, float Friction), PhysicsMaterial>();
        private static readonly List<ThrownGrenade> _live = new List<ThrownGrenade>();

        private readonly List<Collider> _ownColliders = new List<Collider>();
        private GameObject _thrower;
        private ImpactSpec _impact;
        private GrenadeStickiness _stickiness;
        private string _typeId = string.Empty;
        private float _remaining;
        private float _rollDamping;
        private float _bodyRadius;
        private bool _grounded;
        private Rigidbody _body;
        private bool _armed;
        private bool _exploded;

        public GameObject Thrower => _thrower;

        public float RemainingSeconds => _exploded ? 0f : Mathf.Max(0f, _remaining);

        public bool HasExploded => _exploded;

        public ExplosionDamage Explosion => _impact?.Explosion?.Damage;

        public ImpactSpec Impact => _impact;

        public GrenadeStickiness Stickiness => _stickiness;

        public string TypeId => _typeId;

        public float RollDamping => _rollDamping;

        public float BodyRadius => _bodyRadius;

        public static GrenadeSpec UntypedSpec => KitTuning.Grenades.Types[0];

        public static IReadOnlyList<ThrownGrenade> Live => _live;

        public event Action<ThrownGrenade, GrenadeExplosion> Exploded;

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

        public static ThrownGrenade Spawn(
            GameObject thrower,
            Vector3 position,
            Vector3 velocity,
            float fuseSeconds,
            ExplosionDamage explosion,
            int structureDamage,
            GameObject visualPrefab)
        {
            return Create(thrower, position, velocity, UntypedSpec, visualPrefab, grenade => grenade.Arm(thrower, fuseSeconds, explosion, structureDamage));
        }

        public static ThrownGrenade Spawn(GameObject thrower, Vector3 position, Vector3 velocity, GrenadeSpec spec, GameObject visualPrefab)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return Create(thrower, position, velocity, spec, visualPrefab, grenade => grenade.Arm(thrower, spec));
        }

        private static ThrownGrenade Create(
            GameObject thrower, Vector3 position, Vector3 velocity, GrenadeSpec body, GameObject visualPrefab, Action<ThrownGrenade> arm)
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
                    collider.radius = body.BodyRadius;
                    VisualPrefab.Instantiate(visualPrefab, go.transform, VisualName, stripColliders: true);
                }
                else
                {
                    go.transform.localScale = Vector3.one * (body.BodyRadius * 2f);
                    collider = go.GetComponent<SphereCollider>();
                }

                collider.sharedMaterial = PhysicsMaterialFor(body);
                if (thrower != null)
                {
                    foreach (var other in thrower.GetComponentsInChildren<Collider>())
                    {
                        Physics.IgnoreCollision(collider, other);
                    }
                }

                var physicsBody = go.AddComponent<Rigidbody>();
                physicsBody.mass = body.Mass;
                physicsBody.angularDamping = body.AngularDamping;
                physicsBody.interpolation = RigidbodyInterpolation.Interpolate;
                // 投げた直後は速く小さいので、薄い壁をすり抜けないよう連続判定にする。
                physicsBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                physicsBody.linearVelocity = velocity;

                var grenade = go.AddComponent<ThrownGrenade>();
                grenade._bodyRadius = body.BodyRadius;
                arm(grenade);
                return grenade;
            }
            catch
            {
                DestroySafely(go);
                throw;
            }
        }

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
            Arm(thrower, fuseSeconds, impact, GrenadeStickiness.None, string.Empty, UntypedSpec.RollDamping);
        }

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
                ? Shooter.Runtime.Explosion.Detonate(center, _impact.Explosion, _thrower, GrenadeThrower.WeaponId)
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

        public static string FormatLog(GrenadeExplosion explosion)
        {
            return Shooter.Runtime.Explosion.FormatLog(explosion);
        }

        public static bool IsObstructed(Vector3 center, Damageable target)
        {
            return Shooter.Runtime.Explosion.IsObstructed(center, target);
        }

        public static float DistanceTo(Vector3 center, Damageable target)
        {
            return Shooter.Runtime.Explosion.DistanceTo(center, target);
        }

        public static void PlayExplosionEffects(Vector3 center)
        {
            Shooter.Runtime.Explosion.PlayEffects(center);
        }

        public static PhysicsMaterial SharedPhysicsMaterial()
        {
            return PhysicsMaterialFor(UntypedSpec);
        }

        public static PhysicsMaterial PhysicsMaterialFor(GrenadeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var key = (spec.Bounciness, spec.Friction);
            if (!_materials.TryGetValue(key, out var material) || material == null)
            {
                material = new PhysicsMaterial(ObjectName)
                {
                    bounciness = spec.Bounciness,
                    bounceCombine = PhysicsMaterialCombine.Maximum,
                    dynamicFriction = spec.Friction,
                    staticFriction = spec.Friction,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _materials[key] = material;
            }

            return material;
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

    public readonly struct GrenadeExplosion
    {
        public Vector3 Center { get; }

        public int Damaged { get; }

        public int Killed { get; }

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
