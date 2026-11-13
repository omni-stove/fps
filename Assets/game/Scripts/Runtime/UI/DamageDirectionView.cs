using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 撃たれた方向を画面に出すビュー。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// <see cref="CombatEvents.DamageTaken"/> を受けるたびに、攻撃元の方向を画面の中央を囲む円周上の弧で出し、
    /// <see cref="DamageDirection.DefaultSeconds"/> 秒かけて薄くして消す。
    /// 角度は <see cref="DamageDirection.TryScreenAngle"/> で、毎フレーム今のカメラ (<see cref="Viewer"/>) から見直すので、
    /// 振り向くと弧も回る。攻撃元が分からない被弾 (位置が null) には何も出さない。
    /// オフラインとオンラインで同じ HUD (<see cref="Hud.AddViews"/>) に付き、どちらも同じイベントを受ける。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageDirectionView : HudElementView
    {
        /// <summary>Hud.uxml の被弾方向の要素。</summary>
        public const string UxmlName = "damage-direction";

        /// <summary>弧 1 つの要素に付けるクラス (色は Hud.uss の color で決める)。</summary>
        public const string ArcClass = "damage-direction__arc";

        /// <summary>同時に出す弧の数の上限。超えたら最も古い弧を使い回す。</summary>
        public const int MaxIndicators = 8;

        /// <summary>弧の半径 (HUD の基準解像度での px)。</summary>
        public const float ArcRadius = 220f;

        /// <summary>弧の開き (度)。</summary>
        public const float ArcSpanDegrees = 40f;

        /// <summary>弧の線の太さ (HUD の基準解像度での px)。</summary>
        public const float ArcWidth = 12f;

        // 同じ攻撃元から続けて撃たれたときに弧を重ねず 1 本で出し直すための距離 (m)。動く相手の数発も 1 本にまとめる。
        private const float _sameSourceDistance = 1f;

        private readonly List<Indicator> _indicators = new List<Indicator>();

        public override string ElementName => UxmlName;

        /// <summary>
        /// 向きの基準にするカメラ。null なら <see cref="Camera.main"/> を使う (オフラインの PlayerCamera とオンラインの ViewCamera は MainCamera)。
        /// </summary>
        public Transform Viewer { get; set; }

        /// <summary>今出している弧の数。</summary>
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

        /// <summary>sourcePosition から撃たれた弧を出す。近い攻撃元の弧が既にあれば、それを出し直す。</summary>
        public void Show(Vector3 sourcePosition)
        {
            if (!IsFinite(sourcePosition))
            {
                return;
            }

            var indicator = FindNear(sourcePosition) ?? Allocate();
            indicator.Source = sourcePosition;
            indicator.Elapsed = 0f;
            Refresh();
        }

        /// <summary>deltaSeconds 秒進め、出し終えた弧を消して残りの角度と濃さを直す。負か有限でない値では進めずに直すだけ。</summary>
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

        /// <summary>弧をすべて消す。</summary>
        public void Clear()
        {
            foreach (var indicator in _indicators)
            {
                RemoveArc(indicator);
            }

            _indicators.Clear();
            Refresh();
        }

        /// <summary>
        /// index 番目の弧の、今のカメラから見た角度 (画面の上から時計回りの度)。カメラが無いか向きが決まらなければ false。
        /// </summary>
        public bool TryGetAngle(int index, out float degrees)
        {
            degrees = 0f;
            var viewer = ResolveViewer();
            return viewer != null && TryAngle(viewer, _indicators[index].Source, out degrees);
        }

        /// <summary>index 番目の弧の今の不透明度 ([0, 1])。</summary>
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
