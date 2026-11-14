using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 印 (<see cref="PingController.Markers"/>) を HUD に出す既定の表示。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// 毎フレーム今のカメラから見直し、画面の中の印はその位置に、外 (後ろも含む) の印は <see cref="PingScreen.Place"/> で端に寄せて向きの矢印を出す。
    /// 印が画面の中に入ったか端に寄ったときに <c>[Ping] screen</c> をログに出す。
    /// <see cref="Hud.AddViews"/> では付けず、オフラインのプレイヤーの <see cref="PingController"/> が <see cref="PingUi"/> の既定の生成役
    /// (<see cref="DefaultPingViewFactory"/>) で付ける。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PingMarkersView : HudElementView, IPingView
    {
        /// <summary>Hud.uxml の印を入れる要素。</summary>
        public const string UxmlName = "ping-markers";

        /// <summary>印 1 つの要素に付けるクラス。</summary>
        public const string MarkerClass = "ping-marker";

        /// <summary>敵の印に足すクラス。</summary>
        public const string EnemyClass = "ping-marker--enemy";

        /// <summary>場所の印に足すクラス。</summary>
        public const string LocationClass = "ping-marker--location";

        /// <summary>画面の外の印 (端に寄せて矢印を出す) に足すクラス。</summary>
        public const string OffScreenClass = "ping-marker--offscreen";

        /// <summary>印の形の要素に付けるクラス。</summary>
        public const string IconClass = "ping-marker__icon";

        /// <summary>画面の外の印の向きを出す矢印の要素に付けるクラス (色は color で決める)。</summary>
        public const string ArrowClass = "ping-marker__arrow";

        /// <summary>画面の外の印を寄せる端の余白。画面の高さを 1 とした量。</summary>
        public const float EdgeMargin = 0.06f;

        /// <summary>矢印の三角の高さ (HUD の基準解像度での px)。</summary>
        public const float ArrowLength = 10f;

        /// <summary>矢印の三角の底辺の半分 (HUD の基準解像度での px)。</summary>
        public const float ArrowHalfWidth = 7f;

        // カメラの縦横比が読めないときに使う。HUD の基準解像度 (1920x1080) の比。
        private const float _fallbackAspect = 16f / 9f;

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly List<int> _stale = new List<int>();
        private PingController _source;

        public override string ElementName => UxmlName;

        /// <summary>出している印の持ち主。出していなければ null。</summary>
        public PingController Source => _source;

        /// <summary>向きの基準にするカメラ。null なら持ち主のカメラ (<see cref="PingController.ViewCamera"/>)、それも無ければ <see cref="Camera.main"/>。</summary>
        public Camera Viewer { get; set; }

        /// <summary>今画面に出している (位置が決まった) 印の数。</summary>
        public int ShownCount
        {
            get
            {
                var count = 0;
                foreach (var entry in _entries.Values)
                {
                    count += entry.HasPlacement ? 1 : 0;
                }

                return count;
            }
        }

        /// <summary>
        /// hud に (無ければ) このビューを付けて返す。付けたばかりなら Hud に木から要素を渡し直させる。
        /// UXML に要素が無ければエラーをログに出し、要素を持たないまま返す (位置は <see cref="TryGetPlacement"/> で読める)。
        /// </summary>
        public static PingMarkersView AttachTo(Hud hud)
        {
            if (hud == null)
            {
                throw new ArgumentNullException(nameof(hud));
            }

            var view = hud.GetComponent<PingMarkersView>();
            if (view != null)
            {
                return view;
            }

            view = hud.gameObject.AddComponent<PingMarkersView>();
            try
            {
                hud.RebuildView();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{Hud.LogTag} could not show ping markers on the HUD: {e.Message}", hud);
            }

            return view;
        }

        public void Show(PingController source)
        {
            if (_source != null)
            {
                _source.Changed -= OnChanged;
            }

            _source = source;
            if (_source != null)
            {
                _source.Changed += OnChanged;
            }

            RemoveAll();
            Refresh();
        }

        /// <summary>印 id を今出している位置と向き。出していなければ false。</summary>
        public bool TryGetPlacement(int id, out PingScreenPlacement placement)
        {
            if (_entries.TryGetValue(id, out var entry) && entry.HasPlacement)
            {
                placement = entry.Placement;
                return true;
            }

            placement = default;
            return false;
        }

        /// <summary>持ち主の今の印を、今のカメラから見て置き直す。</summary>
        public void Refresh()
        {
            var markers = _source != null ? _source.Markers : (IReadOnlyList<PingMarker>)Array.Empty<PingMarker>();
            DropStale(markers);

            var camera = ResolveViewer();
            var aspect = camera != null && camera.aspect > 0f && !float.IsInfinity(camera.aspect) ? camera.aspect : _fallbackAspect;
            var margin = Mathf.Min(EdgeMargin, 0.49f * Mathf.Min(1f, aspect));
            foreach (var marker in markers)
            {
                if (!_entries.TryGetValue(marker.Id, out var entry))
                {
                    entry = new Entry { Kind = marker.Kind };
                    _entries.Add(marker.Id, entry);
                }

                if (camera == null || !TryPlace(camera, marker, aspect, margin, out var placement))
                {
                    entry.HasPlacement = false;
                    ApplyEntry(entry);
                    continue;
                }

                if (!entry.HasPlacement || entry.Placement.IsOnScreen != placement.IsOnScreen || entry.Placement.Edge != placement.Edge)
                {
                    Debug.Log(PingLog.Screen(marker.Id, placement), this);
                }

                entry.Placement = placement;
                entry.HasPlacement = true;
                ApplyEntry(entry);
            }

            if (Element != null)
            {
                Element.style.display = _entries.Count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        protected override void OnBound(VisualElement element)
        {
            // 前の木の要素は木と一緒に捨てられているので、新しい木に作り直す。
            foreach (var entry in _entries.Values)
            {
                entry.Root = null;
                entry.Arrow = null;
            }

            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            foreach (var entry in _entries.Values)
            {
                RemoveElements(entry);
            }

            element.style.display = DisplayStyle.None;
        }

        private void LateUpdate()
        {
            // 持ち主 (DefaultExecutionOrder 110) が印を動かし、カメラが動いた後に置き直す。
            Refresh();
        }

        private void OnDestroy()
        {
            if (_source != null)
            {
                _source.Changed -= OnChanged;
                _source = null;
            }
        }

        private void OnChanged(PingController source)
        {
            Refresh();
        }

        private Camera ResolveViewer()
        {
            if (Viewer != null)
            {
                return Viewer;
            }

            var own = _source != null ? _source.ViewCamera : null;
            return own != null ? own : Camera.main;
        }

        private static bool TryPlace(Camera camera, PingMarker marker, float aspect, float margin, out PingScreenPlacement placement)
        {
            var viewport = camera.WorldToViewportPoint(marker.Position.ToUnity());
            if (float.IsNaN(viewport.x) || float.IsInfinity(viewport.x)
                || float.IsNaN(viewport.y) || float.IsInfinity(viewport.y)
                || float.IsNaN(viewport.z) || float.IsInfinity(viewport.z))
            {
                placement = default;
                return false;
            }

            placement = PingScreen.Place(viewport.ToNumerics(), aspect, margin);
            return true;
        }

        private void DropStale(IReadOnlyList<PingMarker> markers)
        {
            _stale.Clear();
            foreach (var id in _entries.Keys)
            {
                var present = false;
                foreach (var marker in markers)
                {
                    present |= marker.Id == id;
                }

                if (!present)
                {
                    _stale.Add(id);
                }
            }

            foreach (var id in _stale)
            {
                RemoveElements(_entries[id]);
                _entries.Remove(id);
            }

            _stale.Clear();
        }

        private void RemoveAll()
        {
            foreach (var entry in _entries.Values)
            {
                RemoveElements(entry);
            }

            _entries.Clear();
        }

        private void ApplyEntry(Entry entry)
        {
            var parent = Element;
            if (parent == null)
            {
                return;
            }

            if (entry.Root == null)
            {
                CreateElements(parent, entry);
            }

            if (!entry.HasPlacement)
            {
                entry.Root.style.display = DisplayStyle.None;
                return;
            }

            var placement = entry.Placement;
            entry.Root.style.display = DisplayStyle.Flex;
            entry.Root.style.left = Length.Percent(placement.X * 100f);
            entry.Root.style.top = Length.Percent(placement.Y * 100f);
            entry.Root.EnableInClassList(OffScreenClass, !placement.IsOnScreen);
            entry.Arrow.style.display = placement.IsOnScreen ? DisplayStyle.None : DisplayStyle.Flex;
            entry.Arrow.style.rotate = new Rotate(new Angle(placement.AngleDegrees, AngleUnit.Degree));
        }

        private static void CreateElements(VisualElement parent, Entry entry)
        {
            var root = new VisualElement { pickingMode = PickingMode.Ignore };
            root.AddToClassList(MarkerClass);
            root.AddToClassList(entry.Kind == PingKind.Enemy ? EnemyClass : LocationClass);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(IconClass);
            root.Add(icon);

            // 矢印は要素の上に描いておき、要素ごと中央を軸に回して向きを出す (毎フレーム描き直さずに済む)。
            var arrow = new VisualElement { pickingMode = PickingMode.Ignore };
            arrow.AddToClassList(ArrowClass);
            arrow.generateVisualContent += DrawArrow;
            root.Add(arrow);

            parent.Add(root);
            entry.Root = root;
            entry.Arrow = arrow;
        }

        private static void DrawArrow(MeshGenerationContext context)
        {
            var element = context.visualElement;
            var rect = element.contentRect;
            var tipY = rect.yMin - ArrowLength;
            var painter = context.painter2D;
            painter.fillColor = element.resolvedStyle.color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.center.x, tipY));
            painter.LineTo(new Vector2(rect.center.x + ArrowHalfWidth, rect.yMin));
            painter.LineTo(new Vector2(rect.center.x - ArrowHalfWidth, rect.yMin));
            painter.ClosePath();
            painter.Fill();
        }

        private static void RemoveElements(Entry entry)
        {
            if (entry.Root != null)
            {
                entry.Root.RemoveFromHierarchy();
            }

            entry.Root = null;
            entry.Arrow = null;
        }

        private sealed class Entry
        {
            public PingKind Kind;
            public PingScreenPlacement Placement;
            public bool HasPlacement;
            public VisualElement Root;
            public VisualElement Arrow;
        }
    }
}
