using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PingMarkersView : HudElementView
    {
        public const string UxmlName = "ping-markers";

        public const string MarkerClass = "ping-marker";

        public const string EnemyClass = "ping-marker--enemy";

        public const string LocationClass = "ping-marker--location";

        public const string OffScreenClass = "ping-marker--offscreen";

        public const string IconClass = "ping-marker__icon";

        public const string ArrowClass = "ping-marker__arrow";

        public const float EdgeMargin = 0.06f;

        public const float ArrowLength = 10f;

        public const float ArrowHalfWidth = 7f;

        // カメラの縦横比が読めないときに使う。HUD の基準解像度 (1920x1080) の比。
        private const float _fallbackAspect = 16f / 9f;

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();
        private readonly List<int> _stale = new List<int>();
        private IReadOnlyList<PingMarker> _markers;
        private bool _subscribed;

        // 設定の色の組は PlayerPrefs から読むので、フィールドの初期化子では引けない (結び付けたときにパレットの色にする)
        private Color _enemyColor = EnemyColorOf(TeamPalette.Standard);

        // 出している印が GameSignals から来たか。来ていれば、要素を持っていない間に印が消されたことを、受けたときに消して合わせる
        private bool _showsSignal;

        public override string ElementName => UxmlName;

        public IReadOnlyList<PingMarker> Markers => _markers;

        public Color EnemyColor => _enemyColor;

        public static Color EnemyColorOf(TeamPalette palette)
        {
            if (palette == null)
            {
                throw new ArgumentNullException(nameof(palette));
            }

            return TeamColors.ToColor(palette.Enemy);
        }

        public Camera Viewer { get; set; }

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

        public void Show(IReadOnlyList<PingMarker> markers)
        {
            _showsSignal = false;
            Write(markers);
        }

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

        public void Refresh()
        {
            var markers = _markers ?? (IReadOnlyList<PingMarker>)Array.Empty<PingMarker>();
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
                entry.Icon = null;
                entry.Arrow = null;
            }

            _enemyColor = EnemyColorOf(TeamColors.Current);
            Subscribe();
            var markers = GameSignals.Pings.Value;
            if (markers != null || _showsSignal)
            {
                ShowSignal(markers);
                return;
            }

            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            foreach (var entry in _entries.Values)
            {
                RemoveElements(entry);
            }

            element.style.display = DisplayStyle.None;
        }

        private void LateUpdate()
        {
            // PingController (DefaultExecutionOrder 110) が印を動かし、カメラが動いた後に置き直す。
            Refresh();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Write(IReadOnlyList<PingMarker> markers)
        {
            if (!ReferenceEquals(markers, _markers))
            {
                RemoveAll();
                _markers = markers;
            }

            Refresh();
        }

        private void ShowSignal(IReadOnlyList<PingMarker> markers)
        {
            Write(markers);
            _showsSignal = true;
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.Pings.Changed += HandlePingsChanged;
                AppliedControlSettings.Changed += HandleSettingsChanged;
                _subscribed = true;
                Debug.Log(PingLog.View(GetType().Name), this);
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.Pings.Changed -= HandlePingsChanged;
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                _subscribed = false;
            }
        }

        private void HandleSettingsChanged(ControlSettings settings)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                AppliedControlSettings.Changed -= HandleSettingsChanged;
                return;
            }

            var enemy = EnemyColorOf(TeamColors.For(settings.Palette));
            if (enemy == _enemyColor)
            {
                return;
            }

            _enemyColor = enemy;
            foreach (var entry in _entries.Values)
            {
                PaintEntry(entry);
            }
        }

        private void HandlePingsChanged(IReadOnlyList<PingMarker> markers)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Pings.Changed -= HandlePingsChanged;
                return;
            }

            ShowSignal(markers);
        }

        private Camera ResolveViewer()
        {
            return Viewer != null ? Viewer : LocalPlayerCamera();
        }

        public static Camera LocalPlayerCamera()
        {
            var player = GameSignals.LocalPlayer.Value?.Player;
            var rig = player != null ? player.GetComponent<ViewRig>() : null;
            var camera = rig != null ? rig.Camera : null;
            return camera != null ? camera : Camera.main;
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
                PaintEntry(entry);
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
            entry.Icon = icon;
            entry.Arrow = arrow;
        }

        // 敵の印の色はパレットで変わるので USS に決め打ちせずここで入れる。場所の印は USS の色のまま。
        private void PaintEntry(Entry entry)
        {
            if (entry.Kind != PingKind.Enemy || entry.Root == null)
            {
                return;
            }

            entry.Root.style.color = _enemyColor;
            entry.Icon.style.backgroundColor = _enemyColor;
            entry.Arrow.MarkDirtyRepaint();
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
            entry.Icon = null;
            entry.Arrow = null;
        }

        private sealed class Entry
        {
            public PingKind Kind;
            public PingScreenPlacement Placement;
            public bool HasPlacement;
            public VisualElement Root;
            public VisualElement Icon;
            public VisualElement Arrow;
        }
    }
}
