using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 爆破 (<see cref="SearchAndDestroyRunner"/>) の爆弾の見た目。持たれている間は持ち主の背中に付いて動き (<see cref="ShowCarried"/>)、
    /// 落ちているときと設置されたときは地面に置かれる (<see cref="ShowOnGround"/>)。爆弾の状態は Runner が <see cref="SearchAndDestroy"/> から読んで渡し、
    /// このコンポーネントは状態を持たない。当たり判定は持たせない (弾と移動を遮らないように)。色は同じ GameObject の Renderer を塗る。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BombObject : MonoBehaviour
    {
        /// <summary>持ち主の Transform から見た、背中の位置 (上へ、後ろへ)。</summary>
        public const float CarryHeight = 1.1f;

        public const float CarryBack = 0.35f;

        public static readonly Color CarriedColor = new Color(0.9f, 0.55f, 0.1f);
        public static readonly Color DroppedColor = new Color(1f, 0.85f, 0.2f);
        public static readonly Color PlantedColor = new Color(1f, 0.1f, 0.1f);
        public static readonly Color DefusedColor = new Color(0.2f, 0.8f, 0.3f);

        private Transform _carrier;
        private Renderer _renderer;
        private Material _material;

        /// <summary>今付いている持ち主。持たれていなければ null。</summary>
        public Transform Carrier => _carrier;

        /// <summary>見えているか。</summary>
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

        /// <summary>carrier の背中に付けて見せる。carrier が null なら隠す。</summary>
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

        /// <summary>
        /// 足元 ground に置いて見せる。state で色を変え、<see cref="BombState.Detonated"/> なら隠す (起爆で無くなった)。
        /// </summary>
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

        /// <summary>隠す。</summary>
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

        /// <summary>見た目は 1m 四方の立方体を拡縮したものなので、高さの半分は拡縮の y の半分。</summary>
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
