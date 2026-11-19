using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThrownMeleeWeapon : MonoBehaviour
    {
        public const string ObjectName = "ThrownMeleeWeapon";

        public const string VisualName = "Visual";

        public const string PlaceholderName = "Placeholder";

        // この速さ (m/s) 未満が SettleSeconds 続いたら止まったとみなす。転がり続ける間は拾えないように。
        public const float SettleSpeed = 0.3f;

        public const float SettleSeconds = 0.25f;

        // 仮の形は刃や柄に見えるよう、当たり判定の球の直径より長く細くする。
        public const float PlaceholderLengthPerRadius = 5f;

        public const float PlaceholderThicknessPerRadius = 0.4f;

        private GameObject _thrower;
        private string _throwerId = string.Empty;
        private HeldWeapon _held;
        private MeleeThrowSpec _spec;
        private GameObject _visualPrefab;
        private PickupCollectMode _collectMode;
        private Rigidbody _body;
        private float _flightSeconds;
        private float _stillSeconds;
        private bool _struck;
        private Pickup _pickup;

        public GameObject Thrower => _thrower;

        public string WeaponId => _held.Definition != null ? _held.Definition.Id : string.Empty;

        public HeldWeapon Weapon => _held;

        public MeleeThrowSpec Spec => _spec;

        public Rigidbody Body => _body;

        public bool HasStruck => _struck;

        public bool HasLanded => _pickup != null;

        public float FlightSeconds => _flightSeconds;

        public static ThrownMeleeWeapon Throw(
            GameObject thrower,
            HeldWeapon held,
            MeleeThrowSpec spec,
            Vector3 position,
            Vector3 direction,
            Vector3 spinAxis,
            GameObject visualPrefab = null,
            PickupCollectMode collectMode = PickupCollectMode.Touch)
        {
            if (held.Definition == null)
            {
                throw new ArgumentException("held has no weapon", nameof(held));
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (direction.sqrMagnitude < 1e-6f)
            {
                throw new ArgumentException("direction must not be zero", nameof(direction));
            }

            direction = direction.normalized;
            var go = new GameObject(ObjectName);
            try
            {
                go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
                var collider = go.AddComponent<SphereCollider>();
                collider.radius = spec.Radius;
                if (visualPrefab != null)
                {
                    VisualPrefab.Instantiate(visualPrefab, go.transform, VisualName, stripColliders: true);
                }
                else
                {
                    BuildPlaceholder(go.transform, spec.Radius);
                }

                if (thrower != null)
                {
                    foreach (var other in thrower.GetComponentsInChildren<Collider>())
                    {
                        Physics.IgnoreCollision(collider, other);
                    }
                }

                var body = go.AddComponent<Rigidbody>();
                body.interpolation = RigidbodyInterpolation.Interpolate;
                // 投げた直後は速く細いので、薄い壁をすり抜けないよう連続判定にする。
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.linearVelocity = direction * spec.Speed;
                if (spinAxis.sqrMagnitude > 1e-6f)
                {
                    body.angularVelocity = spinAxis.normalized * (spec.SpinDegreesPerSecond * Mathf.Deg2Rad);
                }

                var thrown = go.AddComponent<ThrownMeleeWeapon>();
                thrown._thrower = thrower;
                var damageable = thrower != null ? thrower.GetComponent<Damageable>() : null;
                thrown._throwerId = damageable != null ? damageable.Id : thrower != null ? thrower.name : string.Empty;
                thrown._held = held;
                thrown._spec = spec;
                thrown._visualPrefab = visualPrefab;
                thrown._collectMode = collectMode;
                thrown._body = body;
                return thrown;
            }
            catch
            {
                DestroySafely(go);
                throw;
            }
        }

        // 照準の右を軸に上へ起こす。右を軸にして負の角度で回すと上を向く (Unity は左手系)。
        public static Vector3 LaunchDirection(Vector3 aimForward, Vector3 aimRight, float upwardDegrees)
        {
            if (aimRight.sqrMagnitude < 1e-6f)
            {
                return aimForward.normalized;
            }

            return (Quaternion.AngleAxis(-upwardDegrees, aimRight.normalized) * aimForward).normalized;
        }

        private void FixedUpdate()
        {
            Advance(Time.fixedDeltaTime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            var point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            var normal = collision.contactCount > 0 ? collision.GetContact(0).normal : -transform.forward;
            Strike(collision.collider, point, normal);
        }

        // 最初に触れた物にだけ当たりを決める。跳ね返った先でもう一度当たると、1 回の投げで何人も倒せてしまう。
        public bool Strike(Collider collider, Vector3 point, Vector3 normal)
        {
            if (_struck || HasLanded || collider == null)
            {
                return false;
            }

            if (_thrower != null && collider.transform.IsChildOf(_thrower.transform))
            {
                return false;
            }

            _struck = true;
            var target = MeleeStrike.FindDamageable(collider);
            if (target == null || target.IsDead)
            {
                CombatEvents.RaiseImpact(point, normal, collider);
                Debug.Log(MeleeLog.ThrowWorldHit(_throwerId, WeaponId, SurfaceMaterial.IdOf(collider)), this);
                return true;
            }

            var velocity = _body != null ? _body.linearVelocity : transform.forward;
            var direction = velocity.sqrMagnitude > 1e-6f ? velocity.normalized : transform.forward;
            Vector3? source = _thrower != null ? _thrower.transform.position : (Vector3?)null;
            Debug.Log(MeleeLog.ThrowHit(_throwerId, WeaponId, target.Id, _spec.Damage), this);
            using (CombatEvents.Attribute(_thrower, WeaponId))
            {
                target.TakeDamage(_spec.Damage, source, direction, DamageKind.Melee);
            }

            // オフラインで投げられるのは手元のプレイヤーだけなので、その画面にヒットマーカーを出す。
            var self = _thrower != null ? _thrower.GetComponent<Damageable>() : null;
            if (self != null && self.IsLocalPlayer)
            {
                var hitbox = Hitbox.FindFor(collider);
                var zone = hitbox != null ? hitbox.Zone : HitZone.Body;
                CombatEvents.RaiseHitConfirmed(new HitConfirm(_spec.Damage, zone, target.IsDead, downed: false, point.ToNumerics()));
            }

            // 刺さったまま相手と一緒に動かす代わりに、勢いを止めて足元へ落とし、倒した所で拾えるようにする。
            if (_body != null && !_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            return true;
        }

        public bool Advance(float deltaSeconds)
        {
            if (HasLanded)
            {
                return false;
            }

            _flightSeconds += Mathf.Max(0f, deltaSeconds);
            var speed = _body != null ? _body.linearVelocity.magnitude : 0f;
            _stillSeconds = speed < SettleSpeed ? _stillSeconds + Mathf.Max(0f, deltaSeconds) : 0f;
            if (_stillSeconds < SettleSeconds && _flightSeconds < _spec.MaxFlightSeconds)
            {
                return false;
            }

            Land();
            return true;
        }

        public Pickup Land()
        {
            if (HasLanded)
            {
                return _pickup;
            }

            var contents = PickupContents.ForDroppedWeapon(_held.Definition, _held.Ammo, attachments: _held.Attachments);
            _pickup = Pickup.Create(contents, transform.position, _collectMode, visualPrefab: _visualPrefab);
            Debug.Log(MeleeLog.ThrowLanded(_throwerId, WeaponId), _pickup);
            DestroySafely(gameObject);
            return _pickup;
        }

        private static void BuildPlaceholder(Transform parent, float radius)
        {
            var placeholder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placeholder.name = PlaceholderName;
            // Destroy はフレームの終わりまで待ち、その間は投げた者と当たる箱が体に残るので、すぐに外す。
            DestroyImmediate(placeholder.GetComponent<Collider>());

            placeholder.transform.SetParent(parent, false);
            var thickness = radius * PlaceholderThicknessPerRadius;
            placeholder.transform.localScale = new Vector3(thickness, thickness, radius * PlaceholderLengthPerRadius);
        }

        private static void DestroySafely(GameObject go)
        {
            if (Application.isPlaying)
            {
                Destroy(go);
            }
            else
            {
                DestroyImmediate(go);
            }
        }
    }
}
