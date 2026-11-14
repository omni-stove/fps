using System;
using Fps.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 分隊への指示の印 (<see cref="SquadCommandController.CurrentOrder"/>) を HUD に出す既定の表示。Hud.uxml の <see cref="UxmlName"/> を使う。
    /// 守る場所か狙う敵の上に、印と札 (「守れ」「狙え」、<see cref="SquadOrderText.Marker"/>) を出す。
    /// 画面の外なら印 (Ping) と同じく <see cref="PingScreen.Place"/> で端に寄せて向きの矢印を出す。
    /// 印が出たか、画面の中と端を行き来したときに <c>[Squad] screen</c> をログに出す。
    /// <see cref="Hud.AddViews"/> では付けず、プレイヤーの <see cref="SquadCommandController"/> が <see cref="SquadOrderUi"/> の既定の生成役
    /// (<see cref="DefaultSquadOrderViewFactory"/>) で付ける。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SquadOrderMarkerView : HudElementView, ISquadOrderView
    {
        /// <summary>Hud.uxml の指示の印を入れる要素。</summary>
        public const string UxmlName = "squad-order";

        /// <summary>印の要素に付けるクラス。</summary>
        public const string MarkerClass = "squad-order__marker";

        /// <summary>狙う指示の印に足すクラス。</summary>
        public const string AttackClass = "squad-order__marker--attack";

        /// <summary>守る指示の印に足すクラス。</summary>
        public const string HoldClass = "squad-order__marker--hold";

        /// <summary>画面の外の印に足すクラス。</summary>
        public const string OffScreenClass = "squad-order__marker--offscreen";

        /// <summary>印の形の要素に付けるクラス。</summary>
        public const string IconClass = "squad-order__icon";

        /// <summary>札の Label に付けるクラス。</summary>
        public const string LabelClass = "squad-order__label";

        /// <summary>画面の外の印の向きの矢印に付けるクラス (色は color で決める)。</summary>
        public const string ArrowClass = "squad-order__arrow";

        /// <summary>画面の外の印を寄せる端の余白。画面の高さを 1 とした量。</summary>
        public const float EdgeMargin = PingMarkersView.EdgeMargin;

        // カメラの縦横比が読めないときに使う。HUD の基準解像度 (1920x1080) の比。
        private const float _fallbackAspect = 16f / 9f;

        private SquadCommandController _source;
        private VisualElement _root;
        private VisualElement _arrow;
        private Label _label;
        private BotErrandKind _shownKind = BotErrandKind.None;
        private PingScreenPlacement _placement;
        private bool _hasPlacement;

        public override string ElementName => UxmlName;

        /// <summary>出している指示の持ち主。出していなければ null。</summary>
        public SquadCommandController Source => _source;

        /// <summary>向きの基準にするカメラ。null なら持ち主のカメラ (<see cref="SquadCommandController.ViewCamera"/>)、それも無ければ <see cref="Camera.main"/>。</summary>
        public Camera Viewer { get; set; }

        /// <summary>今出している印の指示の種類。出していなければ None。</summary>
        public BotErrandKind ShownKind => _hasPlacement ? _shownKind : BotErrandKind.None;

        /// <summary>今出している札の文字。出していなければ空。</summary>
        public string ShownLabel => _hasPlacement && _label != null ? _label.text : string.Empty;

        /// <summary>
        /// hud に (無ければ) このビューを付けて返す。付けたばかりなら Hud に木から要素を渡し直させる。
        /// UXML に要素が無ければエラーをログに出し、要素を持たないまま返す (位置は <see cref="TryGetPlacement"/> で読める)。
        /// </summary>
        public static SquadOrderMarkerView AttachTo(Hud hud)
        {
            if (hud == null)
            {
                throw new ArgumentNullException(nameof(hud));
            }

            var view = hud.GetComponent<SquadOrderMarkerView>();
            if (view != null)
            {
                return view;
            }

            view = hud.gameObject.AddComponent<SquadOrderMarkerView>();
            try
            {
                hud.RebuildView();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError($"{Hud.LogTag} could not show squad orders on the HUD: {e.Message}", hud);
            }

            return view;
        }

        public void Show(SquadCommandController source)
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

            _hasPlacement = false;
            _shownKind = BotErrandKind.None;
            Refresh();
        }

        /// <summary>今出している印の位置と向き。出していなければ false。</summary>
        public bool TryGetPlacement(out PingScreenPlacement placement)
        {
            placement = _placement;
            return _hasPlacement;
        }

        /// <summary>持ち主の今の指示を、今のカメラから見て置き直す。</summary>
        public void Refresh()
        {
            var kind = _source != null ? _source.CurrentOrder : BotErrandKind.None;
            var camera = ResolveViewer();
            if (kind == BotErrandKind.None || camera == null || !TryPlace(camera, _source.CurrentPoint, out var placement))
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
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
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
            // 持ち主 (DefaultExecutionOrder 110) が指示を出し、カメラが動いた後に置き直す。
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

        private void OnChanged(SquadCommandController source)
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
