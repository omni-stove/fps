using Fps.Core;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 画面の中央の下に、近くで Interact したら何が起きるかを 1 行で出すビュー (<see cref="IInteractPrompt"/>)。Hud.uxml の <see cref="UxmlName"/> の Label を使う。
    /// 要素をまだ受けていない間も出す文を覚えておき、受けたときに合わせる。言語が変わったら文にし直す。
    /// </summary>
    public sealed class InteractPromptView : HudElementView, IInteractPrompt
    {
        /// <summary>Hud.uxml の案内の Label。</summary>
        public const string UxmlName = "interact-prompt";

        private object _owner;
        private LocalizedText _source;
        private string _text = string.Empty;

        public override string ElementName => UxmlName;

        /// <summary>出しているか。</summary>
        public bool IsShown => _owner != null;

        /// <summary>出している文。出していなければ空文字。</summary>
        public string Text => IsShown ? _text : string.Empty;

        /// <summary>今出している案内の owner。出していなければ null。</summary>
        public object Owner => _owner;

        public void Show(object owner, LocalizedText text)
        {
            if (owner == null)
            {
                throw new System.ArgumentNullException(nameof(owner));
            }

            var resolved = Localizer.Resolve(text);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                Hide(owner);
                return;
            }

            if (_owner == owner && _text == resolved)
            {
                return;
            }

            _owner = owner;
            _source = text;
            _text = resolved;
            Refresh();
        }

        public void Hide(object owner)
        {
            if (_owner == null || _owner != owner)
            {
                return;
            }

            _owner = null;
            _source = default;
            _text = string.Empty;
            Refresh();
        }

        protected override void OnBound(VisualElement element)
        {
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
            element.style.display = DisplayStyle.None;
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
