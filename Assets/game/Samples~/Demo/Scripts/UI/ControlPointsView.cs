using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ControlPointsView : HudElementView
    {
        public const string UxmlName = "net-control-points";

        public const string PointClass = "net-control-points__point";
        public const string LabelClass = "net-control-points__label";

        public const string ContestedClass = "net-control-points__point--contested";

        public const float RingWidth = 5f;

        public static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.45f);

        public const float ContestedAlpha = 0.9f;

        private readonly List<ControlPointStatus> _states = new List<ControlPointStatus>();
        private readonly List<string> _names = new List<string>();
        private readonly List<PointElement> _elements = new List<PointElement>();
        // 設定の色の組は PlayerPrefs から読むので、フィールドの初期化子では引けない (HUD が Hud の色で塗り直す)
        private Color _alphaColor = TeamColors.Of(TeamPalette.Standard, Team.Alpha, TeamColors.OfflineViewer);
        private Color _bravoColor = TeamColors.Of(TeamPalette.Standard, Team.Bravo, TeamColors.OfflineViewer);
        private Color _contestedColor = ContestedColorOf(TeamPalette.Standard);
        private bool _subscribed;

        public static Color ContestedColorOf(TeamPalette palette)
        {
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            var color = TeamColors.ToColor(palette.Contested);
            return new Color(color.r, color.g, color.b, ContestedAlpha);
        }

        public override string ElementName => UxmlName;

        public Color ContestedColor => _contestedColor;

        public IReadOnlyList<ControlPointStatus> Statuses => _states;

        public IReadOnlyList<string> PointNames => _names;

        public bool IsShown => _states.Count > 0;

        public void SetTeamColors(Color alpha, Color bravo)
        {
            _alphaColor = alpha;
            _bravoColor = bravo;
            for (var i = 0; i < _elements.Count; i++)
            {
                Apply(i);
            }
        }

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
            _contestedColor = ContestedColorOf(TeamColors.Current);
            // 拠点の数が同じだと Write は描き直さないので、今の木の要素を先に作る
            Rebuild();
            HandleStatusChanged(GameSignals.ModeStatus.Value);
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.ModeStatus.Changed += HandleStatusChanged;
                AppliedControlSettings.Changed += HandleSettingsChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.ModeStatus.Changed -= HandleStatusChanged;
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _subscribed = false;
            }
        }

        // チームの色は Hud が塗り直すが、Hud の色を決め打ちにしていても競合の色はパレットに従わせる
        private void HandleSettingsChanged(ControlSettings settings)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                return;
            }

            var contested = ContestedColorOf(TeamColors.For(settings.Palette));
            if (contested == _contestedColor)
            {
                return;
            }

            _contestedColor = contested;
            for (var i = 0; i < _elements.Count; i++)
            {
                Apply(i);
            }
        }

        private void HandleStatusChanged(ModeStatus status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.ModeStatus.Changed -= HandleStatusChanged;
                return;
            }

            if (status == null || !(status.Detail is DominationStatus domination))
            {
                Clear();
                return;
            }

            Write(domination.Points);
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
            _elements[index].Show(state, ColorOf(state.Owner), ColorOf(state.LeaningTeam), _contestedColor);
        }

        private sealed class PointElement : VisualElement
        {
            private readonly Label _label;
            private float _fill;
            private Color _ringColor;
            private Color _contestedColor;
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

            public void Show(ControlPointStatus state, Color ownerColor, Color ringColor, Color contestedColor)
            {
                _label.style.color = ownerColor;
                _fill = state.Fill;
                _ringColor = ringColor;
                _contestedColor = contestedColor;
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

                painter.strokeColor = _contested ? _contestedColor : TrackColor;
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
