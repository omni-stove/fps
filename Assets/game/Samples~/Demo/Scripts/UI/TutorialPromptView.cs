using Shooter.Core;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    public sealed class TutorialPromptView : HudElementView
    {
        public const string UxmlName = "tutorial-prompt";

        public const string CompletedClass = "tutorial-prompt--completed";

        private LocalizedText _source;
        private string _text = string.Empty;
        private bool _shown;
        private bool _completed;
        private bool _subscribed;

        // 出している文が GameSignals から来たか。来ていれば、要素を持っていない間に段階が消されたことを、受けたときに隠して合わせる
        private bool _showsSignal;

        public override string ElementName => UxmlName;

        public bool IsShown => _shown;

        public string Text => _shown ? _text : string.Empty;

        public bool IsCompleted => _shown && _completed;

        public void Show(string text, bool completed)
        {
            Show(string.IsNullOrEmpty(text) ? default : LocalizedText.Literal(text), completed);
        }

        public void Show(LocalizedText text, bool completed)
        {
            _showsSignal = false;
            Write(text, completed);
        }

        public void Show(TutorialStatus? status)
        {
            if (!status.HasValue)
            {
                Hide();
                return;
            }

            Write(TutorialText.For(status.Value), status.Value.IsComplete);
            _showsSignal = true;
        }

        public void RefreshKeys()
        {
            var status = CampaignSignals.Tutorial.Value;
            if (!_showsSignal || !_shown || !status.HasValue)
            {
                return;
            }

            var text = TutorialText.For(status.Value);
            if (text != _source)
            {
                Write(text, status.Value.IsComplete);
            }
        }

        // 割り当てを変えても段階は変わらず通知が来ないので、出している間は毎フレーム今の割り当てでキーの表記を引き直す
        private void LateUpdate()
        {
            RefreshKeys();
        }

        private void Write(LocalizedText text, bool completed)
        {
            var resolved = Localizer.Resolve(text);
            if (string.IsNullOrWhiteSpace(resolved))
            {
                Hide();
                return;
            }

            _source = text;
            _text = resolved;
            _completed = completed;
            _shown = true;
            Refresh();
        }

        public void Hide()
        {
            _showsSignal = false;
            _source = default;
            _text = string.Empty;
            _completed = false;
            _shown = false;
            Refresh();
        }

        protected override void OnBound(VisualElement element)
        {
            Subscribe();
            var status = CampaignSignals.Tutorial.Value;
            if (status.HasValue || _showsSignal)
            {
                Show(status);
                return;
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

        private void ResolveText()
        {
            if (_shown)
            {
                _text = Localizer.Resolve(_source);
            }
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            if (!_subscribed)
            {
                CampaignSignals.Tutorial.Changed += HandleStatusChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                CampaignSignals.Tutorial.Changed -= HandleStatusChanged;
                _subscribed = false;
            }
        }

        private void HandleStatusChanged(TutorialStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                CampaignSignals.Tutorial.Changed -= HandleStatusChanged;
                return;
            }

            Show(status);
        }

        protected override void OnUnbinding(VisualElement element)
        {
            Unsubscribe();
            element.style.display = DisplayStyle.None;
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

            element.EnableInClassList(CompletedClass, _shown && _completed);
            element.style.display = _shown ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
