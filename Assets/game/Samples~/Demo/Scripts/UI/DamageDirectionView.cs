using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class DamageDirectionView : HudElementView
    {
        public const string UxmlName = "damage-direction";

        public const string ArcClass = "damage-direction__arc";

        public const int MaxIndicators = 8;

        public const float ArcRadius = 220f;

        public const float ArcSpanDegrees = 40f;

        public const float ArcWidth = 12f;

        // 同じ攻撃元から続けて撃たれたときに弧を重ねず 1 本で出し直すための距離 (m)。動く相手の数発も 1 本にまとめる。
        private const float _sameSourceDistance = 1f;

        private readonly List<Indicator> _indicators = new List<Indicator>();

        public override string ElementName => UxmlName;

        // 未設定なら Camera.main を基準にするのは、オフラインの PlayerCamera もオンラインの ViewCamera も MainCamera だから。
        public Transform Viewer { get; set; }

        public int ActiveCount => _indicators.Count;

        private void OnEnable()
        {
            CombatEvents.DamageTaken += OnDamageTaken;
        }

        private void OnDisable()
        {
            CombatEvents.DamageTaken -= OnDamageTaken;
            Clear();
        }

        private void LateUpdate()
        {
            // カメラが動いた後に角度を見直すため LateUpdate。ポーズ中に弧が残り続けないよう、時間の止まらない経過で消す。
            Tick(Time.unscaledDeltaTime);
        }

        public void Show(Vector3 sourcePosition)
        {
            if (!IsAllowed || !IsFinite(sourcePosition))
            {
                return;
            }

            var indicator = FindNear(sourcePosition) ?? Allocate();
            indicator.Source = sourcePosition;
            indicator.Elapsed = 0f;
            Refresh();
        }

        public void Tick(float deltaSeconds)
        {
            if (_indicators.Count == 0)
            {
                return;
            }

            var step = deltaSeconds > 0f && !float.IsInfinity(deltaSeconds) ? deltaSeconds : 0f;
            for (var i = _indicators.Count - 1; i >= 0; i--)
            {
                var indicator = _indicators[i];
                indicator.Elapsed += step;
                if (indicator.Elapsed >= DamageDirection.DefaultSeconds)
                {
                    RemoveArc(indicator);
                    _indicators.RemoveAt(i);
                }
            }

            Refresh();
        }

        public void Clear()
        {
            foreach (var indicator in _indicators)
            {
                RemoveArc(indicator);
            }

            _indicators.Clear();
            Refresh();
        }

        public bool TryGetAngle(int index, out float degrees)
        {
            degrees = 0f;
            var viewer = ResolveViewer();
            return viewer != null && TryAngle(viewer, _indicators[index].Source, out degrees);
        }

        public float OpacityAt(int index)
        {
            return DamageDirection.Opacity(_indicators[index].Elapsed, DamageDirection.DefaultSeconds);
        }

        protected override void OnBound(VisualElement element)
        {
            // 前の木の弧は木と一緒に捨てられているので、新しい木に作り直す。
            foreach (var indicator in _indicators)
            {
                indicator.Arc = null;
            }

            Refresh();
        }

        protected override void OnAllowedChanged(bool allowed)
        {
            if (!allowed)
            {
                Clear();
            }
        }

        protected override void OnUnbinding(VisualElement element)
        {
            foreach (var indicator in _indicators)
            {
                RemoveArc(indicator);
            }
        }

        private void OnDamageTaken(DamageTakenInfo info)
        {
            if (info.SourcePosition.HasValue)
            {
                Show(info.SourcePosition.Value);
            }
        }

        private Indicator FindNear(Vector3 sourcePosition)
        {
            var limit = _sameSourceDistance * _sameSourceDistance;
            foreach (var indicator in _indicators)
            {
                if ((indicator.Source - sourcePosition).sqrMagnitude <= limit)
                {
                    return indicator;
                }
            }

            return null;
        }

        private Indicator Allocate()
        {
            if (_indicators.Count < MaxIndicators)
            {
                var created = new Indicator();
                _indicators.Add(created);
                return created;
            }

            var oldest = _indicators[0];
            foreach (var indicator in _indicators)
            {
                if (indicator.Elapsed > oldest.Elapsed)
                {
                    oldest = indicator;
                }
            }

            return oldest;
        }

        private void Refresh()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            element.style.display = _indicators.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            if (_indicators.Count == 0)
            {
                return;
            }

            var viewer = ResolveViewer();
            foreach (var indicator in _indicators)
            {
                var arc = indicator.Arc ?? (indicator.Arc = CreateArc(element));
                if (viewer == null || !TryAngle(viewer, indicator.Source, out var degrees))
                {
                    arc.style.display = DisplayStyle.None;
                    continue;
                }

                arc.style.display = DisplayStyle.Flex;
                arc.style.rotate = new Rotate(new Angle(degrees, AngleUnit.Degree));
                arc.style.opacity = DamageDirection.Opacity(indicator.Elapsed, DamageDirection.DefaultSeconds);
            }
        }

        private Transform ResolveViewer()
        {
            if (Viewer != null)
            {
                return Viewer;
            }

            var main = Camera.main;
            return main != null ? main.transform : null;
        }

        private static bool TryAngle(Transform viewer, Vector3 source, out float degrees)
        {
            return DamageDirection.TryScreenAngle(
                viewer.position.ToNumerics(), viewer.eulerAngles.y, source.ToNumerics(), out degrees);
        }

        // 弧は画面の真上に描いておき、要素ごと中央を軸に回して向きを出す (毎フレーム描き直さずに済む)。
        private static VisualElement CreateArc(VisualElement parent)
        {
            var arc = new VisualElement { pickingMode = PickingMode.Ignore };
            arc.AddToClassList(ArcClass);
            arc.generateVisualContent += DrawArc;
            parent.Add(arc);
            return arc;
        }

        private static void DrawArc(MeshGenerationContext context)
        {
            var element = context.visualElement;
            var painter = context.painter2D;
            var half = ArcSpanDegrees * 0.5f;
            painter.lineWidth = ArcWidth;
            painter.lineCap = LineCap.Round;
            painter.strokeColor = element.resolvedStyle.color;
            painter.BeginPath();
            // Painter2D の角度は +X を 0 とし、y が下向きの画面で時計回りなので、真上は -90°。
            painter.Arc(element.contentRect.center, ArcRadius, Angle.Degrees(-90f - half), Angle.Degrees(-90f + half));
            painter.Stroke();
        }

        private static void RemoveArc(Indicator indicator)
        {
            if (indicator.Arc == null)
            {
                return;
            }

            indicator.Arc.RemoveFromHierarchy();
            indicator.Arc = null;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private sealed class Indicator
        {
            public Vector3 Source;
            public float Elapsed;
            public VisualElement Arc;
        }
    }
}
