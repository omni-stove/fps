using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 画像が空なら覆う側が色だけで覆い、音が空なら鳴らさず、熱源視の材質が空なら ThermalHighlighter がコードで作る。カタログが無ければ Default* の値で同じようにする。
    [CreateAssetMenu(fileName = "VisionVisualsCatalog", menuName = "Shooter/Vision Visuals Catalog")]
    public sealed class VisionVisualCatalog : ScriptableObject
    {
        public const string CatalogName = "VisionVisuals";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        public static readonly Color DefaultNightColor = new Color(0.25f, 1f, 0.35f, 0.35f);

        public static readonly Color DefaultThermalColor = new Color(1f, 0.55f, 0.15f, 0.35f);

        public static readonly Color DefaultThermalHighlightColor = new Color(1f, 0.95f, 0.75f, 1f);

        public const float DefaultSwitchVolume = 0.6f;

        public const float DefaultThermalRescanSeconds = 0.5f;

        public const float MinThermalRescanSeconds = 0.05f;

        [SerializeField, Tooltip("暗視のときに画面 (照準器なら照準の中) に重ねる画像。空なら色だけで覆う")]
        private Texture2D _nightImage;

        [SerializeField, Tooltip("暗視のときに覆う色。画像があれば画像に掛ける")]
        private Color _nightColor = DefaultNightColor;

        [SerializeField, Tooltip("熱源視のときに重ねる画像。空なら色だけで覆う")]
        private Texture2D _thermalImage;

        [SerializeField, Tooltip("熱源視のときに覆う色。画像があれば画像に掛ける")]
        private Color _thermalColor = DefaultThermalColor;

        [SerializeField, Tooltip("見え方を入れたときに 1 度鳴らす音")]
        private AudioClip _switchOn;

        [SerializeField, Tooltip("見え方を切ったときに 1 度鳴らす音")]
        private AudioClip _switchOff;

        [SerializeField, Range(0f, 1f), Tooltip("入れ切りの音の音量")]
        private float _switchVolume = DefaultSwitchVolume;

        [SerializeField, Tooltip("熱源視の間、敵の体 (Damageable の下の Renderer) に貼る材質。空なら熱源の色で光る材質をコードで作る")]
        private Material _thermalMaterial;

        [SerializeField, Tooltip("熱源視の材質が空のとき、コードで作る材質の色")]
        private Color _thermalHighlightColor = DefaultThermalHighlightColor;

        [SerializeField, Min(MinThermalRescanSeconds), Tooltip("熱源視の間に、新しく現れた敵と倒れた敵を探し直す間隔 (秒)")]
        private float _thermalRescanSeconds = DefaultThermalRescanSeconds;

        public Texture2D NightImage
        {
            get => _nightImage;
            set => _nightImage = value;
        }

        public Color NightColor
        {
            get => _nightColor;
            set => _nightColor = value;
        }

        public Texture2D ThermalImage
        {
            get => _thermalImage;
            set => _thermalImage = value;
        }

        public Color ThermalColor
        {
            get => _thermalColor;
            set => _thermalColor = value;
        }

        public AudioClip SwitchOn
        {
            get => _switchOn;
            set => _switchOn = value;
        }

        public AudioClip SwitchOff
        {
            get => _switchOff;
            set => _switchOff = value;
        }

        public float SwitchVolume
        {
            get => _switchVolume;
            set => _switchVolume = Mathf.Clamp01(value);
        }

        public Material ThermalMaterial
        {
            get => _thermalMaterial;
            set => _thermalMaterial = value;
        }

        public Color ThermalHighlightColor
        {
            get => _thermalHighlightColor;
            set => _thermalHighlightColor = value;
        }

        public float ThermalRescanSeconds
        {
            get => _thermalRescanSeconds;
            set => _thermalRescanSeconds = Mathf.Max(MinThermalRescanSeconds, value);
        }

        public static AudioClip SwitchSoundFor(VisionVisualCatalog catalog, bool on)
        {
            if (catalog == null)
            {
                return null;
            }

            return on ? catalog.SwitchOn : catalog.SwitchOff;
        }

        public static float SwitchVolumeOf(VisionVisualCatalog catalog)
        {
            return catalog != null ? catalog.SwitchVolume : DefaultSwitchVolume;
        }

        public static Color ThermalHighlightColorOf(VisionVisualCatalog catalog)
        {
            return catalog != null ? catalog.ThermalHighlightColor : DefaultThermalHighlightColor;
        }

        // アセットの Inspector で下限より小さく入っていても、毎フレーム探し直さないよう下限で止める。
        public static float ThermalRescanSecondsOf(VisionVisualCatalog catalog)
        {
            return catalog != null ? Mathf.Max(MinThermalRescanSeconds, catalog.ThermalRescanSeconds) : DefaultThermalRescanSeconds;
        }

        public static VisionVisualCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.VisionVisuals);
            return fromContent != null ? fromContent : Resources.Load<VisionVisualCatalog>(ResourcePath);
        }

        // カタログが無いときも覆う側が同じ式で色を引けるよう、null の catalog を受ける。
        public static Color ColorFor(VisionVisualCatalog catalog, VisionMode mode)
        {
            switch (mode)
            {
                case VisionMode.Night:
                    return catalog != null ? catalog.NightColor : DefaultNightColor;
                case VisionMode.Thermal:
                    return catalog != null ? catalog.ThermalColor : DefaultThermalColor;
                default:
                    return Color.clear;
            }
        }

        public static Texture2D ImageFor(VisionVisualCatalog catalog, VisionMode mode)
        {
            if (catalog == null)
            {
                return null;
            }

            switch (mode)
            {
                case VisionMode.Night:
                    return catalog.NightImage;
                case VisionMode.Thermal:
                    return catalog.ThermalImage;
                default:
                    return null;
            }
        }
    }
}
