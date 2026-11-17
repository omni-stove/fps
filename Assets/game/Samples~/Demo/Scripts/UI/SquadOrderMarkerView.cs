using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class SquadOrderMarkerView : HudElementView
    {
        public const string UxmlName = "squad-order";

        public const string MarkerClass = "squad-order__marker";

        public const string AttackClass = "squad-order__marker--attack";

        public const string HoldClass = "squad-order__marker--hold";

        public const string OffScreenClass = "squad-order__marker--offscreen";

        public const string IconClass = "squad-order__icon";

        public const string LabelClass = "squad-order__label";

        public const string ArrowClass = "squad-order__arrow";

        public const float EdgeMargin = PingMarkersView.EdgeMargin;

        // カメラの縦横比が読めないときに使う。HUD の基準解像度 (1920x1080) の比。
        private const float _fallbackAspect = 16f / 9f;

        private SquadOrderStatus? _order;
        private bool _subscribed;

        // 出している指示が GameSignals から来たか。来ていれば、要素を持っていない間に指示が消されたことを、受けたときに消して合わせる
        private bool _showsSignal;
        private VisualElement _root;
        private VisualElement _arrow;
        private Label _label;
        private BotErrandKind _shownKind = BotErrandKind.None;
        private PingScreenPlacement _placement;
        private bool _hasPlacement;

        public override string ElementName => UxmlName;

        public SquadOrderStatus? Order => _order;

        public Camera Viewer { get; set; }

        public BotErrandKind ShownKind => _hasPlacement ? _shownKind : BotErrandKind.None;

        public string ShownLabel => _hasPlacement && _label != null ? _label.text : string.Empty;

        public void Show(SquadOrderStatus? order)
        {
            _showsSignal = false;
            Write(order);
        }

        public bool TryGetPlacement(out PingScreenPlacement placement)
        {
            placement = _placement;
            return _hasPlacement;
        }

        public void Refresh()
        {
            var kind = _order.HasValue ? _order.Value.Order : BotErrandKind.None;
            var camera = ResolveViewer();
            if (kind == BotErrandKind.None || camera == null || !TryPlace(camera, _order.Value.CurrentPoint, out var placement))
            {
                _hasPlacement = false;
                _shownKind = kind;
                Apply();
                return;
            }

            if (!_hasPlacement || _shownKind != kind || _placement.IsOnScreen != placement.IsOnScreen || _placement.Edge != placement.Edge)
            {
                Debug.Log(SquadOrderLog.Screen(kind, placement), this);
            }

            _shownKind = kind;
            _placement = placement;
            _hasPlacement = true;
            Apply();
        }

        protected override void OnBound(VisualElement element)
        {
            // 前の木の要素は木と一緒に捨てられているので、新しい木に作り直す。
            _root = null;
            _arrow = null;
            _label = null;
            Subscribe();
            var order = GameSignals.SquadOrder.Value;
            if (order.HasValue || _showsSignal)
            {
                ShowSignal(order);
                return;
            }

            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            if (_root != null)
            {
                _root.RemoveFromHierarchy();
            }

            _root = null;
            _arrow = null;
            _label = null;
            element.style.display = DisplayStyle.None;
        }

        protected override void OnLanguageChanged()
        {
            base.OnLanguageChanged();
            Refresh();
        }

        private void LateUpdate()
        {
            // SquadCommandController (DefaultExecutionOrder 110) が指示を出し、カメラが動いた後に置き直す。
            Refresh();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Write(SquadOrderStatus? order)
        {
            if (!_order.Equals(order))
            {
                _hasPlacement = false;
                _shownKind = BotErrandKind.None;
            }

            _order = order;
            Refresh();
        }

        private void ShowSignal(SquadOrderStatus? order)
        {
            Write(order);
            _showsSignal = true;
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.SquadOrder.Changed += HandleOrderChanged;
                _subscribed = true;
                Debug.Log(SquadOrderLog.View(GetType().Name), this);
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.SquadOrder.Changed -= HandleOrderChanged;
                _subscribed = false;
            }
        }

        private void HandleOrderChanged(SquadOrderStatus? order)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.SquadOrder.Changed -= HandleOrderChanged;
                return;
            }

            ShowSignal(order);
        }

        private Camera ResolveViewer()
        {
            return Viewer != null ? Viewer : PingMarkersView.LocalPlayerCamera();
        }

        private static bool TryPlace(Camera camera, Vector3 point, out PingScreenPlacement placement)
        {
            var viewport = camera.WorldToViewportPoint(point);
            if (float.IsNaN(viewport.x) || float.IsInfinity(viewport.x)
                || float.IsNaN(viewport.y) || float.IsInfinity(viewport.y)
                || float.IsNaN(viewport.z) || float.IsInfinity(viewport.z))
            {
                placement = default;
                return false;
            }

            var aspect = camera.aspect > 0f && !float.IsInfinity(camera.aspect) ? camera.aspect : _fallbackAspect;
            var margin = Mathf.Min(EdgeMargin, 0.49f * Mathf.Min(1f, aspect));
            placement = PingScreen.Place(viewport.ToNumerics(), aspect, margin);
            return true;
        }

        private void Apply()
        {
            var parent = Element;
            if (parent == null)
            {
                return;
            }

            if (!_hasPlacement)
            {
                if (_root != null)
                {
                    _root.style.display = DisplayStyle.None;
                }

                parent.style.display = DisplayStyle.None;
                return;
            }

            if (_root == null)
            {
                CreateElements(parent);
            }

            parent.style.display = DisplayStyle.Flex;
            _root.style.display = DisplayStyle.Flex;
            _root.style.left = Length.Percent(_placement.X * 100f);
            _root.style.top = Length.Percent(_placement.Y * 100f);
            _root.EnableInClassList(AttackClass, _shownKind == BotErrandKind.Attack);
            _root.EnableInClassList(HoldClass, _shownKind != BotErrandKind.Attack);
            _root.EnableInClassList(OffScreenClass, !_placement.IsOnScreen);
            _arrow.style.display = _placement.IsOnScreen ? DisplayStyle.None : DisplayStyle.Flex;
            _arrow.style.rotate = new Rotate(new Angle(_placement.AngleDegrees, AngleUnit.Degree));
            _label.text = SquadOrderText.Marker(_shownKind);
        }

        private void CreateElements(VisualElement parent)
        {
            _root = new VisualElement { pickingMode = PickingMode.Ignore };
            _root.AddToClassList(MarkerClass);

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList(IconClass);
            _root.Add(icon);

            _label = new Label { pickingMode = PickingMode.Ignore };
            _label.AddToClassList(LabelClass);
            _root.Add(_label);

            // 矢印は要素の上に描いておき、要素ごと中央を軸に回して向きを出す (毎フレーム描き直さずに済む)。
            _arrow = new VisualElement { pickingMode = PickingMode.Ignore };
            _arrow.AddToClassList(ArrowClass);
            _arrow.generateVisualContent += DrawArrow;
            _root.Add(_arrow);

            parent.Add(_root);
        }

        private static void DrawArrow(MeshGenerationContext context)
        {
            var element = context.visualElement;
            var rect = element.contentRect;
            var tipY = rect.yMin - PingMarkersView.ArrowLength;
            var painter = context.painter2D;
            painter.fillColor = element.resolvedStyle.color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.center.x, tipY));
            painter.LineTo(new Vector2(rect.center.x + PingMarkersView.ArrowHalfWidth, rect.yMin));
            painter.LineTo(new Vector2(rect.center.x - PingMarkersView.ArrowHalfWidth, rect.yMin));
            painter.ClosePath();
            painter.Fill();
        }
    }
}
