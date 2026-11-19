using System;
using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // HUD の量 (HudVisibility) では隠さない。縁と照準はスコープ越しに見えている景色そのものだから。
    [DisallowMultipleComponent]
    public sealed class ScopeOverlayView : HudElementView
    {
        public const string UxmlName = "scope-overlay";

        public const string ImageClass = "scope-overlay__image";

        public const int MilDotsPerArm = 4;

        // 点の間隔を縁の内側の半径の割合で決めるのは、画面の大きさが変わっても点で測る距離の目安が変わらないようにするため。
        public const float MilDotSpacing = 0.1f;

        public const float MilDotPostStart = 0.6f;

        private ReticleCatalog _catalog;
        private bool _catalogResolved;
        private Hud _hud;
        private VisualElement _image;

        public override string ElementName => UxmlName;

        public ReticleCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = ReticleCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
                Apply();
            }
        }

        public Func<SightView> SightSource { get; set; }

        public SightView Shown { get; private set; }

        public ReticleEntry Reticle { get; private set; }

        public bool IsShown => Shown.ScopeOverlay || Shown.ShowsReticle;

        public float LensDiameter => Catalog != null ? Catalog.LensDiameter : ReticleCatalog.DefaultLensDiameter;

        public Color MaskColor => Catalog != null ? Catalog.MaskColor : ReticleCatalog.DefaultMaskColor;

        private void OnEnable()
        {
            _hud = GetComponent<Hud>();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        public void Refresh()
        {
            var view = ReadSightView();
            if (view == Shown)
            {
                return;
            }

            Shown = view;
            Apply();
        }

        public static float ReticleSize(ReticleEntry reticle, bool scopeOverlay, float screenHeight, float lensDiameter)
        {
            if (reticle == null || !(screenHeight > 0f))
            {
                return 0f;
            }

            var basis = scopeOverlay ? screenHeight * lensDiameter : screenHeight;
            return basis * reticle.Size;
        }

        protected override void OnBound(VisualElement element)
        {
            element.generateVisualContent += Draw;
            element.RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _image = new VisualElement { pickingMode = PickingMode.Ignore };
            _image.AddToClassList(ImageClass);
            element.Add(_image);
            Apply();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            element.generateVisualContent -= Draw;
            element.UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
            _image?.RemoveFromHierarchy();
            _image = null;
        }

        private SightView ReadSightView()
        {
            if (SightSource != null)
            {
                return SightSource();
            }

            if (_hud == null)
            {
                _hud = GetComponent<Hud>();
            }

            var ads = _hud != null ? _hud.Ads : null;
            return ads != null && ads.IsInitialized ? ads.SightView : SightView.None;
        }

        private void OnGeometryChanged(GeometryChangedEvent evt)
        {
            PlaceImage();
            Element?.MarkDirtyRepaint();
        }

        private void Apply()
        {
            var view = Shown;
            Reticle = view.ShowsReticle ? ReticleCatalog.Resolve(Catalog, view.ReticleId, view.ScopeOverlay) : null;
            if (Element == null)
            {
                return;
            }

            Element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
            if (_image != null)
            {
                var image = Reticle != null ? Reticle.Image : null;
                _image.style.display = image != null ? DisplayStyle.Flex : DisplayStyle.None;
                _image.style.backgroundImage = image != null ? new StyleBackground(image) : new StyleBackground(StyleKeyword.None);
                _image.style.unityBackgroundImageTintColor = Reticle != null ? Reticle.Color : Color.white;
                PlaceImage();
            }

            Element.MarkDirtyRepaint();
        }

        private void PlaceImage()
        {
            if (_image == null || Element == null)
            {
                return;
            }

            var rect = Element.contentRect;
            var size = ReticleSize(Reticle, Shown.ScopeOverlay, rect.height, LensDiameter);
            if (float.IsNaN(size))
            {
                size = 0f;
            }

            _image.style.width = size;
            _image.style.height = size;
            _image.style.left = (rect.width - size) * 0.5f;
            _image.style.top = (rect.height - size) * 0.5f;
        }

        private void Draw(MeshGenerationContext context)
        {
            var rect = context.visualElement.contentRect;
            if (!(rect.width > 0f) || !(rect.height > 0f))
            {
                return;
            }

            var painter = context.painter2D;
            var center = rect.center;
            if (Shown.ScopeOverlay)
            {
                DrawMask(painter, rect, center, rect.height * LensDiameter * 0.5f, MaskColor);
            }

            if (Reticle != null && Reticle.Image == null)
            {
                DrawShape(painter, Reticle.Shape, center, ReticleSize(Reticle, Shown.ScopeOverlay, rect.height, LensDiameter), Reticle.Color);
            }
        }

        private static void DrawMask(Painter2D painter, Rect rect, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMin));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax));
            painter.LineTo(new Vector2(rect.xMin, rect.yMax));
            painter.ClosePath();
            painter.MoveTo(center + new Vector2(radius, 0f));
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.ClosePath();
            painter.Fill(FillRule.OddEven);
        }

        private static void DrawShape(Painter2D painter, ReticleShape shape, Vector2 center, float size, Color color)
        {
            var half = size * 0.5f;
            if (!(half > 0f))
            {
                return;
            }

            switch (shape)
            {
                case ReticleShape.Crosshair:
                    // 細い線は中央を狙う点に、外側の太い柱は暗い景色でも中央の位置を目で追えるように。
                    var thin = Mathf.Max(1f, half * 0.005f);
                    var thick = Mathf.Max(3f, half * 0.02f);
                    var postStart = half * 0.45f;
                    painter.strokeColor = color;
                    painter.lineCap = LineCap.Butt;
                    Line(painter, center + new Vector2(-postStart, 0f), center + new Vector2(postStart, 0f), thin);
                    Line(painter, center + new Vector2(0f, -postStart), center + new Vector2(0f, postStart), thin);
                    Line(painter, center + new Vector2(-half, 0f), center + new Vector2(-postStart, 0f), thick);
                    Line(painter, center + new Vector2(postStart, 0f), center + new Vector2(half, 0f), thick);
                    Line(painter, center + new Vector2(0f, postStart), center + new Vector2(0f, half), thick);
                    break;
                case ReticleShape.MilDot:
                    DrawMilDot(painter, center, half, color);
                    break;
                case ReticleShape.Circle:
                    painter.strokeColor = color;
                    painter.lineWidth = Mathf.Max(1f, half * 0.08f);
                    painter.BeginPath();
                    painter.Arc(center, half, Angle.Degrees(0f), Angle.Degrees(360f));
                    painter.Stroke();
                    Dot(painter, center, Mathf.Max(1.5f, half * 0.08f), color);
                    break;
                default:
                    Dot(painter, center, half, color);
                    break;
            }
        }

        private static void DrawMilDot(Painter2D painter, Vector2 center, float half, Color color)
        {
            var thin = Mathf.Max(1f, half * 0.004f);
            var thick = Mathf.Max(3f, half * 0.02f);
            var postStart = half * MilDotPostStart;
            var dot = Mathf.Max(1.5f, half * 0.008f);
            painter.strokeColor = color;
            painter.lineCap = LineCap.Butt;
            Line(painter, center + new Vector2(-postStart, 0f), center + new Vector2(postStart, 0f), thin);
            Line(painter, center + new Vector2(0f, -postStart), center + new Vector2(0f, postStart), thin);
            Line(painter, center + new Vector2(-half, 0f), center + new Vector2(-postStart, 0f), thick);
            Line(painter, center + new Vector2(postStart, 0f), center + new Vector2(half, 0f), thick);
            Line(painter, center + new Vector2(0f, postStart), center + new Vector2(0f, half), thick);
            for (var i = 1; i <= MilDotsPerArm; i++)
            {
                var step = half * MilDotSpacing * i;
                Dot(painter, center + new Vector2(step, 0f), dot, color);
                Dot(painter, center + new Vector2(-step, 0f), dot, color);
                Dot(painter, center + new Vector2(0f, step), dot, color);
                Dot(painter, center + new Vector2(0f, -step), dot, color);
            }
        }

        private static void Line(Painter2D painter, Vector2 from, Vector2 to, float width)
        {
            painter.lineWidth = width;
            painter.BeginPath();
            painter.MoveTo(from);
            painter.LineTo(to);
            painter.Stroke();
        }

        private static void Dot(Painter2D painter, Vector2 center, float radius, Color color)
        {
            painter.fillColor = color;
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0f), Angle.Degrees(360f));
            painter.Fill();
        }
    }
}
