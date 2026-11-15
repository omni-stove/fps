using System;
using System.Globalization;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    // 残りを毎フレーム読みに行くのは、目くらましが延びても知らせ (Started) は来ないため。
    // 被弾の赤 (DamageVignetteView) と同じく HUD の量では隠さない。見えないことそのものが効き目だから。
    [DisallowMultipleComponent]
    public sealed class FlashOverlay : HudElementView
    {
        public const string LogTag = "[Flash]";

        public const string UxmlName = "flash-overlay";

        private FlashOverlayCatalog _catalog;
        private bool _catalogResolved;
        private Func<float> _remainingSeconds;
        private AudioSource _ringingSource;

        public override string ElementName => UxmlName;

        public float Opacity { get; private set; }

        public FlashOverlayCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = FlashOverlayCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
                Apply();
            }
        }

        public float FadeSeconds => Catalog != null ? Catalog.FadeSeconds : FlashOverlayCatalog.DefaultFadeSeconds;

        public static float OpacityFor(float remainingSeconds, float fadeSeconds)
        {
            if (!(remainingSeconds > 0f) || !(fadeSeconds > 0f))
            {
                return 0f;
            }

            return Mathf.Clamp01(remainingSeconds / fadeSeconds);
        }

        public void Track(Func<float> remainingBlindSeconds)
        {
            _remainingSeconds = remainingBlindSeconds;
            Refresh();
        }

        public void Refresh()
        {
            var remaining = 0f;
            if (_remainingSeconds != null)
            {
                remaining = _remainingSeconds();
            }

            var opacity = OpacityFor(remaining, FadeSeconds);
            if (opacity > 0f && Opacity <= 0f)
            {
                Debug.Log(LogTag + " overlay start remaining=" + remaining.ToString("F2", CultureInfo.InvariantCulture), this);
                PlayRinging();
            }
            else if (opacity <= 0f && Opacity > 0f)
            {
                Debug.Log(LogTag + " overlay end", this);
                StopRinging();
            }

            if (opacity != Opacity)
            {
                Opacity = opacity;
                Apply();
            }
        }

        protected override void OnBound(VisualElement element)
        {
            Apply();
        }

        private void OnEnable()
        {
            StatusEffectReceiver.LocalPlayerBlinded += HandleLocalPlayerBlinded;
        }

        private void OnDisable()
        {
            StatusEffectReceiver.LocalPlayerBlinded -= HandleLocalPlayerBlinded;
            StopRinging();
            Opacity = 0f;
            Apply();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void HandleLocalPlayerBlinded(StatusEffectReceiver receiver)
        {
            Track(() => receiver != null ? receiver.RemainingSeconds(StatusEffectKinds.Blind) : 0f);
        }

        private void Apply()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            if (Opacity <= 0f)
            {
                element.style.display = DisplayStyle.None;
                return;
            }

            var catalog = Catalog;
            var color = catalog != null ? catalog.Color : Color.white;
            var image = catalog != null ? catalog.Image : null;
            if (image != null)
            {
                element.style.backgroundImage = new StyleBackground(image);
                element.style.unityBackgroundImageTintColor = color;
                element.style.backgroundColor = StyleKeyword.Null;
            }
            else
            {
                element.style.backgroundImage = StyleKeyword.Null;
                element.style.backgroundColor = color;
            }

            element.style.opacity = Opacity;
            element.style.display = DisplayStyle.Flex;
        }

        private void PlayRinging()
        {
            var catalog = Catalog;
            var clip = catalog != null ? catalog.Ringing : null;
            if (clip == null || !Application.isPlaying)
            {
                return;
            }

            if (_ringingSource == null)
            {
                _ringingSource = gameObject.AddComponent<AudioSource>();
                _ringingSource.playOnAwake = false;
                _ringingSource.spatialBlend = 0f;
            }

            _ringingSource.clip = clip;
            _ringingSource.volume = catalog.RingingVolume * AppliedControlSettings.Current.EffectsVolume;
            _ringingSource.Play();
        }

        private void StopRinging()
        {
            if (_ringingSource != null && _ringingSource.isPlaying)
            {
                _ringingSource.Stop();
            }
        }
    }
}
