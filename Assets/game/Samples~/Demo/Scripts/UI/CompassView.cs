using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    /// <summary>
    /// ミニマップと同じ値 (<see cref="GameSignals.Minimap"/>) から、HUD の上の帯に方位とピン・目標の向きを出す既定の表示。
    /// Hud.uxml の <see cref="UxmlName"/> を使い、見た目は Minimap.uss で決める。
    /// 帯の中央が自分の向きで、左右の端までで <see cref="SpanDegrees"/> を見せる (<see cref="BandPosition"/>)。
    /// 方位の文字は Localization の minimap.json (<see cref="CardinalKey"/>) から引き、言語が変わったら書き直す。
    /// HUD の量が <see cref="HudDensity.Full"/> でなければミニマップと一緒に隠す (<see cref="MinimapView.AllowedAt"/>)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompassView : HudElementView
    {
        /// <summary>Hud.uxml の帯の要素。</summary>
        public const string UxmlName = "compass";

        /// <summary>方位の文字の Label に付けるクラス。</summary>
        public const string LabelClass = "compass__label";

        /// <summary>東西南北 (8 方位のうち 90 度ごと) の文字に足すクラス。</summary>
        public const string PrimaryLabelClass = "compass__label--primary";

        /// <summary>目盛りに付けるクラス。</summary>
        public const string TickClass = "compass__tick";

        /// <summary>ピン・目標の印に付けるクラス。</summary>
        public const string MarkerClass = "compass__marker";

        public const string PingMarkerClass = "compass__marker--ping";

        public const string ObjectiveMarkerClass = "compass__marker--objective";

        /// <summary>帯の中央 (自分の向き) の印に付けるクラス。</summary>
        public const string CenterClass = "compass__center";

        /// <summary>方位の文字のキーの頭。後ろに <see cref="CardinalSuffixes"/> の 1 つが付く。</summary>
        public const string CardinalKeyPrefix = "minimap.compass.";

        /// <summary><see cref="SpanDegrees"/> の既定 (度)。</summary>
        public const float DefaultSpanDegrees = 180f;

        /// <summary>目盛りの間隔 (度)。</summary>
        public const float TickStepDegrees = 15f;

        /// <summary>北から時計回りに 45 度ごとの方位のキーの後ろ。</summary>
        public static readonly IReadOnlyList<string> CardinalSuffixes = new[] { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };

        private static readonly int _tickCount = Mathf.RoundToInt(360f / TickStepDegrees);

        private readonly List<Label> _labels = new List<Label>();
        private VisualElement _center;
        private readonly List<VisualElement> _ticks = new List<VisualElement>();
        private readonly List<VisualElement> _markerElements = new List<VisualElement>();
        private readonly List<Marker> _markers = new List<Marker>();
        private MinimapStatus? _status;
        private float _spanDegrees = DefaultSpanDegrees;
        private bool _subscribed;
        private bool _showsSignal;

        public override string ElementName => UxmlName;

        /// <summary>出している値。出していなければ null。</summary>
        public MinimapStatus? Status => _status;

        /// <summary>帯に出しているピン・目標の印 (帯の外のものは除く)。</summary>
        public IReadOnlyList<Marker> Markers => _markers;

        /// <summary>帯の左端から右端までに見せる角度 (度)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">0 以下か 360 より大きいか有限でない。</exception>
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

        /// <summary>北から時計回りに index 番目 (45 度ごと) の方位の文字のキー。</summary>
        /// <exception cref="ArgumentOutOfRangeException">index が 0 から 7 の外。</exception>
        public static string CardinalKey(int index)
        {
            if (index < 0 || index >= CardinalSuffixes.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "a compass point index must be 0 to 7");
            }

            return CardinalKeyPrefix + CardinalSuffixes[index];
        }

        /// <summary>from から to への向き (度、北 (+Z) を 0 に時計回り、0 以上 360 未満)。上から見て同じ位置なら null。</summary>
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

        /// <summary>
        /// headingDegrees を向いているとき、bearingDegrees の向きを帯のどこに出すか。左端 0〜右端 1 で中央が 0.5。
        /// 左右に spanDegrees の半分より外れていれば null (端ちょうどは出す)。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">spanDegrees が 0 以下か 360 より大きいか有限でない。</exception>
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

        /// <summary>今出している方位の文字 index (<see cref="CardinalKey"/>) の帯の位置。帯の外か、出していなければ false。</summary>
        public bool TryGetCardinalPosition(int index, out float x)
        {
            x = 0f;
            if (!_status.HasValue || index < 0 || index >= CardinalSuffixes.Count)
            {
                return false;
            }

            var position = BandPosition(_status.Value.SelfHeadingDegrees, index * 45f, _spanDegrees);
            x = position ?? 0f;
            return position.HasValue;
        }

        /// <summary>status を出す。null なら出すのをやめる。要素を持っている間は <see cref="GameSignals.Minimap"/> が出されるたびに呼ばれる。</summary>
        public void Show(MinimapStatus? status)
        {
            _showsSignal = false;
            Write(status);
        }

        protected override void OnBound(VisualElement element)
        {
            // 前の木の要素は木と一緒に捨てられているので、新しい木に作り直す。
            _labels.Clear();
            _ticks.Clear();
            _markerElements.Clear();
            CreateBand(element);
            Subscribe();
            var density = GameSignals.HudDensity.Value;
            if (density.HasValue)
            {
                SetAllowed(MinimapView.AllowedAt(density.Value));
            }

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
            Write(status);
            _showsSignal = true;
        }

        private void Redraw()
        {
            _markers.Clear();
            if (_status.HasValue)
            {
                var status = _status.Value;
                foreach (var contact in status.Contacts)
                {
                    if (contact.Kind != MinimapContactKind.Ping && contact.Kind != MinimapContactKind.Objective)
                    {
                        continue;
                    }

                    var bearing = BearingDegrees(status.SelfPosition, contact.Position);
                    var x = bearing.HasValue ? BandPosition(status.SelfHeadingDegrees, bearing.Value, _spanDegrees) : null;
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

            var heading = _status.Value.SelfHeadingDegrees;
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

            // 値が消されたときは今のままにする (Hud と同じく、戻す先が無いため)。
            if (density.HasValue)
            {
                SetAllowed(MinimapView.AllowedAt(density.Value));
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

        /// <summary>帯に出したピン・目標の印 1 つ。</summary>
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

            /// <summary>帯の左端 0〜右端 1。中央が 0.5。</summary>
            public float X { get; }
        }
    }
}
