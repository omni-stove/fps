using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    /// <summary>
    /// 拠点制圧の、拠点ごとの状態を HUD に出す。拠点を番号の順に横へ並べ、それぞれを丸い枠で描く。
    /// 枠の中の呼び名 (A, B, C ...) は持ち主のチームの色 (中立なら <see cref="TeamColors.Neutral"/>) で塗り、
    /// 枠に沿うリングは進みが寄っている側のチームの色で、進み (<see cref="ControlPointStatus.Fill"/>) の分だけ上から時計回りに描く。
    /// 両チームがいて進みが止まっている拠点は、リングの下地を <see cref="ContestedColor"/> にする。
    /// Hud.uxml の <see cref="UxmlName"/> を使う。要素を持っている間は <see cref="GameSignals.Domination"/> を購読して拠点の状態を出し、消されたら隠す。
    /// HUD が上の欄にチームの点数を出している (<see cref="Hud.ShowsTeamScores"/>、ネット対戦) 間だけ出し、出していなければ <see cref="DominationView"/> に任せる。
    /// </summary>
    /// <remarks>
    /// 状態は Label が無い (UIDocument の木がまだ無い) ときも <see cref="Statuses"/> と <see cref="PointNames"/> で読める。
    /// 拠点の数が変わったときだけ要素を作り直し、状態が変わった拠点だけ描き直す。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class ControlPointsView : HudElementView
    {
        /// <summary>Hud.uxml のオンラインの拠点の要素。</summary>
        public const string UxmlName = "net-control-points";

        public const string PointClass = "net-control-points__point";
        public const string LabelClass = "net-control-points__label";

        /// <summary>両チームがいて占領が止まっている拠点に足す USS クラス。</summary>
        public const string ContestedClass = "net-control-points__point--contested";

        /// <summary>リングの太さ (px)。</summary>
        public const float RingWidth = 5f;

        /// <summary>進んでいない部分のリングの色。</summary>
        public static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.45f);

        /// <summary>押し合いになっている拠点のリングの下地の色。</summary>
        public static readonly Color ContestedColor = new Color(1f, 0.82f, 0.31f, 0.9f);

        private readonly List<ControlPointStatus> _states = new List<ControlPointStatus>();
        private readonly List<string> _names = new List<string>();
        private readonly List<PointElement> _elements = new List<PointElement>();
        // 設定の色の組は PlayerPrefs から読むので、フィールドの初期化子では引けない (HUD が Hud の色で塗り直す)
        private Color _alphaColor = TeamColors.Of(TeamPalette.Standard, Team.Alpha, TeamColors.OfflineViewer);
        private Color _bravoColor = TeamColors.Of(TeamPalette.Standard, Team.Bravo, TeamColors.OfflineViewer);
        private bool _subscribed;


        public override string ElementName => UxmlName;

        /// <summary>出している拠点の状態 (番号の順)。出していなければ空。</summary>
        public IReadOnlyList<ControlPointStatus> Statuses => _states;

        /// <summary>出している拠点の呼び名 (番号の順)。</summary>
        public IReadOnlyList<string> PointNames => _names;

        /// <summary>拠点を 1 つ以上出していれば true。</summary>
        public bool IsShown => _states.Count > 0;

        /// <summary>持ち主とリングを塗るチームの色。HUD の点数の色に合わせる。出している拠点も塗り直す。</summary>
        public void SetTeamColors(Color alpha, Color bravo)
        {
            _alphaColor = alpha;
            _bravoColor = bravo;
            for (var i = 0; i < _elements.Count; i++)
            {
                Apply(i);
            }
        }

        /// <summary>team の色。null なら中立の色。</summary>
        public Color ColorOf(Team? team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return _alphaColor;
                case Team.Bravo:
                    return _bravoColor;
                default:
                    return TeamColors.Neutral;
            }
        }

        private void Write(IReadOnlyList<ControlPointStatus> states)
        {
            if (states.Count != _states.Count)
            {
                _states.Clear();
                _names.Clear();
                for (var i = 0; i < states.Count; i++)
                {
                    _states.Add(states[i]);
                    _names.Add(DominationHudFormat.PointName(i));
                }

                Rebuild();
                return;
            }

            for (var i = 0; i < states.Count; i++)
            {
                if (_states[i].Equals(states[i]))
                {
                    continue;
                }

                _states[i] = states[i];
                Apply(i);
            }
        }

        private void Clear()
        {
            if (_states.Count == 0)
            {
                return;
            }

            _states.Clear();
            _names.Clear();
            Rebuild();
        }

        protected override void OnBound(VisualElement element)
        {
            Subscribe();
            // 拠点の数が同じだと Write は描き直さないので、今の木の要素を先に作る
            Rebuild();
            HandleStatusChanged(GameSignals.Domination.Value);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.Domination.Changed += HandleStatusChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.Domination.Changed -= HandleStatusChanged;
                _subscribed = false;
            }
        }

        private void HandleStatusChanged(DominationStatus status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Domination.Changed -= HandleStatusChanged;
                return;
            }

            var hud = GetComponent<Hud>();
            if (status == null || hud == null || !hud.ShowsTeamScores)
            {
                Clear();
                return;
            }

            Write(status.Points);
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.Clear();
            element.style.display = DisplayStyle.None;
            _elements.Clear();
        }

        // 拠点の数はシーンで決まり、変わるのは試合の始めと木の作り直しのときだけなので、差分を取らずに作り直す。
        private void Rebuild()
        {
            _elements.Clear();
            var element = Element;
            if (element == null)
            {
                return;
            }

            element.Clear();
            if (_states.Count == 0)
            {
                element.style.display = DisplayStyle.None;
                return;
            }

            for (var i = 0; i < _states.Count; i++)
            {
                var point = new PointElement(_names[i]);
                _elements.Add(point);
                element.Add(point);
                Apply(i);
            }

            element.style.display = DisplayStyle.Flex;
        }

        private void Apply(int index)
        {
            if (index >= _elements.Count)
            {
                return;
            }

            var state = _states[index];
            _elements[index].Show(state, ColorOf(state.Owner), ColorOf(state.LeaningTeam));
        }

        /// <summary>拠点 1 つ。丸い枠のリングを描き、中に呼び名を出す。</summary>
        private sealed class PointElement : VisualElement
        {
            private readonly Label _label;
            private float _fill;
            private Color _ringColor;
            private bool _contested;

            public PointElement(string pointName)
            {
                pickingMode = PickingMode.Ignore;
                AddToClassList(PointClass);
                _label = new Label(pointName) { pickingMode = PickingMode.Ignore };
                _label.AddToClassList(LabelClass);
                Add(_label);
                generateVisualContent += Draw;
            }

            public void Show(ControlPointStatus state, Color ownerColor, Color ringColor)
            {
                _label.style.color = ownerColor;
                _fill = state.Fill;
                _ringColor = ringColor;
                _contested = state.Contested;
                EnableInClassList(ContestedClass, _contested);
                MarkDirtyRepaint();
            }

            private void Draw(MeshGenerationContext context)
            {
                var rect = contentRect;
                var radius = (Mathf.Min(rect.width, rect.height) - RingWidth) * 0.5f;
                if (!(radius > 0f))
                {
                    return;
                }

                var painter = context.painter2D;
                painter.lineWidth = RingWidth;
                painter.lineCap = LineCap.Butt;

                painter.strokeColor = _contested ? ContestedColor : TrackColor;
                painter.BeginPath();
                painter.Arc(rect.center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
                painter.Stroke();

                if (_fill <= 0f)
                {
                    return;
                }

                painter.strokeColor = _ringColor;
                painter.BeginPath();
                painter.Arc(rect.center, radius, Angle.Degrees(-90f), Angle.Degrees(-90f + (360f * Mathf.Clamp01(_fill))));
                painter.Stroke();
            }
        }
    }
}
