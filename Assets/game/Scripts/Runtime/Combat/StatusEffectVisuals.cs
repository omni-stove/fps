using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class StatusEffectVisuals : MonoBehaviour
    {
        public const string ObjectNamePrefix = "StatusEffect_";

        // 体の大きさが分からないときは、人型のカプセルに合わせる。
        private const float _fallbackCenterHeight = 1f;
        private const float _fallbackBodyHeight = 1.8f;

        private const int _flameCount = 5;
        private const float _flickerSpeed = 9f;

        private static readonly Color _flameColor = new Color(1f, 0.45f, 0.05f, 1f);
        private static Material _flameMaterial;

        private readonly Dictionary<string, Shown> _shown = new Dictionary<string, Shown>();

        public static StatusEffectVisuals For(GameObject host)
        {
            if (host == null)
            {
                throw new System.ArgumentNullException(nameof(host));
            }

            var visuals = host.GetComponent<StatusEffectVisuals>();
            return visuals != null ? visuals : host.AddComponent<StatusEffectVisuals>();
        }

        public bool IsShowing(string kindId)
        {
            return kindId != null && _shown.ContainsKey(kindId);
        }

        public void Show(string kindId)
        {
            if (string.IsNullOrEmpty(kindId) || _shown.ContainsKey(kindId) || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            var (center, height) = Measure();
            var catalog = StatusEffectVisualCatalog.Load();
            var prefab = catalog != null ? catalog.VisualFor(kindId) : null;
            GameObject instance;
            List<Transform> flames = null;
            if (prefab != null)
            {
                instance = Instantiate(prefab, transform);
            }
            else if (kindId == StatusEffectKinds.Burn)
            {
                flames = new List<Transform>();
                instance = BuildFlames(height, flames);
                instance.transform.SetParent(transform, false);
            }
            else
            {
                return;
            }

            instance.name = ObjectNamePrefix + kindId;
            instance.transform.position = center;
            _shown.Add(kindId, new Shown(instance, flames));
        }

        public void Hide(string kindId)
        {
            if (kindId == null || !_shown.TryGetValue(kindId, out var shown))
            {
                return;
            }

            _shown.Remove(kindId);
            DestroySafely(shown.Instance);
        }

        public void HideAll()
        {
            foreach (var shown in _shown.Values)
            {
                DestroySafely(shown.Instance);
            }

            _shown.Clear();
        }

        private void Update()
        {
            foreach (var shown in _shown.Values)
            {
                if (shown.Flames == null)
                {
                    continue;
                }

                for (var i = 0; i < shown.Flames.Count; i++)
                {
                    var flame = shown.Flames[i];
                    if (flame == null)
                    {
                        continue;
                    }

                    var noise = Mathf.PerlinNoise(Time.time * _flickerSpeed, i * 3.7f);
                    flame.localScale = shown.FlameScales[i] * (0.6f + (0.8f * noise));
                }
            }
        }

        private void OnDestroy()
        {
            HideAll();
        }

        private (Vector3 Center, float Height) Measure()
        {
            var found = false;
            var bounds = default(Bounds);
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (IsOwnVisual(renderer.transform))
                {
                    continue;
                }

                if (!found)
                {
                    bounds = renderer.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return found && bounds.size.y > 0.01f
                ? (bounds.center, bounds.size.y)
                : (transform.position + (Vector3.up * _fallbackCenterHeight), _fallbackBodyHeight);
        }

        private bool IsOwnVisual(Transform candidate)
        {
            foreach (var shown in _shown.Values)
            {
                if (shown.Instance != null && candidate.IsChildOf(shown.Instance.transform))
                {
                    return true;
                }
            }

            return false;
        }

        private static GameObject BuildFlames(float bodyHeight, List<Transform> flames)
        {
            var root = new GameObject();
            var size = Mathf.Clamp(bodyHeight * 0.25f, 0.15f, 1f);
            for (var i = 0; i < _flameCount; i++)
            {
                var flame = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flame.name = "Flame" + i;
                // 次のフレームまで残ると弾がこの球に当たり、体の当たり判定より先に止まるので、すぐ消す。
                DestroyImmediate(flame.GetComponent<Collider>());
                var renderer = flame.GetComponent<Renderer>();
                renderer.sharedMaterial = FlameMaterial(renderer);
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                flame.transform.SetParent(root.transform, false);

                var t = (i + 0.5f) / _flameCount;
                var angle = i * 2.4f;
                flame.transform.localPosition = new Vector3(Mathf.Cos(angle) * size * 0.8f, (t - 0.5f) * bodyHeight * 0.8f, Mathf.Sin(angle) * size * 0.8f);
                flame.transform.localScale = Vector3.one * size * (1.2f - (0.5f * t));
                flames.Add(flame.transform);
            }

            return root;
        }

        private static Material FlameMaterial(Renderer template)
        {
            if (_flameMaterial == null)
            {
                _flameMaterial = new Material(template.sharedMaterial) { name = ObjectNamePrefix + StatusEffectKinds.Burn, color = _flameColor };
                _flameMaterial.EnableKeyword("_EMISSION");
                _flameMaterial.SetColor("_EmissionColor", _flameColor * 2f);
            }

            return _flameMaterial;
        }

        private static void DestroySafely(Object target)
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

        private sealed class Shown
        {
            public Shown(GameObject instance, List<Transform> flames)
            {
                Instance = instance;
                Flames = flames;
                if (flames != null)
                {
                    FlameScales = new Vector3[flames.Count];
                    for (var i = 0; i < flames.Count; i++)
                    {
                        FlameScales[i] = flames[i].localScale;
                    }
                }
            }

            public GameObject Instance { get; }

            public List<Transform> Flames { get; }

            public Vector3[] FlameScales { get; }
        }
    }
}
