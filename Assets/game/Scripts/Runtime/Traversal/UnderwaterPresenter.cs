using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime.Traversal
{
    // 目 (カメラ) と体 (この位置 = 足元) を別に見るのは、立ち泳ぎで顔が出ている間は息が続き、色と音も変えないため。
    // UI Toolkit の HUD に足さず OnGUI で重ねるのは、SuppressionVignette と同じく、HUD を差し替えたゲームでも消えないようにするため。
    [DisallowMultipleComponent]
    public sealed class UnderwaterPresenter : MonoBehaviour
    {
        [SerializeField, Tooltip("水中かを見る目。空ならメインカメラ、それも無ければこの位置")]
        private Transform _eye;

        [SerializeField, Tooltip("息が切れたらダメージを受けるもの。空なら親から探す")]
        private Damageable _damageable;

        private IWaterVolumes _water = WaterVolumes.Scene;
        private SwimTuning _swim;
        private DrowningClock _clock;
        private WaterCatalog _catalog;
        private bool _catalogResolved;
        private Texture2D _placeholder;
        private AudioSource _ambientSource;
        private AudioSource _swimSource;
        private AudioLowPassFilter _muffle;
        private bool _muffleAdded;
        private bool _muffleWasEnabled;
        private float _muffleCutoffBefore;
        private Vector3 _lastBodyPosition;
        private bool _hasLastBody;

        public event Action<Vector3> Splashed;

        public event Action<int> Drowned;

        public Transform Eye
        {
            get
            {
                if (_eye != null)
                {
                    return _eye;
                }

                var main = Camera.main;
                return main != null ? main.transform : transform;
            }
            set => _eye = value;
        }

        public Damageable Damageable
        {
            get => _damageable != null ? _damageable : (_damageable = GetComponentInParent<Damageable>());
            set => _damageable = value;
        }

        // 予測とサーバーに渡す写しでなくその場の水を引くのは、見た目と音は今開いている水に合わせればよいため。
        public IWaterVolumes Water
        {
            get => _water;
            set => _water = value ?? throw new ArgumentNullException(nameof(value));
        }

        // 既定の値でなく traversal の調整値を読み、ゲームが息の長さと溺れのダメージをデータで変えられるようにする。
        public SwimTuning Swim
        {
            get => _swim ??= TraversalTuning.Current.Swim;
            set
            {
                _swim = value ?? throw new ArgumentNullException(nameof(value));
                if (_clock != null)
                {
                    _clock.Tuning = _swim;
                }
            }
        }

        // ネット対戦では体力をサーバーが持つので、つなぐ側が切れるようにする。
        public bool AppliesDrowning { get; set; } = true;

        public WaterCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalogResolved = true;
                    _catalog = WaterCatalog.Load();
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
            }
        }

        public bool IsEyeUnderwater { get; private set; }

        public bool IsBodyInWater { get; private set; }

        public bool IsSwimSoundPlaying => _swimSource != null && _swimSource.isPlaying;

        public float BreathFraction => Clock.BreathFraction;

        public Color OverlayColor => Catalog != null ? Catalog.OverlayColor : WaterCatalog.DefaultOverlayColor;

        public Texture2D OverlayTexture
        {
            get
            {
                var catalog = Catalog;
                if (catalog != null && catalog.Overlay != null)
                {
                    return catalog.Overlay;
                }

                if (_placeholder == null)
                {
                    _placeholder = CreatePlaceholder();
                }

                return _placeholder;
            }
        }

        public bool IsMuffled => _muffle != null && _muffle.enabled && IsEyeUnderwater;

        private DrowningClock Clock => _clock ??= new DrowningClock(Swim);

        public static Texture2D CreatePlaceholder()
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "UnderwaterOverlay (placeholder)",
                hideFlags = HideFlags.DontSave,
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, false);
            return texture;
        }

        // Edit Mode では Update が走らないので、テストが直接呼べるよう公開する。
        public void Step(float seconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be finite and not negative");
            }

            var eye = Eye.position;
            var body = transform.position;
            var eyeUnder = IsBelowSurface(eye, out _);
            var bodyIn = IsBelowSurface(body, out var bodySurface);

            if (bodyIn && !IsBodyInWater && _hasLastBody)
            {
                Splash(new Vector3(body.x, bodySurface, body.z));
            }

            IsBodyInWater = bodyIn;
            if (eyeUnder != IsEyeUnderwater)
            {
                IsEyeUnderwater = eyeUnder;
                SetMuffled(eyeUnder);
                SetLoop(ref _ambientSource, eyeUnder, Catalog != null ? Catalog.UnderwaterLoop : null, Catalog != null ? Catalog.UnderwaterVolume : 0f);
            }

            var horizontalSpeed = 0f;
            if (_hasLastBody && seconds > 0f)
            {
                var moved = body - _lastBodyPosition;
                moved.y = 0f;
                horizontalSpeed = moved.magnitude / seconds;
            }

            _lastBodyPosition = body;
            _hasLastBody = true;

            var minSpeed = Catalog != null ? Catalog.SwimSoundMinSpeed : WaterCatalog.DefaultSwimSoundMinSpeed;
            SetLoop(ref _swimSource, bodyIn && horizontalSpeed >= minSpeed, Catalog != null ? Catalog.SwimSound : null, Catalog != null ? Catalog.SwimVolume : 0f);

            Drown(eyeUnder, seconds);
        }

        private bool IsBelowSurface(Vector3 position, out float surface)
        {
            var found = _water.SurfaceAt(position.ToNumerics());
            surface = found ?? 0f;
            return found.HasValue && position.y < found.Value;
        }

        private void Drown(bool eyeUnder, float seconds)
        {
            var target = Damageable;
            if (!AppliesDrowning || target == null || target.IsDead)
            {
                Clock.Reset();
                return;
            }

            var damage = Clock.Tick(eyeUnder, seconds);
            if (damage <= 0)
            {
                return;
            }

            target.TakeDamage(damage, null, null, DamageKind.Drowning);
            Debug.Log(WaterLog.Drown(target.Id, damage, target.Health.Current), this);
            Drowned?.Invoke(damage);
        }

        private void Splash(Vector3 point)
        {
            Splashed?.Invoke(point);

            // Edit Mode では時間差で消せず、専用サーバーには見る人がいない。
            var catalog = Catalog;
            if (catalog == null || !Application.isPlaying || NetStartup.ShouldDisablePresentation(NetStartup.IsDedicatedServerBuild))
            {
                return;
            }

            if (catalog.Splash != null)
            {
                var effect = Instantiate(catalog.Splash, point, Quaternion.identity);
                Destroy(effect, catalog.SplashLifetimeSeconds);
            }

            if (catalog.SplashSound != null)
            {
                AudioSource.PlayClipAtPoint(catalog.SplashSound, point, catalog.SplashVolume);
            }
        }

        private void SetLoop(ref AudioSource source, bool on, AudioClip clip, float volume)
        {
            if (!on || clip == null)
            {
                if (source != null && source.isPlaying)
                {
                    source.Stop();
                }

                return;
            }

            if (source == null)
            {
                source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
            }

            source.volume = volume;
            if (source.clip != clip)
            {
                source.clip = clip;
                source.Stop();
            }

            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        // 聞こえる音すべてをこもらせるため、音源ごとでなく耳 (AudioListener) に掛ける。耳に元からある filter は値を戻す。
        private void SetMuffled(bool on)
        {
            if (on)
            {
                var listener = FindListener();
                if (listener == null)
                {
                    return;
                }

                if (_muffle == null)
                {
                    _muffle = listener.GetComponent<AudioLowPassFilter>();
                    _muffleAdded = _muffle == null;
                    if (_muffleAdded)
                    {
                        _muffle = listener.gameObject.AddComponent<AudioLowPassFilter>();
                        _muffleWasEnabled = false;
                        _muffleCutoffBefore = WaterCatalog.MaxCutoffHz;
                    }
                    else
                    {
                        _muffleWasEnabled = _muffle.enabled;
                        _muffleCutoffBefore = _muffle.cutoffFrequency;
                    }
                }

                _muffle.cutoffFrequency = Catalog != null ? Catalog.MuffleCutoffHz : WaterCatalog.DefaultMuffleCutoffHz;
                _muffle.enabled = true;
                return;
            }

            if (_muffle == null)
            {
                return;
            }

            _muffle.cutoffFrequency = _muffleCutoffBefore;
            _muffle.enabled = _muffleWasEnabled;
        }

        private AudioListener FindListener()
        {
            var onEye = Eye.GetComponentInParent<AudioListener>();
            return onEye != null ? onEye : FindAnyObjectByType<AudioListener>();
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        private void OnGUI()
        {
            if (!IsEyeUnderwater || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var color = OverlayColor;
            if (color.a <= 0f)
            {
                return;
            }

            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), OverlayTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private void OnDisable()
        {
            if (IsEyeUnderwater)
            {
                SetMuffled(false);
            }

            IsEyeUnderwater = false;
            IsBodyInWater = false;
            _hasLastBody = false;
            SetLoop(ref _ambientSource, false, null, 0f);
            SetLoop(ref _swimSource, false, null, 0f);
            _clock?.Reset();
        }

        private void OnDestroy()
        {
            if (_muffle != null && _muffleAdded)
            {
                DestroyObject(_muffle);
                _muffle = null;
            }

            if (_placeholder != null)
            {
                DestroyObject(_placeholder);
                _placeholder = null;
            }
        }

        private static void DestroyObject(UnityEngine.Object target)
        {
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
