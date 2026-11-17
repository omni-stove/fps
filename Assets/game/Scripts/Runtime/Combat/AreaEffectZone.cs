using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed partial class AreaEffectZone : MonoBehaviour
    {
        public const string LogTag = "[AreaEffect]";

        public const string ObjectName = "AreaEffect";

        public const string VisualName = "Visual";

        public const string VisualResourceFolder = "Presentation/AreaEffects";

        private float _remaining;
        private bool _ended;

        public event Action<AreaEffectZone> Ended;

        public string KindId { get; private set; } = string.Empty;

        public Vector3 Center => transform.position;

        public float Radius { get; private set; }

        public float RemainingSeconds => _ended ? 0f : Mathf.Max(0f, _remaining);

        public bool HasEnded => _ended;

        public AreaEffectSpec Spec { get; private set; }

        public GameObject Owner { get; private set; }

        public string WeaponId { get; private set; } = string.Empty;

        public static AreaEffectZone Spread(AreaEffectSpec spec, Vector3 center, GameObject owner, string weaponId)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var zone = Create(spec.VisualId, spec, center, spec.Radius, spec.DurationSeconds);
            zone.Owner = owner;
            zone.WeaponId = weaponId ?? string.Empty;
            zone.StartSmoke();
            return zone;
        }

        public static AreaEffectZone Show(string kindId, Vector3 center, float radius, float remainingSeconds)
        {
            return Create(kindId, null, center, radius, remainingSeconds);
        }

        private static AreaEffectZone Create(string kindId, AreaEffectSpec spec, Vector3 center, float radius, float seconds)
        {
            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be a non-negative finite number");
            }

            var go = new GameObject(ObjectName);
            go.transform.position = center;
            var zone = go.AddComponent<AreaEffectZone>();
            zone.KindId = kindId ?? string.Empty;
            zone.Spec = spec;
            zone.Radius = radius;
            zone._remaining = seconds;
            zone.BuildVisual();
            Debug.Log(string.Format(
                CultureInfo.InvariantCulture,
                "{0} spread kind={1} at=({2:0.0}, {3:0.0}, {4:0.0}) radius={5:0.0} seconds={6:0.0}",
                LogTag,
                zone.KindId,
                center.x,
                center.y,
                center.z,
                radius,
                seconds));
            return zone;
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            StopSmoke();
        }

        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_ended)
            {
                return false;
            }

            if (Spec != null)
            {
                var step = Mathf.Min(deltaSeconds, _remaining);
                StepSmoke(step);
                StepFire(step);
            }

            _remaining -= deltaSeconds;
            if (_remaining > 0f)
            {
                return false;
            }

            End();
            return true;
        }

        public void SetRemaining(float seconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be a non-negative finite number");
            }

            if (seconds <= 0f)
            {
                End();
                return;
            }

            _remaining = seconds;
        }

        public void End()
        {
            if (_ended)
            {
                return;
            }

            _ended = true;
            StopSmoke();
            Debug.Log($"{LogTag} ended kind={KindId}");
            try
            {
                Ended?.Invoke(this);
            }
            finally
            {
                if (Application.isPlaying)
                {
                    Destroy(gameObject);
                }
                else
                {
                    DestroyImmediate(gameObject);
                }
            }
        }

        partial void StartSmoke();

        partial void StopSmoke();

        partial void BuildSmokeVisual(ref bool built);

        partial void StepSmoke(float deltaSeconds);

        partial void StepFire(float deltaSeconds);

        partial void BuildFireVisual(ref bool built);

        private void BuildVisual()
        {
            var prefab = KindId.Length > 0 ? Resources.Load<GameObject>(VisualResourceFolder + "/" + KindId) : null;
            if (prefab != null)
            {
                VisualPrefab.Instantiate(prefab, transform, VisualName, stripColliders: true);
                return;
            }

            var built = false;
            BuildSmokeVisual(ref built);
            if (!built)
            {
                BuildFireVisual(ref built);
            }

            if (built)
            {
                return;
            }

            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = VisualName;

            // 見た目だけの球が弾・視線・移動を遮らないよう、すぐ当たり判定を外す
            DestroyImmediate(sphere.GetComponent<Collider>());
            sphere.transform.SetParent(transform, false);
            sphere.transform.localScale = Vector3.one * (Radius * 2f);
        }
    }
}
