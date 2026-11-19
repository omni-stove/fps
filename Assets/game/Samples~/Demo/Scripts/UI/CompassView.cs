using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class CompassView : HudElementView
    {
        public const string UxmlName = "compass";

        public const string LabelClass = "compass__label";

        public const string PrimaryLabelClass = "compass__label--primary";

        public const string TickClass = "compass__tick";

        public const string MarkerClass = "compass__marker";

        public const string PingMarkerClass = "compass__marker--ping";

        public const string ObjectiveMarkerClass = "compass__marker--objective";

        public const string CenterClass = "compass__center";

        public const string CardinalKeyPrefix = "minimap.compass.";

        public const float DefaultSpanDegrees = 180f;

        public const float TickStepDegrees = 15f;

        public static readonly IReadOnlyList<string> CardinalSuffixes = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        private static readonly int _tickCount = Mathf.RoundToInt(360f / TickStepDegrees);

        private readonly List<Label> _labels = new List<Label>();
        private VisualElement _center;
        private readonly List<VisualElement> _ticks = new List<VisualElement>();
        private readonly List<VisualElement> _markerElements = new List<VisualElement>();
        private readonly List<Marker> _markers = new List<Marker>();
        private MinimapStatus? _status;
        private float _spanDegrees = DefaultSpanDegrees;
        private float _heading;
        private bool _subscribed;
        private bool _showsSignal;

        public override string ElementName => UxmlName;

        public MinimapStatus? Status => _status;

        public IReadOnlyList<Marker> Markers => _markers;

        public float HeadingDegrees => _heading;

        public float SpanDegrees
        {
            get => _spanDegrees;
            set
            {
                RequireSpan(value, nameof(value));
                _spanDegrees = value;
                Redraw();
            }
        }

        public static string CardinalKey(int index)
        {
            if (index < 0 || index >= CardinalSuffixes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "a compass point index must be 0 to 7");
            }

            return CardinalKeyPrefix + CardinalSuffixes[index];
        }

        public static float? BearingDegrees(System.Numerics.Vector3 from, System.Numerics.Vector3 to)
        {
            var east = to.X - from.X;
            var north = to.Z - from.Z;
            if (east * east + north * north < 1e-8f)
            {
                return null;
            }

            var degrees = Mathf.Atan2(east, north) * Mathf.Rad2Deg;
            if (degrees < 0f)
            {
                degrees += 360f;
            }

            return degrees >= 360f ? 0f : degrees;
        }

        public static float? BandPosition(float headingDegrees, float bearingDegrees, float spanDegrees)
        {
            RequireSpan(spanDegrees, nameof(spanDegrees));
            var delta = (bearingDegrees - headingDegrees) % 360f;
            if (delta >= 180f)
            {
                delta -= 360f;
            }
            else if (delta < -180f)
            {
                delta += 360f;
            }

            var half = spanDegrees * 0.5f;
            if (delta > half || delta < -half)
            {
                return null;
            }

            return 0.5f + delta / spanDegrees;
        }

        public bool TryGetCardinalPosition(int index, out float x)
        {
            x = 0f;
            if (!_status.HasValue || index < 0 || index >= CardinalSuffixes.Count)
            {
                return false;
            }

            var position = BandPosition(_heading, index * 45f, _spanDegrees);
            x = position ?? 0f;
            return position.HasValue;
        }

        public void Show(MinimapStatus? status)
        {
            _showsSignal = false;
            Write(status);
        }

        // 地図の知らせは間隔ごとにしか来ないので、その間も向きだけは毎フレーム読み直して帯を回す。
        public void FollowHeading()
        {
            if (_status.HasValue && !CurrentHeading(_status.Value).Equals(_heading))
            {
                Redraw();
            }
        }

        protected override void OnBound(VisualElement element)
        {
            // 前の木の要素は木と一緒に捨てられているので、新しい木に作り直す。
            _labels.Clear();
            _ticks.Clear();
            _markerElements.Clear();
            CreateBand(element);
            Subscribe();
            ApplyDensity(GameSignals.HudDensity.Value);

            var status = GameSignals.Minimap.Value;
            if (status.HasValue || _showsSignal)
            {
                ShowSignal(status);
                return;
            }

            Redraw();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            foreach (var label in _labels)
            {
                label.RemoveFromHierarchy();
            }

            foreach (var tick in _ticks)
            {
                tick.RemoveFromHierarchy();
            }

            foreach (var marker in _markerElements)
            {
                marker.RemoveFromHierarchy();
            }

            _labels.Clear();
            _ticks.Clear();
            _markerElements.Clear();
            _center?.RemoveFromHierarchy();
            _center = null;
            element.style.display = DisplayStyle.None;
        }

        protected override void OnLanguageChanged()
        {
            base.OnLanguageChanged();
            for (var i = 0; i < _labels.Count; i++)
            {
                _labels[i].text = GameText.Get(CardinalKey(i));
            }
        }

        private void Start()
        {
            // Hud.AddViews の後から手で付けたときは Hud が木を作り直すまで要素を受けないので、今の木から受ける。
            if (Element == null)
            {
                MinimapView.BindToDocument(this);
            }
        }

        private void LateUpdate()
        {
            FollowHeading();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void CreateBand(VisualElement parent)
        {
            for (var i = 0; i < _tickCount; i++)
            {
                // 方位の文字と重なると読みにくいので、文字の無い角度にだけ目盛りを置く。
                if (i * TickStepDegrees % 45f == 0f)
                {
                    continue;
                }

                var tick = new VisualElement { pickingMode = PickingMode.Ignore, userData = i * TickStepDegrees };
                tick.AddToClassList(TickClass);
                parent.Add(tick);
                _ticks.Add(tick);
            }

            for (var i = 0; i < CardinalSuffixes.Count; i++)
            {
                var label = new Label(GameText.Get(CardinalKey(i))) { pickingMode = PickingMode.Ignore };
                label.AddToClassList(LabelClass);
                label.EnableInClassList(PrimaryLabelClass, i % 2 == 0);
                parent.Add(label);
                _labels.Add(label);
            }

            _center = new VisualElement { pickingMode = PickingMode.Ignore };
            _center.AddToClassList(CenterClass);
            parent.Add(_center);
        }

        private void Write(MinimapStatus? status)
        {
            _status = status;
            Redraw();
        }

        private void ShowSignal(MinimapStatus? status)
        {
            _showsSignal = true;
            Write(status);
        }

        // Show で手渡された値は渡した側が向きまで決めているので、今のプレイヤーの向きで上書きしない。
        private float CurrentHeading(MinimapStatus status)
        {
            if (!_showsSignal)
            {
                return status.SelfHeadingDegrees;
            }

            var source = GameSignals.LocalHeadingDegrees.Value;
            if (source != null)
            {
                var degrees = source();
                return float.IsNaN(degrees) || float.IsInfinity(degrees) ? status.SelfHeadingDegrees : Mathf.Repeat(degrees, 360f);
            }

            var player = GameSignals.LocalPlayer.Value?.Player;
            if (player != null && MinimapProjection.TryHeadingDegrees(player.transform.forward.ToNumerics(), out var heading))
            {
                return heading;
            }

            return status.SelfHeadingDegrees;
        }

        private void Redraw()
        {
            _markers.Clear();
            if (_status.HasValue)
            {
                var status = _status.Value;
                _heading = CurrentHeading(status);
                foreach (var contact in status.Contacts)
                {
                    if (contact.Kind != MinimapContactKind.Ping && contact.Kind != MinimapContactKind.Objective)
                    {
                        continue;
                    }

                    var bearing = BearingDegrees(status.SelfPosition, contact.Position);
                    var x = bearing.HasValue ? BandPosition(_heading, bearing.Value, _spanDegrees) : null;
                    if (x.HasValue)
                    {
                        _markers.Add(new Marker(contact.Kind, contact.Team, x.Value));
                    }
                }
            }

            var parent = Element;
            if (parent == null)
            {
                return;
            }

            parent.style.display = _status.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (!_status.HasValue)
            {
                return;
            }

            var heading = _heading;
            foreach (var tick in _ticks)
            {
                Place(tick, BandPosition(heading, (float)tick.userData, _spanDegrees));
            }

            for (var i = 0; i < _labels.Count; i++)
            {
                Place(_labels[i], BandPosition(heading, i * 45f, _spanDegrees));
            }

            while (_markerElements.Count > _markers.Count)
            {
                var last = _markerElements.Count - 1;
                _markerElements[last].RemoveFromHierarchy();
                _markerElements.RemoveAt(last);
            }

            var palette = TeamColors.Current;
            var viewer = GameSignals.PaletteViewer.Value;
            for (var i = 0; i < _markers.Count; i++)
            {
                if (i == _markerElements.Count)
                {
                    var created = new VisualElement { pickingMode = PickingMode.Ignore };
                    created.AddToClassList(MarkerClass);
                    parent.Add(created);
                    _markerElements.Add(created);
                }

                var marker = _markers[i];
                var element = _markerElements[i];
                element.EnableInClassList(PingMarkerClass, marker.Kind == MinimapContactKind.Ping);
                element.EnableInClassList(ObjectiveMarkerClass, marker.Kind == MinimapContactKind.Objective);
                element.style.backgroundColor = MinimapView.ColorOf(palette, marker.Kind, marker.Team, viewer);
                Place(element, marker.X);
            }
        }

        private static void Place(VisualElement element, float? x)
        {
            element.style.display = x.HasValue ? DisplayStyle.Flex : DisplayStyle.None;
            if (x.HasValue)
            {
                element.style.left = Length.Percent(x.Value * 100f);
            }
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.Minimap.Changed += HandleMinimapChanged;
            GameSignals.HudDensity.Changed += HandleDensityChanged;
            GameSignals.PaletteViewer.Changed += HandlePaletteViewerChanged;
            AppliedControlSettings.Changed += HandleSettingsChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.Minimap.Changed -= HandleMinimapChanged;
            GameSignals.HudDensity.Changed -= HandleDensityChanged;
            GameSignals.PaletteViewer.Changed -= HandlePaletteViewerChanged;
            AppliedControlSettings.Changed -= HandleSettingsChanged;
            _subscribed = false;
        }

        private void HandleMinimapChanged(MinimapStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                Unsubscribe();
                return;
            }

            ShowSignal(status);
        }

        private void HandleDensityChanged(HudDensity? density)
        {
            if (this == null)
            {
                Unsubscribe();
                return;
            }

            ApplyDensity(density);
        }

        private void ApplyDensity(HudDensity? density)
        {
            // 値が消されたときは今のままにする (Hud と同じく、戻す先が無いため)。
            if (density.HasValue)
            {
                SetAllowed(HudVisibility.For(density.Value).Compass);
            }
        }

        private void HandlePaletteViewerChanged(Team? viewer)
        {
            if (this == null)
            {
                Unsubscribe();
                return;
            }

            Redraw();
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            if (this == null)
            {
                Unsubscribe();
                return;
            }

            Redraw();
        }

        private static void RequireSpan(float spanDegrees, string paramName)
        {
            if (!(spanDegrees > 0f) || spanDegrees > 360f || float.IsInfinity(spanDegrees))
            {
                throw new ArgumentOutOfRangeException(paramName, spanDegrees, "the compass span must be above 0 and at most 360 degrees");
            }
        }

        public readonly struct Marker
        {
            public Marker(MinimapContactKind kind, Team? team, float x)
            {
                Kind = kind;
                Team = team;
                X = x;
            }

            public MinimapContactKind Kind { get; }

            public Team? Team { get; }

            public float X { get; }
        }
    }
}
