using Fps.Core;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 画面下の中央に、チュートリアルの今の手順 (または完了) を 1 行で出すビュー。Hud.uxml の <see cref="UxmlName"/> の Label を使う。
    /// 要素を持っている間は <see cref="GameSignals.Tutorial"/> を購読し、出された段階を <see cref="TutorialText"/> で文にして出し、消されたら隠す。
    /// 要素を受けたときは今の段階に合わせる。<see cref="ITutorialPrompt"/> の <see cref="Show(LocalizedText, bool)"/> / <see cref="Hide"/> で直に出すこともでき、
    /// 要素をまだ受けていない間 (HUD の木を作り直している間) も出す文を覚えておき、受けたときに合わせる。
    /// 受け取った文は今の言語 (<see cref="Localizer.Resolve"/>) で文にし、言語が変わったら文にし直す。
    /// </summary>
    public sealed class TutorialPromptView : HudElementView, ITutorialPrompt
    {
        /// <summary>Hud.uxml のチュートリアルの Label。</summary>
        public const string UxmlName = "tutorial-prompt";

        /// <summary>すべて終えたときに足す USS クラス。</summary>
        public const string CompletedClass = "tutorial-prompt--completed";

        private LocalizedText _source;
        private string _text = string.Empty;
        private bool _shown;
        private bool _completed;
        private bool _subscribed;

        // 出している文が GameSignals から来たか。来ていれば、要素を持っていない間に段階が消されたことを、受けたときに隠して合わせる
        private bool _showsSignal;

        public override string ElementName => UxmlName;

        /// <summary>出しているか。</summary>
        public bool IsShown => _shown;

        /// <summary>出している文。出していなければ空文字。</summary>
        public string Text => _shown ? _text : string.Empty;

        /// <summary>完了の見た目で出しているか。</summary>
        public bool IsCompleted => _shown && _completed;

        /// <summary>決まった文字列 text をそのまま出す。completed なら完了の見た目にする。text が null か空白だけなら隠す。</summary>
        public void Show(string text, bool completed)
        {
            Show(string.IsNullOrEmpty(text) ? default : LocalizedText.Literal(text), completed);
        }

        /// <summary>text を今の言語の文にして出す。completed なら完了の見た目にする。文にして空白だけなら隠す。</summary>
        public void Show(LocalizedText text, bool completed)
        {
            _showsSignal = false;
            Write(text, completed);
        }

        /// <summary>
        /// status を <see cref="TutorialText.For"/> で文にして出す。すべて終えた段階なら完了の見た目にする。null なら隠す。
        /// 要素を持っている間は <see cref="GameSignals.Tutorial"/> が出されるたびに呼ばれる。
        /// </summary>
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

        /// <summary>
        /// 出している段階の文を、今のキーの割り当てで作り直す。割り当てが変わって文が変わったときだけ書き直す。
        /// Play 中は毎フレーム呼ぶ。
        /// </summary>
        public void RefreshKeys()
        {
            var status = GameSignals.Tutorial.Value;
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

        /// <summary>隠す。</summary>
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
            var status = GameSignals.Tutorial.Value;
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
                GameSignals.Tutorial.Changed += HandleStatusChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed)
            {
                GameSignals.Tutorial.Changed -= HandleStatusChanged;
                _subscribed = false;
            }
        }

        private void HandleStatusChanged(TutorialStatus? status)
        {
            // Edit Mode では OnDestroy が呼ばれずに消えることがあるので、消えた後に届いた通知で購読を外す
            if (this == null)
            {
                GameSignals.Tutorial.Changed -= HandleStatusChanged;
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
