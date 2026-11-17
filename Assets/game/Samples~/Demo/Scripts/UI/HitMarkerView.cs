using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class HitMarkerView : HudElementView
    {
        public const string UxmlName = "hit-marker";

        public const string HeadClass = "hit-marker--head";

        public const string KillClass = "hit-marker--kill";

        public const string LineClass = "hit-marker__line";

        public const float DisplaySeconds = 0.15f;

        private float _remaining;
        private bool _headshot;
        private bool _killed;
        private PresentationCatalog _catalog;
        private bool _catalogResolved;
        private AudioSource _audio;

        public override string ElementName => UxmlName;

        public bool IsShowing => _remaining > 0f;

        public float Remaining => _remaining;

        public bool IsHeadshot => _headshot;

        public bool IsKill => _killed;

        public PresentationCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = PresentationCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
            }
        }

        private void OnEnable()
        {
            CombatEvents.HitConfirmed += Show;
        }

        private void OnDisable()
        {
            CombatEvents.HitConfirmed -= Show;
            _remaining = 0f;
            Apply();
        }

        private void Update()
        {
            // ポーズ中に出ていた印が残り続けないよう、時間の止まらない経過で消す。
            Tick(Time.unscaledDeltaTime);
        }

        public void Show(HitConfirm confirm)
        {
            // 音だけ鳴らすと、ハードコアで隠した命中の手応えが音で伝わってしまうため
            if (!IsAllowed)
            {
                return;
            }

            _remaining = DisplaySeconds;
            _headshot = confirm.Zone == HitZone.Head;
            _killed = confirm.Killed;
            Apply();
            PlaySound(confirm.Killed);
        }

        public void Tick(float deltaSeconds)
        {
            if (_remaining <= 0f || !(deltaSeconds > 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            _remaining -= deltaSeconds;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                Apply();
            }
        }

        protected override void OnBound(VisualElement element)
        {
            Apply();
        }

        protected override void OnAllowedChanged(bool allowed)
        {
            if (!allowed)
            {
                _remaining = 0f;
                Apply();
            }
        }

        private void Apply()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            element.EnableInClassList(HeadClass, _headshot);
            element.EnableInClassList(KillClass, _killed);
            ApplyLineColor(element);
            element.style.display = IsShowing ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void ApplyLineColor(VisualElement element)
        {
            // 消すときに設定を読みに行かない
            if (_headshot && !IsShowing)
            {
                return;
            }

            StyleColor color = _headshot ? new StyleColor(TeamColors.Highlight) : new StyleColor(StyleKeyword.Null);
            element.Query<VisualElement>(className: LineClass).ForEach(line => line.style.backgroundColor = color);
        }

        private void PlaySound(bool killed)
        {
            var catalog = Catalog;
            if (catalog == null)
            {
                return;
            }

            var clip = catalog.HitMarkerSoundFor(killed);
            if (clip == null)
            {
                return;
            }

            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
            }

            _audio.PlayOneShot(clip, catalog.HitMarkerVolume * AppliedControlSettings.Current.EffectsVolume);
        }
    }
}
