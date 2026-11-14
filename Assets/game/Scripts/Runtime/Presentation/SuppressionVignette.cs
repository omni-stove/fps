using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // UI Toolkit の HUD に足さず OnGUI で描くのは、HUD を差し替えたゲームでも既定の演出が消えないようにするため。
    [DisallowMultipleComponent]
    public sealed class SuppressionVignette : MonoBehaviour, ISuppressionPresenter
    {
        public const int PlaceholderSize = 64;

        // 中心から端までのうち、暗くなり始める位置の割合。照準のまわりは隠さない。
        public const float PlaceholderInnerRadius = 0.45f;

        private Texture2D _texture;
        private Texture2D _placeholder;
        private Color _tint = Color.black;
        private bool _resolved;

        public float Alpha { get; private set; }

        public Texture2D Texture
        {
            get
            {
                Resolve();
                return _texture;
            }
        }

        public bool UsesPlaceholder
        {
            get
            {
                Resolve();
                return _texture == _placeholder;
            }
        }

        public void Present(float level, SuppressionSpec spec)
        {
            var max = spec != null ? spec.MaxVignetteAlpha : 0f;
            Alpha = float.IsNaN(level) ? 0f : Mathf.Clamp01(level) * max;
        }

        // カタログに模様が無いときの仮のもの。読み返せるよう CPU 側の画素を残す (テストが確かめる)。
        public static Texture2D CreatePlaceholder()
        {
            var texture = new Texture2D(PlaceholderSize, PlaceholderSize, TextureFormat.RGBA32, false)
            {
                name = "SuppressionVignette (placeholder)",
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };

            var pixels = new Color32[PlaceholderSize * PlaceholderSize];
            var half = (PlaceholderSize - 1) / 2f;
            for (var y = 0; y < PlaceholderSize; y++)
            {
                for (var x = 0; x < PlaceholderSize; x++)
                {
                    // 角まで含めて端で 1 になるよう、中心からの距離を対角の半分で割る。
                    var dx = (x - half) / half;
                    var dy = (y - half) / half;
                    var radius = Mathf.Sqrt((dx * dx) + (dy * dy)) / Mathf.Sqrt(2f);
                    var alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PlaceholderInnerRadius, 1f, radius));
                    pixels[(y * PlaceholderSize) + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private void Resolve()
        {
            if (_resolved && _texture != null)
            {
                return;
            }

            _resolved = true;
            var catalog = SuppressionCatalog.Load();
            if (catalog != null && catalog.Vignette != null)
            {
                _texture = catalog.Vignette;
                _tint = catalog.Tint;
                return;
            }

            if (_placeholder == null)
            {
                _placeholder = CreatePlaceholder();
            }

            _texture = _placeholder;
            _tint = catalog != null ? catalog.Tint : Color.black;
        }

        private void OnGUI()
        {
            if (Alpha <= 0f || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var texture = Texture;
            if (texture == null)
            {
                return;
            }

            var previous = GUI.color;
            GUI.color = new Color(_tint.r, _tint.g, _tint.b, Alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), texture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private void OnDisable()
        {
            Alpha = 0f;
        }

        private void OnDestroy()
        {
            if (_placeholder == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_placeholder);
            }
            else
            {
                DestroyImmediate(_placeholder);
            }

            _placeholder = null;
        }
    }
}
