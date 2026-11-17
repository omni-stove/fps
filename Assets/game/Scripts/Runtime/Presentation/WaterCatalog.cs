using UnityEngine;

namespace Shooter.Runtime
{
    // 枠が空なら UnderwaterPresenter が無地の色で覆い、その音・水しぶきだけを出さない。カタログが無ければ既定の値で同じようにする。
    [CreateAssetMenu(fileName = "WaterCatalog", menuName = "Shooter/Water Catalog")]
    public sealed class WaterCatalog : ScriptableObject
    {
        public const string CatalogName = "Water";

        public static readonly Color DefaultOverlayColor = new Color(0.08f, 0.32f, 0.42f, 0.45f);

        public const float DefaultMuffleCutoffHz = 900f;

        public const float MinCutoffHz = 10f;

        public const float MaxCutoffHz = 22000f;

        public const float DefaultSwimSoundMinSpeed = 0.5f;

        public const float DefaultSplashLifetimeSeconds = 3f;

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("目が水中のとき画面全体に引き伸ばして重ねる画像。空なら色だけで覆う")]
        private Texture2D _overlay;

        [SerializeField, Tooltip("重ねる色。画像があれば画像に掛ける。a が濃さ")]
        private Color _overlayColor = DefaultOverlayColor;

        [SerializeField, Range(MinCutoffHz, MaxCutoffHz), Tooltip("目が水中のとき、聞こえる音からこの周波数より高い音を削る (Hz)")]
        private float _muffleCutoffHz = DefaultMuffleCutoffHz;

        [SerializeField, Tooltip("目が水中の間、繰り返し鳴らす音。空なら鳴らさない")]
        private AudioClip _underwaterLoop;

        [SerializeField, Range(0f, 1f), Tooltip("水中の音の音量")]
        private float _underwaterVolume = 0.6f;

        [SerializeField, Tooltip("水の中を進んでいる間、繰り返し鳴らす泳ぐ音。空なら鳴らさない")]
        private AudioClip _swimSound;

        [SerializeField, Range(0f, 1f), Tooltip("泳ぐ音の音量")]
        private float _swimVolume = 0.5f;

        [SerializeField, Min(0f), Tooltip("水平にこの速さ (m/s) 以上で進んでいる間だけ泳ぐ音を鳴らす")]
        private float _swimSoundMinSpeed = DefaultSwimSoundMinSpeed;

        [SerializeField, Tooltip("水に入ったとき水面に出す水しぶき。+Y を上に向けて置く。空なら出さない")]
        private GameObject _splash;

        [SerializeField, Min(0.1f), Tooltip("水しぶきを消すまでの秒数")]
        private float _splashLifetimeSeconds = DefaultSplashLifetimeSeconds;

        [SerializeField, Tooltip("水に入ったときの音。空なら鳴らさない")]
        private AudioClip _splashSound;

        [SerializeField, Range(0f, 1f), Tooltip("水に入ったときの音の音量")]
        private float _splashVolume = 0.8f;

        public Texture2D Overlay
        {
            get => _overlay;
            set => _overlay = value;
        }

        public Color OverlayColor
        {
            get => _overlayColor;
            set => _overlayColor = value;
        }

        public float MuffleCutoffHz
        {
            get => Mathf.Clamp(_muffleCutoffHz, MinCutoffHz, MaxCutoffHz);
            set => _muffleCutoffHz = Mathf.Clamp(value, MinCutoffHz, MaxCutoffHz);
        }

        public AudioClip UnderwaterLoop
        {
            get => _underwaterLoop;
            set => _underwaterLoop = value;
        }

        public float UnderwaterVolume
        {
            get => _underwaterVolume;
            set => _underwaterVolume = Mathf.Clamp01(value);
        }

        public AudioClip SwimSound
        {
            get => _swimSound;
            set => _swimSound = value;
        }

        public float SwimVolume
        {
            get => _swimVolume;
            set => _swimVolume = Mathf.Clamp01(value);
        }

        public float SwimSoundMinSpeed
        {
            get => Mathf.Max(0f, _swimSoundMinSpeed);
            set => _swimSoundMinSpeed = Mathf.Max(0f, value);
        }

        public GameObject Splash
        {
            get => _splash;
            set => _splash = value;
        }

        public float SplashLifetimeSeconds
        {
            get => Mathf.Max(0.1f, _splashLifetimeSeconds);
            set => _splashLifetimeSeconds = Mathf.Max(0.1f, value);
        }

        public AudioClip SplashSound
        {
            get => _splashSound;
            set => _splashSound = value;
        }

        public float SplashVolume
        {
            get => _splashVolume;
            set => _splashVolume = Mathf.Clamp01(value);
        }

        public static WaterCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.Water);
            return fromContent != null ? fromContent : Resources.Load<WaterCatalog>(ResourcePath);
        }
    }
}
