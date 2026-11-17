using Fps.Core;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 画面の中央の下に、近くで Interact したら何が起きるかを 1 行で出すビュー (<see cref="IInteractPrompt"/>)。Hud.uxml の <see cref="UxmlName"/> の Label を使う。
    /// 要素を持っている間は <see cref="GameSignals.InteractHint"/> を購読し、出された案内の中身 (<see cref="PickupHint"/>・<see cref="AmmoCrateHint"/>) を
    /// 今の入力機器のキー (<see cref="InteractKeyLabel"/>) を入れた文にして出し、消されたら隠す。要素を受けたときは今の案内に合わせる。
    /// 要素をまだ受けていない間も出す文を覚えておき、受けたときに合わせる。言語が変わったら文にし直す。
    /// </summary>
    public sealed class InteractPromptView : HudElementView, IInteractPrompt
    {
        /// <summary>Hud.uxml の案内の Label。</summary>
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

        /// <summary>出しているか。</summary>
        public bool IsShown => _owner != null;

        /// <summary>出している文。出していなければ空文字。</summary>
        public string Text => IsShown ? _text : string.Empty;

        /// <summary>今出している案内の owner。出していなければ null。</summary>
        public object Owner => _owner;

        /// <summary><see cref="GameSignals.InteractHint"/> の案内に入れているキーの表記。案内を出していなければ null。</summary>
        public string HintKeyLabel => _hint.HasValue ? _hintKeyLabel : null;

        /// <summary>
        /// detail (<see cref="InteractHint.Detail"/>) の案内の文。keyLabel は Interact のキーの表示。
        /// 文にできない中身 (知らない型を含む) なら false。
        /// </summary>
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

        /// <summary>
        /// hint を文にして出す。null なら <see cref="GameSignals.InteractHint"/> から出していた案内を隠す。
        /// 要素を持っている間は <see cref="GameSignals.InteractHint"/> が出されるたびに呼ばれる。
        /// </summary>
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
