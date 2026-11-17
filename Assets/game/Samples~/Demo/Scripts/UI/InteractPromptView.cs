using Shooter.Core;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public sealed class InteractPromptView : HudElementView
    {
        public const string UxmlName = "interact-prompt";

        private readonly InteractKeyLabel _keyLabel = new InteractKeyLabel();
        private object _owner;
        private LocalizedText _source;
        private string _text = string.Empty;
        private bool _subscribed;

        // GameSignals から出している案内。キーの表記は入力機器を替えると変わるので、出している間は毎フレーム文にし直す
        private InteractHint? _hint;
        private string _hintKeyLabel;
        private string _actionPath;
        private InputAction _action;

        public override string ElementName => UxmlName;

        public bool IsShown => _owner != null;

        public string Text => IsShown ? _text : string.Empty;

        public object Owner => _owner;

        public string HintKeyLabel => _hint.HasValue ? _hintKeyLabel : null;

        public static bool TryDescribe(object detail, string keyLabel, out LocalizedText text)
        {
            switch (detail)
            {
                case PickupHint pickup:
                    return PickupPrompt.TryDescribe(pickup, keyLabel, out text);
                case AmmoCrateHint crate:
                    text = AmmoCratePrompt.Describe(crate, keyLabel);
                    return true;
                default:
                    text = default;
                    return false;
            }
        }

        public static string InputActionPathOf(object detail)
        {
            switch (detail)
            {
                case PickupHint pickup:
                    return pickup.InputActionPath;
                case AmmoCrateHint crate:
                    return crate.InputActionPath;
                default:
                    return string.Empty;
            }
        }

        public void Show(object owner, LocalizedText text)
        {
            if (owner == null)
            {
                throw new System.ArgumentNullException(nameof(owner));
            }

            _hint = null;
            if (!Write(owner, text))
            {
                Hide(owner);
            }
        }

        public void Hide(object owner)
        {
            if (_owner == null || _owner != owner)
            {
                return;
            }

            Clear();
        }

        public void Show(InteractHint? hint)
        {
            if (hint == null || hint.Value.Source == null)
            {
                if (_hint.HasValue)
                {
                    Clear();
                }

                return;
            }

            _hint = hint;
            ShowHint(KeyLabelFor(hint.Value.Detail));
        }

        private void ShowHint(string keyLabel)
        {
            var hint = _hint.Value;
            _hintKeyLabel = keyLabel;
            if (!TryDescribe(hint.Detail, keyLabel, out var text) || !Write(hint.Source, text))
            {
                Clear();
            }
        }

        private bool Write(object owner, LocalizedText text)
        {
            var resolved = Localizer.Resolve(text);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                return false;
            }

            if (_owner == owner && _text == resolved)
            {
                return true;
            }

            _owner = owner;
            _source = text;
            _text = resolved;
            Refresh();
            return true;
        }

        private void Clear()
        {
            _hint = null;
            _owner = null;
            _source = default;
            _text = string.Empty;
            Refresh();
        }

        private string KeyLabelFor(object detail)
        {
            var path = InputActionPathOf(detail);
            if (path != _actionPath)
            {
                _actionPath = path;
                _action = path.Length > 0 ? InputSystem.actions?.FindAction(path) : null;
            }

            return _keyLabel.For(_action);
        }

        private void Update()
        {
            if (!_hint.HasValue)
            {
                return;
            }

            var keyLabel = KeyLabelFor(_hint.Value.Detail);
            if (keyLabel != _hintKeyLabel)
            {
                ShowHint(keyLabel);
            }
        }

        protected override void OnBound(VisualElement element)
        {
            Subscribe();
            var hint = GameSignals.InteractHint.Value;
            if (hint.HasValue || _hint.HasValue)
            {
                Show(hint);
            }

            // 要素を持っていない間の言語の変更は届かないので、受けたときに今の言語で文にし直す
            ResolveText();
            Refresh();
        }

        protected override void OnLanguageChanged()
        {
            ResolveText();
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.style.display = DisplayStyle.None;
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                GameSignals.InteractHint.Changed += HandleHintChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.InteractHint.Changed -= HandleHintChanged;
                _subscribed = false;
            }
        }

        private void HandleHintChanged(InteractHint? hint)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.InteractHint.Changed -= HandleHintChanged;
                return;
            }

            Show(hint);
        }

        private void ResolveText()
        {
            if (IsShown)
            {
                _text = Localizer.Resolve(_source);
            }
        }

        private void Refresh()
        {
            var element = Element;
            if (element == null)
            {
                return;
            }

            if (element is Label label)
            {
                label.text = _text;
            }

            element.style.display = IsShown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
