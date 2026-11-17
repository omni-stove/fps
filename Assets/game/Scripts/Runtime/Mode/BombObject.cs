using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 弾と移動を遮らないよう、当たり判定は持たせない。
    [DisallowMultipleComponent]
    public sealed class BombObject : MonoBehaviour
    {
        public const float CarryHeight = 1.1f;

        public const float CarryBack = 0.35f;

        public static readonly Color CarriedColor = new Color(0.9f, 0.55f, 0.1f);
        public static readonly Color DroppedColor = new Color(1f, 0.85f, 0.2f);
        public static readonly Color PlantedColor = new Color(1f, 0.1f, 0.1f);
        public static readonly Color DefusedColor = new Color(0.2f, 0.8f, 0.3f);

        private Transform _carrier;
        private Renderer _renderer;
        private Material _material;

        public Transform Carrier => _carrier;

        public bool IsVisible => Renderer != null && Renderer.enabled;

        private Renderer Renderer
        {
            get
            {
                if (_renderer == null)
                {
                    _renderer = GetComponent<Renderer>();
                }

                return _renderer;
            }
        }

        public void ShowCarried(Transform carrier)
        {
            _carrier = carrier;
            if (carrier == null)
            {
                Hide();
                return;
            }

            Follow();
            Paint(CarriedColor, true);
        }

        public void ShowOnGround(Vector3 ground, BombState state)
        {
            _carrier = null;
            transform.position = ground + (Vector3.up * HalfHeight());
            switch (state)
            {
                case BombState.Planted:
                    Paint(PlantedColor, true);
                    break;
                case BombState.Defused:
                    Paint(DefusedColor, true);
                    break;
                case BombState.Detonated:
                    Paint(PlantedColor, false);
                    break;
                default:
                    Paint(DroppedColor, true);
                    break;
            }
        }

        public void Hide()
        {
            _carrier = null;
            Paint(CarriedColor, false);
        }

        private void LateUpdate()
        {
            if (_carrier != null)
            {
                Follow();
            }
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_material);
                }
                else
                {
                    DestroyImmediate(_material);
                }

                _material = null;
            }
        }

        private void Follow()
        {
            var forward = _carrier.forward;
            forward.y = 0f;
            var back = forward.sqrMagnitude > 1e-6f ? -forward.normalized : Vector3.zero;
            transform.SetPositionAndRotation(
                _carrier.position + (Vector3.up * (CarryHeight - HalfHeight())) + (back * CarryBack),
                Quaternion.LookRotation(back.sqrMagnitude > 0f ? -back : Vector3.forward, Vector3.up));
        }

        // 見た目は 1m 四方の立方体を拡縮したものなので、高さの半分は拡縮の y の半分。
        private float HalfHeight()
        {
            return transform.lossyScale.y * 0.5f;
        }

        private void Paint(Color color, bool visible)
        {
            var renderer = Renderer;
            if (renderer == null)
            {
                return;
            }

            renderer.enabled = visible;

            // 共有のマテリアルを塗り替えると他のプリミティブまで色が変わるので、この爆弾用に複製する。
            if (_material == null)
            {
                if (renderer.sharedMaterial == null)
                {
                    return;
                }

                _material = new Material(renderer.sharedMaterial) { name = name };
                renderer.sharedMaterial = _material;
            }

            _material.color = color;
        }
    }
}
