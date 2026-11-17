using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    /// <summary>
    /// ミニマップ (<see cref="GameSignals.Minimap"/>) を HUD の右上に丸い地図で出す既定の表示。Hud.uxml の <see cref="UxmlName"/> を使い、見た目は Minimap.uss で決める。
    /// 自分を中心に、<see cref="RangeMeters"/> までを円の半径に収め、外の印は縁に寄せる (<see cref="Place"/>)。
    /// <see cref="RotatesWithHeading"/> なら自分の向きを上に、そうでなければ北 (+Z) を上にする。
    /// 印は種類ごとに形を変え (味方は向きの三角・敵はひし形・ピンは丸・目標は四角)、色は <see cref="TeamColors"/> の組と
    /// <see cref="GameSignals.PaletteViewer"/> の視点で塗る (<see cref="ColorOf"/>)。
    /// 要素を持っている間は <see cref="GameSignals.Minimap"/>・<see cref="GameSignals.HudDensity"/>・<see cref="GameSignals.PaletteViewer"/> と設定の色の組を購読し、
    /// HUD の量でミニマップを出さない (<see cref="HudVisibility.Minimap"/>) なら隠す。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MinimapView : HudElementView
    {
        /// <summary>Hud.uxml の地図の要素。</summary>
        public const string UxmlName = "minimap";

        /// <summary>印 1 つの要素に付けるクラス。</summary>
        public const string ContactClass = "minimap__contact";

        public const string AllyClass = "minimap__contact--ally";

        public const string EnemyClass = "minimap__contact--enemy";

        public const string PingClass = "minimap__contact--ping";

        public const string ObjectiveClass = "minimap__contact--objective";

        /// <summary>半径の外にあって縁に寄せた印に足すクラス。</summary>
        public const string EdgeClass = "minimap__contact--edge";

        /// <summary>中心の自分の印に付けるクラス。</summary>
        public const string SelfClass = "minimap__self";

        /// <summary><see cref="RangeMeters"/> の既定 (m)。</summary>
        public const float DefaultRangeMeters = 50f;

        /// <summary><see cref="RotatesWithHeading"/> の既定。</summary>
        public const bool DefaultRotatesWithHeading = true;

        private readonly List<Mark> _marks = new List<Mark>();
        private readonly List<VisualElement> _markElements = new List<VisualElement>();
        private MinimapStatus? _status;
        private VisualElement _self;
        private float _rangeMeters = DefaultRangeMeters;
        private bool _rotatesWithHeading = DefaultRotatesWithHeading;
        private bool _subscribed;

        // 出している値が GameSignals から来たか。来ていれば、要素を持っていない間に値が消されたことを、受けたときに消して合わせる
        private bool _showsSignal;

        public override string ElementName => UxmlName;

        /// <summary>出している値。出していなければ null。</summary>
        public MinimapStatus? Status => _status;

        /// <summary>出している印 (<see cref="MinimapStatus.Contacts"/> と同じ並び)。出していなければ空。</summary>
        public IReadOnlyList<Mark> Marks => _marks;

        /// <summary>円の縁までの距離 (m)。ゲームの調整値 (minimap.json) から入れる。</summary>
        /// <exception cref="ArgumentOutOfRangeException">0 以下か有限でない。</exception>
        public float RangeMeters
        {
            get => _rangeMeters;
            set
            {
                RequireRange(value, nameof(value));
                _rangeMeters = value;
                Redraw();
            }
        }

        /// <summary>自分の向きを上にして地図を回すか。false なら北を上にする。</summary>
        public bool RotatesWithHeading
        {
            get => _rotatesWithHeading;
            set
            {
                _rotatesWithHeading = value;
                Redraw();
            }
        }

        /// <summary>
        /// status の自分から見た contact を地図の上に置く。位置は円の外接矩形を 1 とした量で、x は左 0〜右 1、y は上 0〜下 1、中心が 0.5。
        /// rangeMeters より遠い印は、向きを保ったまま縁 (中心から 0.5) に寄せる。高さ (Y) は見ない。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">rangeMeters が 0 以下か有限でない。</exception>
        public static Mark Place(MinimapStatus status, MinimapContact contact, float rangeMeters, bool rotatesWithHeading)
        {
            RequireRange(rangeMeters, nameof(rangeMeters));
            var up = rotatesWithHeading ? status.SelfHeadingDegrees : 0f;
            var radians = up * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            var east = contact.Position.X - status.SelfPosition.X;
            var north = contact.Position.Z - status.SelfPosition.Z;
            var right = (east * cos - north * sin) / rangeMeters;
            var forward = (east * sin + north * cos) / rangeMeters;
            var length = Mathf.Sqrt(right * right + forward * forward);
            var atEdge = length > 1f;
            if (atEdge)
            {
                right /= length;
                forward /= length;
            }

            return new Mark(
                contact.Kind,
                contact.Team,
                0.5f + 0.5f * right,
                0.5f - 0.5f * forward,
                WrapDegrees(contact.HeadingDegrees - up),
                atEdge);
        }

        /// <summary>
        /// palette で、viewer のチームから見た kind / team の印の色。チームがあれば <see cref="TeamColors.Of(TeamPalette, Team?, Team?)"/>、
        /// 無ければ種類で決める (味方は味方の色・敵は敵の色・目標は中立)。ピンはチームによらず注意を引く色。
        /// </summary>
        /// <exception cref="ArgumentNullException">palette が null。</exception>
        /// <exception cref="ArgumentOutOfRangeException">kind が定義済みの値でない。</exception>
        public static Color ColorOf(TeamPalette palette, MinimapContactKind kind, Team? team, Team? viewer)
        {
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            switch (kind)
            {
                case MinimapContactKind.Ping:
                    return TeamColors.ToColor(palette.Highlight);
                case MinimapContactKind.Ally:
                    return team.HasValue ? TeamColors.Of(palette, team, viewer) : TeamColors.ToColor(palette.Friendly);
                case MinimapContactKind.Enemy:
                    return team.HasValue ? TeamColors.Of(palette, team, viewer) : TeamColors.ToColor(palette.Enemy);
                case MinimapContactKind.Objective:
                    return TeamColors.Of(palette, team, viewer);
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown minimap contact kind");
            }
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
            _markElements.Clear();
            _self = null;
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
            foreach (var mark in _markElements)
            {
                mark.RemoveFromHierarchy();
            }

            _markElements.Clear();
            _self?.RemoveFromHierarchy();
            _self = null;
            element.style.display = DisplayStyle.None;
        }

        private void Start()
        {
            // Hud.AddViews の後から手で付けたときは Hud が木を作り直すまで要素を受けないので、今の木から受ける。
            if (Element == null)
            {
                BindToDocument(this);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        /// <summary>view が要素をまだ持っていなければ、同じ GameObject の UIDocument の今の木から受ける。木がまだ無ければ何もしない。</summary>
        internal static void BindToDocument(HudElementView view)
        {
            var document = view.GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root == null || root.childCount == 0)
            {
                return;
            }

            try
            {
                view.Bind(root);
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{Hud.LogTag} could not bind {view.GetType().Name}: {e.Message}", view);
            }
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
            _marks.Clear();
            if (_status.HasValue)
            {
                foreach (var contact in _status.Value.Contacts)
                {
                    _marks.Add(Place(_status.Value, contact, _rangeMeters, _rotatesWithHeading));
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

            if (_self == null)
            {
                _self = CreateShape(SelfClass, Shape.Arrow, Color.white);
                parent.Add(_self);
            }

            _self.style.rotate = new Rotate(new Angle(_rotatesWithHeading ? 0f : _status.Value.SelfHeadingDegrees, AngleUnit.Degree));

            while (_markElements.Count > _marks.Count)
            {
                var last = _markElements.Count - 1;
                _markElements[last].RemoveFromHierarchy();
                _markElements.RemoveAt(last);
            }

            var palette = TeamColors.Current;
            var viewer = GameSignals.PaletteViewer.Value;
            for (var i = 0; i < _marks.Count; i++)
            {
                var mark = _marks[i];
                if (i == _markElements.Count)
                {
                    var created = CreateShape(ContactClass, ShapeOf(mark.Kind), Color.white);
                    // 自分の印より後ろに描き、重なったときに自分が見えるようにする。
                    parent.Insert(parent.IndexOf(_self), created);
                    _markElements.Add(created);
                }

                ApplyMark(_markElements[i], mark, ColorOf(palette, mark.Kind, mark.Team, viewer));
            }
        }

        private static void ApplyMark(VisualElement element, Mark mark, Color color)
        {
            element.EnableInClassList(AllyClass, mark.Kind == MinimapContactKind.Ally);
            element.EnableInClassList(EnemyClass, mark.Kind == MinimapContactKind.Enemy);
            element.EnableInClassList(PingClass, mark.Kind == MinimapContactKind.Ping);
            element.EnableInClassList(ObjectiveClass, mark.Kind == MinimapContactKind.Objective);
            element.EnableInClassList(EdgeClass, mark.AtEdge);
            element.style.left = Length.Percent(mark.X * 100f);
            element.style.top = Length.Percent(mark.Y * 100f);

            // 向きに意味があるのは味方の三角だけなので、ほかの形は回さない。
            var rotation = mark.Kind == MinimapContactKind.Ally ? mark.RotationDegrees : 0f;
            element.style.rotate = new Rotate(new Angle(rotation, AngleUnit.Degree));
            SetShape(element, ShapeOf(mark.Kind), color);
        }

        private static Shape ShapeOf(MinimapContactKind kind)
        {
            switch (kind)
            {
                case MinimapContactKind.Ally:
                    return Shape.Arrow;
                case MinimapContactKind.Enemy:
                    return Shape.Diamond;
                case MinimapContactKind.Ping:
                    return Shape.Circle;
                default:
                    return Shape.Square;
            }
        }

        private static VisualElement CreateShape(string className, Shape shape, Color color)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            element.userData = new ShapeStyle { Shape = shape, Color = color };
            element.generateVisualContent += DrawShape;
            return element;
        }

        // style.color を変えても generateVisualContent は呼び直されないので、形と色は userData に持ち、変わったときだけ描き直させる。
        private static void SetShape(VisualElement element, Shape shape, Color color)
        {
            var style = (ShapeStyle)element.userData;
            if (style.Shape == shape && style.Color == color)
            {
                return;
            }

            style.Shape = shape;
            style.Color = color;
            element.MarkDirtyRepaint();
        }

        private static void DrawShape(MeshGenerationContext context)
        {
            var element = context.visualElement;
            if (!(element.userData is ShapeStyle style))
            {
                return;
            }

            var rect = element.contentRect;
            var painter = context.painter2D;
            painter.fillColor = style.Color;
            painter.strokeColor = new Color(0f, 0f, 0f, 0.7f);
            painter.lineWidth = 1.5f;
            painter.BeginPath();
            switch (style.Shape)
            {
                case Shape.Arrow:
                    painter.MoveTo(new Vector2(rect.center.x, rect.yMin));
                    painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                    painter.LineTo(new Vector2(rect.center.x, rect.yMax - rect.height * 0.3f));
                    painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                    break;
                case Shape.Diamond:
                    painter.MoveTo(new Vector2(rect.center.x, rect.yMin));
                    painter.LineTo(new Vector2(rect.xMax, rect.center.y));
                    painter.LineTo(new Vector2(rect.center.x, rect.yMax));
                    painter.LineTo(new Vector2(rect.xMin, rect.center.y));
                    break;
                case Shape.Circle:
                    painter.Arc(rect.center, Mathf.Min(rect.width, rect.height) * 0.5f, Angle.Degrees(0f), Angle.Degrees(360f));
                    break;
                default:
                    painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
                    painter.LineTo(new Vector2(rect.xMax, rect.yMin));
                    painter.LineTo(new Vector2(rect.xMax, rect.yMax));
                    painter.LineTo(new Vector2(rect.xMin, rect.yMax));
                    break;
            }

            painter.ClosePath();
            painter.Fill();
            painter.Stroke();
        }

        private void ApplyDensity(HudDensity? density)
        {
            // 値が消されたときは今のままにする (Hud と同じく、戻す先が無いため)。
            if (density.HasValue)
            {
                SetAllowed(HudVisibility.For(density.Value).Minimap);
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

        private static void RequireRange(float rangeMeters, string paramName)
        {
            if (!(rangeMeters > 0f) || float.IsInfinity(rangeMeters))
            {
                throw new ArgumentOutOfRangeException(paramName, rangeMeters, "the minimap range must be positive and finite");
            }
        }

        private static float WrapDegrees(float degrees)
        {
            var wrapped = degrees % 360f;
            if (wrapped < 0f)
            {
                wrapped += 360f;
            }

            // -1e-8 に 360 を足すと float では 360 に丸まるので、0 に寄せて範囲を 0 以上 360 未満に保つ。
            return wrapped >= 360f ? 0f : wrapped;
        }

        /// <summary>地図に置いた印 1 つ。</summary>
        public readonly struct Mark
        {
            public Mark(MinimapContactKind kind, Team? team, float x, float y, float rotationDegrees, bool atEdge)
            {
                Kind = kind;
                Team = team;
                X = x;
                Y = y;
                RotationDegrees = rotationDegrees;
                AtEdge = atEdge;
            }

            public MinimapContactKind Kind { get; }

            public Team? Team { get; }

            /// <summary>左 0〜右 1。中心が 0.5。</summary>
            public float X { get; }

            /// <summary>上 0〜下 1。中心が 0.5。</summary>
            public float Y { get; }

            /// <summary>地図の上での向き (度、上を 0 に時計回り)。</summary>
            public float RotationDegrees { get; }

            /// <summary>半径の外にあって縁に寄せたか。</summary>
            public bool AtEdge { get; }
        }

        private enum Shape
        {
            Arrow,
            Diamond,
            Circle,
            Square,
        }

        private sealed class ShapeStyle
        {
            public Shape Shape;
            public Color Color;
        }
    }
}
