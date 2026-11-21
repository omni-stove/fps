using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 文字の大きさと色は Hud.uss に任せ、ゲームが見た目を差し替えられるようにする。
    [DisallowMultipleComponent]
    public sealed class DamageNumberView : HudElementView
    {
        public const string UxmlName = "damage-numbers";

        public const string AnchorClass = "damage-number-anchor";

        public const string NumberClass = "damage-number";

        public const string HeadClass = "damage-number--head";

        public const string KillClass = "damage-number--kill";

        public const float CrosshairOffsetX = 28f;

        public const float CrosshairOffsetY = 20f;

        private readonly Dictionary<int, VisualElement> _anchors = new Dictionary<int, VisualElement>();
        private readonly HashSet<int> _live = new HashSet<int>();
        private readonly List<int> _stale = new List<int>();
        private DamageNumberCatalog _catalog;
        private bool _catalogResolved;
        private DamageNumberFeed _feed;

        public override string ElementName => UxmlName;

        public Camera Viewer { get; set; }

        public DamageNumberCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = DamageNumberCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
                _feed = null;
                RemoveAll();
            }
        }

        public bool IsEnabled
        {
            get
            {
                var catalog = Catalog;
                return catalog != null && catalog.Enabled;
            }
        }

        public IReadOnlyList<DamageNumber> Numbers => _feed != null ? _feed.Numbers : (IReadOnlyList<DamageNumber>)System.Array.Empty<DamageNumber>();

        private void OnEnable()
        {
            CombatEvents.HitConfirmed += Show;
        }

        private void OnDisable()
        {
            CombatEvents.HitConfirmed -= Show;
            ClearNumbers();
        }

        private void LateUpdate()
        {
            // ポーズ中に出ていた数値が残り続けないよう、時間の止まらない経過で消す。
            Tick(Time.unscaledDeltaTime);
        }

        public void Show(HitConfirm confirm)
        {
            if (!IsAllowed || !IsEnabled)
            {
                return;
            }

            var feed = Feed();
            if (feed == null || feed.Add(confirm) == 0)
            {
                return;
            }

            Apply();
        }

        public void Tick(float deltaSeconds)
        {
            if (_feed == null || (_feed.Numbers.Count == 0 && _anchors.Count == 0))
            {
                return;
            }

            _feed.Tick(deltaSeconds);
            Apply();
        }

        protected override void OnBound(VisualElement element)
        {
            _anchors.Clear();
            element.Clear();
            element.style.display = DisplayStyle.None;
            Apply();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            RemoveAll();
        }

        protected override void OnAllowedChanged(bool allowed)
        {
            if (!allowed)
            {
                ClearNumbers();
            }
        }

        private DamageNumberFeed Feed()
        {
            if (_feed != null)
            {
                return _feed;
            }

            var catalog = Catalog;
            if (catalog == null)
            {
                return null;
            }

            DamageNumberSettings settings;
            try
            {
                settings = catalog.ToSettings();
            }
            catch (System.ArgumentOutOfRangeException e)
            {
                Debug.LogWarning($"{Hud.LogTag} damage number catalog '{catalog.name}' is invalid; using the defaults: {e.Message}", this);
                settings = DamageNumberSettings.Default;
            }

            _feed = new DamageNumberFeed(settings);
            return _feed;
        }

        private void ClearNumbers()
        {
            _feed?.Clear();
            RemoveAll();
            if (Element != null)
            {
                Element.style.display = DisplayStyle.None;
            }
        }

        private void RemoveAll()
        {
            foreach (var anchor in _anchors.Values)
            {
                anchor.RemoveFromHierarchy();
            }

            _anchors.Clear();
        }

        private void Apply()
        {
            var parent = Element;
            if (parent == null || _feed == null)
            {
                return;
            }

            var camera = Viewer != null ? Viewer : Camera.main;
            _live.Clear();
            foreach (var number in _feed.Numbers)
            {
                _live.Add(number.Id);
                if (!_anchors.TryGetValue(number.Id, out var anchor))
                {
                    anchor = Create(parent, number);
                    _anchors.Add(number.Id, anchor);
                }

                Place(anchor, number, camera);
            }

            _stale.Clear();
            foreach (var id in _anchors.Keys)
            {
                if (!_live.Contains(id))
                {
                    _stale.Add(id);
                }
            }

            foreach (var id in _stale)
            {
                _anchors[id].RemoveFromHierarchy();
                _anchors.Remove(id);
            }

            parent.style.display = _anchors.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private static VisualElement Create(VisualElement parent, DamageNumber number)
        {
            var anchor = new VisualElement { pickingMode = PickingMode.Ignore };
            anchor.AddToClassList(AnchorClass);
            var label = new Label(number.Damage.ToString(CultureInfo.InvariantCulture)) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(NumberClass);
            label.EnableInClassList(HeadClass, number.Zone == HitZone.Head || number.Zone == HitZone.WeakPoint);
            label.EnableInClassList(KillClass, number.Killed);
            anchor.Add(label);
            parent.Add(anchor);
            return anchor;
        }

        private void Place(VisualElement anchor, DamageNumber number, Camera camera)
        {
            var rise = _feed.Rise(number);
            if (TryViewport(camera, number, out var viewport))
            {
                anchor.style.left = Length.Percent(viewport.x * 100f);
                anchor.style.top = Length.Percent((1f - viewport.y) * 100f);
                anchor.style.translate = new Translate(new Length(0f), new Length(-rise));
            }
            else
            {
                anchor.style.left = Length.Percent(50f);
                anchor.style.top = Length.Percent(50f);
                anchor.style.translate = new Translate(new Length(CrosshairOffsetX), new Length(-CrosshairOffsetY - rise));
            }

            anchor.style.opacity = _feed.Opacity(number);
        }

        private static bool TryViewport(Camera camera, DamageNumber number, out Vector3 viewport)
        {
            viewport = default;
            if (camera == null || !number.Point.HasValue)
            {
                return false;
            }

            viewport = camera.WorldToViewportPoint(number.Point.Value.ToUnity());
            return viewport.z > 0f
                && !float.IsNaN(viewport.x) && !float.IsInfinity(viewport.x)
                && !float.IsNaN(viewport.y) && !float.IsInfinity(viewport.y);
        }
    }
}
