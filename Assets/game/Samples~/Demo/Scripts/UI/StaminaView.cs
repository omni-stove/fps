using Shooter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class StaminaView : HudElementView
    {
        public const string UxmlName = "stamina";

        public const string FillName = "stamina-fill";

        public const string ReadingName = "stamina-reading";

        public const string DepletedClass = "stamina--depleted";

        private StaminaStatus? _status;
        private VisualElement _fill;
        private Label _readingLabel;
        private string _reading = string.Empty;
        private bool _subscribed;

        public override string ElementName => UxmlName;

        public StaminaStatus? Status => _status;

        public bool IsShown => StaminaText.IsShown(_status);

        public bool IsDepleted => _status.HasValue && _status.Value.Depleted;

        public float FillPercent => _status.HasValue ? StaminaText.FillPercent(_status.Value) : 0f;

        public string Reading => _reading;

        public void Show(StaminaStatus? status)
        {
            _status = status;
            Refresh();
        }

        public void Refresh()
        {
            _reading = IsShown ? Localizer.Resolve(StaminaText.Reading(_status.Value)) : string.Empty;
            Write();
        }

        protected override void OnBound(VisualElement element)
        {
            _fill = element.Q<VisualElement>(FillName);
            if (_fill == null)
            {
                // 塗りの子が無いレイアウトでも、ゲージの枠そのものの幅で量を出せるようにする。
                _fill = new VisualElement { name = FillName, pickingMode = PickingMode.Ignore };
                _fill.AddToClassList("stamina__fill");
                element.Add(_fill);
            }

            _readingLabel = element.Q<Label>(ReadingName);
            if (_readingLabel == null)
            {
                _readingLabel = new Label { name = ReadingName, pickingMode = PickingMode.Ignore };
                _readingLabel.style.position = Position.Absolute;
                _readingLabel.style.width = 1;
                _readingLabel.style.height = 1;
                _readingLabel.style.overflow = Overflow.Hidden;
                _readingLabel.style.opacity = 0f;
                element.Add(_readingLabel);
            }

            Subscribe();
            Show(GameSignals.Stamina.Value);
        }

        protected override void OnLanguageChanged()
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.RemoveFromClassList(DepletedClass);
            element.tooltip = string.Empty;
            element.style.display = DisplayStyle.None;
            _readingLabel?.RemoveFromHierarchy();
            _readingLabel = null;
            _fill = null;
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

        // 走っている間は毎フレーム値が変わるので、要素を作り直さずに幅とクラスだけ書き換える。
        private void Write()
        {
            var element = Element;
            if (element == null || _fill == null)
            {
                return;
            }

            var width = new StyleLength(Length.Percent(FillPercent));
            if (_fill.style.width != width)
            {
                _fill.style.width = width;
            }

            element.EnableInClassList(DepletedClass, IsShown && IsDepleted);
            // tooltip はポインタを無視する要素とランタイムのパネルでは出ないので、文は見えない Label にも書く。
            if (element.tooltip != _reading)
            {
                element.tooltip = _reading;
            }

            if (_readingLabel != null && _readingLabel.text != _reading)
            {
                _readingLabel.text = _reading;
            }

            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            GameSignals.Stamina.Changed += HandleStaminaChanged;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed)
            {
                return;
            }

            GameSignals.Stamina.Changed -= HandleStaminaChanged;
            _subscribed = false;
        }

        private void HandleStaminaChanged(StaminaStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Stamina.Changed -= HandleStaminaChanged;
                return;
            }

            Show(status);
        }
    }
}
