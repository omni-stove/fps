using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // AwarenessIndicatorCatalog に印のプレハブが無いときだけ使う仮の印。形と色はコードで決め、ゲームはカタログで差し替える。
    [DisallowMultipleComponent]
    public sealed class AwarenessMarker : MonoBehaviour, IAwarenessIndicator
    {
        public const string ObjectNamePrefix = "AwarenessMarker-";
        public const float Size = 0.3f;

        public static readonly Color NoticedColor = new Color(1f, 0.9f, 0.2f);
        public static readonly Color AlertedColor = new Color(1f, 0.55f, 0.1f);
        public static readonly Color EngagedColor = new Color(0.9f, 0.1f, 0.1f);

        private Renderer _renderer;
        private Material _material;

        public bool IsShown => _renderer != null && _renderer.enabled;

        public Color ShownColor => _material != null ? _material.color : Color.clear;

        public static AwarenessMarker Create(string ownerName)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = ObjectNamePrefix + ownerName;

            // 弾と移動を遮らないように当たり判定を外す。
            DestroySafely(go.GetComponent<Collider>());

            var marker = go.AddComponent<AwarenessMarker>();
            marker._renderer = go.GetComponent<Renderer>();
            marker._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            marker._renderer.receiveShadows = false;

            // 共有のマテリアルを塗り替えると他のプリミティブまで色が変わるので、印ごとに複製する。
            marker._material = new Material(marker._renderer.sharedMaterial) { name = go.name };
            marker._renderer.sharedMaterial = marker._material;
            marker.Show(AwarenessStage.Unaware, 0f);
            return marker;
        }

        public void Show(AwarenessStage stage, float level)
        {
            if (_renderer == null)
            {
                return;
            }

            _renderer.enabled = stage != AwarenessStage.Unaware;
            if (!_renderer.enabled)
            {
                return;
            }

            _material.color = ColorOf(stage);

            // 交戦の手前ではゲージの量で大きくし、どこまでたまったかを見えるようにする。
            var scale = stage == AwarenessStage.Engaged ? 1f : 0.5f + (0.5f * Mathf.Clamp01(level));
            transform.localScale = Vector3.one * (Size * scale);
        }

        public static Color ColorOf(AwarenessStage stage)
        {
            switch (stage)
            {
                case AwarenessStage.Noticed:
                    return NoticedColor;
                case AwarenessStage.Alerted:
                    return AlertedColor;
                case AwarenessStage.Engaged:
                    return EngagedColor;
                default:
                    return Color.clear;
            }
        }

        // Edit Mode では OnDestroy が呼ばれないので、複製したマテリアルもここで消す。
        public void Remove()
        {
            if (_material != null)
            {
                DestroySafely(_material);
                _material = null;
            }

            DestroySafely(gameObject);
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                DestroySafely(_material);
                _material = null;
            }
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
    }
}
