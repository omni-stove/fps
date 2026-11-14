using System;
using UnityEngine;

namespace Fps.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DeployPreview : MonoBehaviour
    {
        public static readonly Color DefaultValidColor = new Color(0.2f, 0.9f, 0.3f, 0.45f);
        public static readonly Color DefaultInvalidColor = new Color(0.95f, 0.2f, 0.2f, 0.45f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private Renderer[] _renderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock _block;
        private bool? _valid;

        public Color ValidColor { get; private set; } = DefaultValidColor;

        public Color InvalidColor { get; private set; } = DefaultInvalidColor;

        public bool? IsValid => _valid;

        // 見本が弾や置き場所の判定に当たらないよう、差し替えた見た目でも当たり判定を切る。最初の判定が出るまでは隠す。
        public static DeployPreview Attach(GameObject visual, Color validColor, Color invalidColor)
        {
            if (visual == null)
            {
                throw new ArgumentNullException(nameof(visual));
            }

            foreach (var collider in visual.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = false;
            }

            var preview = visual.GetComponent<DeployPreview>();
            if (preview == null)
            {
                preview = visual.AddComponent<DeployPreview>();
            }

            preview.ValidColor = validColor;
            preview.InvalidColor = invalidColor;
            preview._renderers = visual.GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in preview._renderers)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            preview._valid = null;
            visual.SetActive(false);
            return preview;
        }

        public void Show(Pose pose, bool valid)
        {
            transform.SetPositionAndRotation(pose.position, pose.rotation);
            if (_valid != valid)
            {
                _valid = valid;
                Paint(valid ? ValidColor : InvalidColor);
            }

            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        // 共有マテリアルを塗り替えると同じマテリアルの物まで色が変わるので、この見本だけの上書きで色を付ける。
        private void Paint(Color color)
        {
            if (_block == null)
            {
                _block = new MaterialPropertyBlock();
            }

            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            foreach (var renderer in _renderers)
            {
                if (renderer != null)
                {
                    renderer.SetPropertyBlock(_block);
                }
            }
        }
    }
}
