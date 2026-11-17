using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ThrownDistraction : MonoBehaviour
    {
        public const string LogTag = "[Distraction]";
        public const string ObjectName = "Distraction";
        public const string VisualName = "Visual";

        // 空き缶や小石ほどの大きさ。跳ね方はグレネードと同じ材質を使う。
        public const float BodyRadius = 0.06f;
        public const float Mass = 0.2f;

        private GameObject _thrower;
        private DistractionSpec _spec;
        private AudioClip _landingSound;
        private float _elapsed;
        private float _lingerRemaining;
        private bool _armed;

        public event Action<ThrownDistraction> Landed;

        public static string LandedLogPrefix => LogTag + " landed ";

        public GameObject Thrower => _thrower;

        public DistractionSpec Spec => _spec;

        public bool HasLanded { get; private set; }

        public Vector3? LandedAt { get; private set; }

        public int Reacted { get; private set; }

        public static ThrownDistraction Spawn(
            GameObject thrower, Vector3 position, Vector3 velocity, DistractionSpec spec, GameObject visualPrefab, AudioClip landingSound)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

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

                collider.sharedMaterial = ThrownGrenade.SharedPhysicsMaterial();
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

                var distraction = go.AddComponent<ThrownDistraction>();
                distraction.Arm(thrower, spec, landingSound);
                return distraction;
            }
            catch
            {
                DestroySafely(go);
                throw;
            }
        }

        public void Arm(GameObject thrower, DistractionSpec spec, AudioClip landingSound)
        {
            _thrower = thrower;
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _landingSound = landingSound;
            _elapsed = 0f;
            _armed = true;
        }

        // 跳ねるたびに鳴らすと、1 投げで何度も呼び寄せたり、転がるたびに字幕が出続けたりするので、最初に当たったときだけ鳴らす。
        public bool Land(Vector3 point)
        {
            if (!_armed || HasLanded)
            {
                return false;
            }

            HasLanded = true;
            LandedAt = point;
            _lingerRemaining = _spec.LingerSeconds;
            Reacted = StealthNoise.Emit(_thrower, new SoundStimulus(SoundCueKind.Clatter, point.ToNumerics(), _spec.NoiseRadius, true));
            Debug.Log(FormatLandedLog(point, _spec.NoiseRadius, Reacted), this);
            PlayLandingSound(point);
            Landed?.Invoke(this);
            return true;
        }

        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (!_armed)
            {
                return false;
            }

            if (HasLanded)
            {
                _lingerRemaining -= deltaSeconds;
                if (_lingerRemaining > 0f)
                {
                    return false;
                }
            }
            else
            {
                _elapsed += deltaSeconds;
                if (_elapsed < _spec.MaxFlightSeconds)
                {
                    return false;
                }

                Debug.Log(FormatLostLog(transform.position), this);
            }

            _armed = false;
            DestroySafely(gameObject);
            return true;
        }

        public static string FormatThrownLog(Vector3 position, Vector3 velocity)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} thrown at=({1:0.0}, {2:0.0}, {3:0.0}) speed={4:0.0}",
                LogTag,
                position.x,
                position.y,
                position.z,
                velocity.magnitude);
        }

        public static string FormatLandedLog(Vector3 point, float radius, int reacted)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} landed at=({1:0.0}, {2:0.0}, {3:0.0}) radius={4:0.0} reacted={5}",
                LogTag,
                point.x,
                point.y,
                point.z,
                radius,
                reacted);
        }

        public static string FormatLostLog(Vector3 position)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} lost at=({1:0.0}, {2:0.0}, {3:0.0})",
                LogTag,
                position.x,
                position.y,
                position.z);
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnCollisionEnter(Collision collision)
        {
            Land(collision.contactCount > 0 ? collision.GetContact(0).point : transform.position);
        }

        // 字幕 (SubtitleFeed) は実際に鳴った音だけを出すので、素材が無ければ鳴らさず字幕も出さない。敵の耳には Land で届けてある。
        private void PlayLandingSound(Vector3 point)
        {
            if (_landingSound == null || !Application.isPlaying || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            AudioSource.PlayClipAtPoint(_landingSound, point);
            SubtitleFeed.Raise(SoundCueKind.Clatter, point, _spec.NoiseRadius);
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
}
