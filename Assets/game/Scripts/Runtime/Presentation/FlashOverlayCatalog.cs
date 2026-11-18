using UnityEngine;

namespace Shooter.Runtime
{
    // 枠が空なら FlashOverlay が無地の色で覆い、音は鳴らさない。カタログが無ければ既定の値で同じようにする。
    [CreateAssetMenu(fileName = "FlashOverlayCatalog", menuName = "Shooter/Flash Overlay Catalog")]
    public sealed class FlashOverlayCatalog : ScriptableObject
    {
        public const string CatalogName = "FlashOverlay";

        public const float DefaultFadeSeconds = 1.5f;

        public const float MinFadeSeconds = 0.05f;

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("画面全体に引き伸ばして重ねる画像。空なら色だけで覆う")]
        private Texture2D _image;

        [SerializeField, Tooltip("覆う色。画像があれば画像に掛ける")]
        private Color _color = Color.white;

        [SerializeField, Min(MinFadeSeconds), Tooltip("目くらましの残りがこの秒数を切ってから薄れ始める。短い目くらましは初めから薄い")]
        private float _fadeSeconds = DefaultFadeSeconds;

        [SerializeField, Tooltip("目がくらんだときに 1 度鳴らす耳鳴り")]
        private AudioClip _ringing;

        [SerializeField, Range(0f, 1f), Tooltip("耳鳴りの音量")]
        private float _ringingVolume = 0.7f;

        public Texture2D Image
        {
            get => _image;
            set => _image = value;
        }

        public Color Color
        {
            get => _color;
            set => _color = value;
        }

        public float FadeSeconds
        {
            get => Mathf.Max(MinFadeSeconds, _fadeSeconds);
            set => _fadeSeconds = Mathf.Max(MinFadeSeconds, value);
        }

        public AudioClip Ringing
        {
            get => _ringing;
            set => _ringing = value;
        }

        public float RingingVolume
        {
            get => _ringingVolume;
            set => _ringingVolume = Mathf.Clamp01(value);
        }

        public static FlashOverlayCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.FlashOverlay);
            return fromContent != null ? fromContent : Resources.Load<FlashOverlayCatalog>(ResourcePath);
        }
    }
}
