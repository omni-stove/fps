using System;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // HUD の量 (HudVisibility) では隠さない。体力の表示を隠す量でも、これが体力の減りを知る手がかりになるため。
    [DisallowMultipleComponent]
    public sealed class DamageVignetteView : HudElementView
    {
        public const string LogTag = "[DamageFeedback]";

        public const string UxmlName = "damage-vignette";

        private static bool _warnedMissingCatalog;

        private DamageFeedbackCatalog _catalog;
        private bool _catalogResolved;
        private DamageFeedback _feedback;
        private Hud _hud;
        private HudModel _model;
        private AudioSource _heartbeatSource;
        private AudioSource _oneShotSource;

        public override string ElementName => UxmlName;

        public DamageFeedbackCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = DamageFeedbackCatalog.Load();
                    _catalogResolved = true;
                    if (_catalog == null && !_warnedMissingCatalog)
                    {
                        _warnedMissingCatalog = true;
                        Debug.LogWarning(
                            $"{LogTag} catalog 'Resources/{DamageFeedbackCatalog.ResourcePath}' was not found; damage vignette and heartbeat are off "
                            + "(run Shooter/Build All Presentation Assets)", this);
                    }
                }

                return _catalog;
            }
            set
            {
                StopHeartbeat();
                _catalog = value;
                _catalogResolved = true;
                _feedback = null;
                Apply();
            }
        }

        public DamageFeedback Feedback => _feedback ??= new DamageFeedback(ResolveSettings());

        public float Intensity => Feedback.Intensity;

        public bool IsLowHealth => Feedback.IsLowHealth;

        public bool IsHeartbeatPlaying => _heartbeatSource != null && _heartbeatSource.isPlaying;

        private void OnEnable()
        {
            CombatEvents.DamageTaken += OnDamageTaken;
            _hud = GetComponent<Hud>();
            if (_hud != null)
            {
                _hud.ShownModelChanged += OnShownModelChanged;
                WatchModel(_hud.ShownModel);
            }
        }

        private void OnDisable()
        {
            CombatEvents.DamageTaken -= OnDamageTaken;
            if (_hud != null)
            {
                _hud.ShownModelChanged -= OnShownModelChanged;
                _hud = null;
            }

            WatchModel(null);

            StopHeartbeat();
            _feedback?.Reset();
            Apply();
        }

        private void LateUpdate()
        {
            // ポーズ中に被弾の赤が残り続けないよう、時間の止まらない経過で消す。
            Tick(Time.unscaledDeltaTime);
        }

        public void ShowHit(int amount)
        {
            var feedback = Feedback;
            if (!feedback.Hit(amount))
            {
                return;
            }

            Debug.Log(
                $"{LogTag} hit amount={amount.ToString(CultureInfo.InvariantCulture)} intensity={Format(feedback.Intensity)}", this);
            Apply();
        }

        public void SetHealthFraction(float? fraction)
        {
            var feedback = Feedback;
            var change = feedback.SetHealth(fraction);
            switch (change)
            {
                case LowHealthChange.Started:
                    Debug.Log(
                        $"{LogTag} low health start fraction={Format(feedback.HealthFraction ?? 0f)} threshold={Format(feedback.Settings.LowHealthThreshold)}",
                        this);
                    StartHeartbeat();
                    break;
                case LowHealthChange.Recovered:
                    Debug.Log($"{LogTag} low health end reason=recovered fraction={Format(feedback.HealthFraction ?? 0f)}", this);
                    StopHeartbeat();
                    PlayBreath();
                    break;
                case LowHealthChange.Cleared:
                    Debug.Log($"{LogTag} low health end reason={(feedback.HealthFraction.HasValue ? "down" : "cleared")}", this);
                    StopHeartbeat();
                    break;
            }

            Apply();
        }

        public void Tick(float deltaSeconds)
        {
            if (_feedback == null)
            {
                return;
            }

            var before = _feedback.Intensity;
            _feedback.Tick(deltaSeconds);
            if (before != _feedback.Intensity)
            {
                Apply();
            }

            if (IsHeartbeatPlaying)
            {
                _heartbeatSource.volume = HeartbeatVolume();
            }
        }

        protected override void OnBound(VisualElement element)
        {
            Apply();
        }

        private void OnDamageTaken(DamageTakenInfo info)
        {
            ShowHit(info.Amount);
        }

        private void OnModelChanged(HudField field)
        {
            if (field == HudField.Health && _model != null)
            {
                SetHealthFraction(_model.HealthFraction);
            }
        }

        private void OnShownModelChanged()
        {
            if (_hud != null)
            {
                WatchModel(_hud.ShownModel);
            }
        }

        private void WatchModel(HudModel model)
        {
            if (_model != null)
            {
                _model.Changed -= OnModelChanged;
            }

            _model = model;
            if (_model != null)
            {
                _model.Changed += OnModelChanged;
                SetHealthFraction(_model.HealthFraction);
            }
        }

        private DamageFeedbackSettings ResolveSettings()
        {
            var catalog = Catalog;
            if (catalog == null)
            {
                return DamageFeedbackSettings.Default;
            }

            try
            {
                return catalog.ToSettings();
            }
            catch (ArgumentOutOfRangeException e)
            {
                Debug.LogError($"{LogTag} catalog '{catalog.name}' has an invalid value; using the defaults: {e.Message}", catalog);
                return DamageFeedbackSettings.Default;
            }
        }

        private void Apply()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            // Edit Mode で HUD を作っただけ (OnEnable が走らない) のときに、カタログが無い警告を出さないため。
            var intensity = _feedback != null ? _feedback.Intensity : 0f;
            var catalog = intensity > 0f ? Catalog : null;
            var texture = catalog != null ? catalog.Vignette : null;
            if (texture == null)
            {
                element.style.display = DisplayStyle.None;
                return;
            }

            element.style.backgroundImage = new StyleBackground(texture);
            element.style.unityBackgroundImageTintColor = catalog.Tint;
            element.style.opacity = intensity;
            element.style.display = DisplayStyle.Flex;
        }

        private void StartHeartbeat()
        {
            var catalog = Catalog;
            var clip = catalog != null ? catalog.Heartbeat : null;
            if (clip == null)
            {
                return;
            }

            var source = _heartbeatSource != null ? _heartbeatSource : (_heartbeatSource = CreateSource(loop: true));
            source.clip = clip;
            source.volume = HeartbeatVolume();
            if (!source.isPlaying)
            {
                source.Play();
            }
        }

        private void StopHeartbeat()
        {
            if (_heartbeatSource != null && _heartbeatSource.isPlaying)
            {
                _heartbeatSource.Stop();
            }
        }

        private void PlayBreath()
        {
            var catalog = Catalog;
            var clip = catalog != null ? catalog.Breath : null;
            if (clip == null)
            {
                return;
            }

            var source = _oneShotSource != null ? _oneShotSource : (_oneShotSource = CreateSource(loop: false));
            source.PlayOneShot(clip, catalog.BreathVolume * AppliedControlSettings.Current.EffectsVolume);
        }

        // 設定の効果音の音量を鳴らしている途中で変えても追うよう、鳴らすたびと Tick で読み直す。
        private float HeartbeatVolume()
        {
            var catalog = Catalog;
            return catalog != null ? catalog.HeartbeatVolume * AppliedControlSettings.Current.EffectsVolume : 0f;
        }

        private AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = loop;
            return source;
        }

        private static string Format(float value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        // ドメインの再読み込みを切った Play でも、Play ごとに 1 度は警告を出す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetWarning()
        {
            _warnedMissingCatalog = false;
        }
    }
}
