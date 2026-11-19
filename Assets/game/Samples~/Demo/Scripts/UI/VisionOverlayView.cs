using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // HUD の量 (HudVisibility) では隠さない。暗視・サーマルの色は見えている景色そのものだから。
    [DisallowMultipleComponent]
    public sealed class VisionOverlayView : HudElementView
    {
        public const string UxmlName = "vision-overlay";

        public const string NightClass = "vision-overlay--night";

        public const string ThermalClass = "vision-overlay--thermal";

        public const string ScopeClass = "vision-overlay--scope";

        public const string GogglesClass = "vision-overlay--goggles";

        private VisionVisualCatalog _catalog;
        private bool _catalogResolved;
        private VisionStatus? _status;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public VisionVisualCatalog Catalog
        {
            get
            {
                if (!_catalogResolved)
                {
                    _catalog = VisionVisualCatalog.Load();
                    _catalogResolved = true;
                }

                return _catalog;
            }
            set
            {
                _catalog = value;
                _catalogResolved = true;
                Write();
            }
        }

        public VisionStatus? Status => _status;

        public bool IsShown => _status.HasValue && _status.Value.IsActive;

        public VisionMode Mode => IsShown ? _status.Value.Mode : VisionMode.None;

        public Color Tint => VisionVisualCatalog.ColorFor(Catalog, Mode);

        public Texture2D Image => VisionVisualCatalog.ImageFor(Catalog, Mode);

        public void Show(VisionStatus? status)
        {
            _status = status;
            Write();
        }

        protected override void OnBound(VisualElement element)
        {
            Subscribe();
            Show(GameSignals.Vision.Value);
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            SetClasses(element, VisionStatus.None);
            element.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            element.style.backgroundColor = new StyleColor(StyleKeyword.Null);
            element.style.unityBackgroundImageTintColor = new StyleColor(StyleKeyword.Null);
            element.style.display = DisplayStyle.None;
        }

        // HUD を組んだ後に足されたときは、HUD が結び直さないので自分で結ぶ。
        private void Start()
        {
            if (Element != null)
            {
                return;
            }

            var document = GetComponent<UIDocument>();
            var root = document != null ? document.rootVisualElement : null;
            if (root != null && root.childCount > 0)
            {
                Bind(root);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        // 画像があれば色は画像に掛け、無ければ色だけで画面を覆う。
        private void Write()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            var status = IsShown ? _status.Value : VisionStatus.None;
            SetClasses(element, status);
            if (!status.IsActive)
            {
                element.style.display = DisplayStyle.None;
                return;
            }

            var tint = Tint;
            var image = Image;
            if (image != null)
            {
                element.style.backgroundImage = new StyleBackground(image);
                element.style.unityBackgroundImageTintColor = tint;
                element.style.backgroundColor = Color.clear;
            }
            else
            {
                element.style.backgroundImage = new StyleBackground(StyleKeyword.None);
                element.style.unityBackgroundImageTintColor = Color.white;
                element.style.backgroundColor = tint;
            }

            element.style.display = DisplayStyle.Flex;
        }

        private static void SetClasses(VisualElement element, VisionStatus status)
        {
            element.EnableInClassList(NightClass, status.Mode == VisionMode.Night);
            element.EnableInClassList(ThermalClass, status.Mode == VisionMode.Thermal);
            element.EnableInClassList(ScopeClass, status.Source == VisionSource.Scope);
            element.EnableInClassList(GogglesClass, status.Source == VisionSource.Goggles);
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.Vision.Changed += HandleVisionChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.Vision.Changed -= HandleVisionChanged;
            _subscribed = false;
        }

        private void HandleVisionChanged(VisionStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Vision.Changed -= HandleVisionChanged;
                return;
            }

            Show(status);
        }
    }
}
