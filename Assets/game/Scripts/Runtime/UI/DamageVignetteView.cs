using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 被弾の画面効果と低体力の演出。Hud.uxml の <see cref="UxmlName"/> (画面全体を覆う要素) に
    /// <see cref="DamageFeedbackCatalog.Vignette"/> を引き伸ばして重ね、不透明度で強さを出す。
    /// <see cref="CombatEvents.DamageTaken"/> を受けると端を赤くして薄くし、同じ GameObject の <see cref="Hud"/> の体力
    /// (<see cref="HudModel.HealthFraction"/>) がしきい値以下の間は端の赤と心音を続け、回復したら止めて息切れの音を鳴らす。
    /// 強さと低体力の判定は <see cref="DamageFeedback"/> が決める。
    /// オフラインとオンラインで同じ HUD (<see cref="Hud.AddViews"/>) に付き、どちらも同じイベントと HudModel の体力を受ける。
    /// カタログが無ければ警告を 1 度だけ出し、画像も音も出さずに判定とログだけ続ける。
    /// </summary>
    /// <remarks>
    /// HUD の量 (<see cref="HudVisibility"/>) では隠さない。体力の表示を隠す量でも、これが体力の減りを知る手がかりになるため。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DamageVignetteView : HudElementView
    {
        public const string LogTag = "[DamageFeedback]";

        /// <summary>Hud.uxml の画面端の効果の要素。</summary>
        public const string UxmlName = "damage-vignette";

        private static bool _warnedMissingCatalog;

        private DamageFeedbackCatalog _catalog;
        private bool _catalogResolved;
        private DamageFeedback _feedback;
        private Hud _hud;
        private AudioSource _heartbeatSource;
        private AudioSource _oneShotSource;

        public override string ElementName => UxmlName;

        /// <summary>
        /// 素材と値を引くカタログ。初めて読んだときに <see cref="DamageFeedbackCatalog.Load"/> で探し、無ければ null。
        /// 代入すると探さずにそれを使い (null で素材なし)、強さと低体力の状態は最初からにする。
        /// </summary>
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
                            + "(run Fps/Build All Presentation Assets)", this);
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

        /// <summary>今の強さと低体力の状態。初めて読んだときに <see cref="Catalog"/> の値で作る。</summary>
        public DamageFeedback Feedback => _feedback ??= new DamageFeedback(ResolveSettings());

        /// <summary>画面に出している強さ ([0, 1])。</summary>
        public float Intensity => Feedback.Intensity;

        /// <summary>しきい値以下の体力でいるか。</summary>
        public bool IsLowHealth => Feedback.IsLowHealth;

        /// <summary>心音を鳴らしているか。</summary>
        public bool IsHeartbeatPlaying => _heartbeatSource != null && _heartbeatSource.isPlaying;

        private void OnEnable()
        {
            CombatEvents.DamageTaken += OnDamageTaken;
            _hud = GetComponent<Hud>();
            if (_hud != null)
            {
                _hud.Model.Changed += OnModelChanged;
                SetHealthFraction(_hud.Model.HealthFraction);
            }
        }

        private void OnDisable()
        {
            CombatEvents.DamageTaken -= OnDamageTaken;
            if (_hud != null)
            {
                _hud.Model.Changed -= OnModelChanged;
                _hud = null;
            }

            StopHeartbeat();
            _feedback?.Reset();
            Apply();
        }

        private void LateUpdate()
        {
            // ポーズ中に被弾の赤が残り続けないよう、時間の止まらない経過で消す。
            Tick(Time.unscaledDeltaTime);
        }

        /// <summary>amount のダメージを受けた効果を出す。0 以下のダメージでは何もしない。</summary>
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

        /// <summary>
        /// 体力の割合 (null は体力が無い) を渡す。しきい値以下に下がったら心音を始め、回復したら止めて息切れの音を鳴らす。
        /// 倒れた (0) ときと体力が無くなったときは心音を止めるだけ。
        /// </summary>
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

        /// <summary>deltaSeconds 秒進めて強さを直す。0 以下か有限でない値では進めない。</summary>
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
            if (field == HudField.Health && _hud != null)
            {
                SetHealthFraction(_hud.Model.HealthFraction);
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
