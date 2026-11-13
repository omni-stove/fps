using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// 画面下の中央に、チュートリアルの今の手順 (または完了) を 1 行で出すビュー。Hud.uxml の <see cref="UxmlName"/> の Label を使う。
    /// 何を出すかは <see cref="TutorialPresenter"/> が <see cref="Show"/> / <see cref="Hide"/> で決める。
    /// 要素をまだ受けていない間 (HUD の木を作り直している間) も出す文を覚えておき、受けたときに合わせる。
    /// </summary>
    public sealed class TutorialPromptView : HudElementView
    {
        /// <summary>Hud.uxml のチュートリアルの Label。</summary>
        public const string UxmlName = "tutorial-prompt";

        /// <summary>すべて終えたときに足す USS クラス。</summary>
        public const string CompletedClass = "tutorial-prompt--completed";

        private string _text = string.Empty;
        private bool _shown;
        private bool _completed;

        public override string ElementName => UxmlName;

        /// <summary>出しているか。</summary>
        public bool IsShown => _shown;

        /// <summary>出している文。出していなければ空文字。</summary>
        public string Text => _shown ? _text : string.Empty;

        /// <summary>完了の見た目で出しているか。</summary>
        public bool IsCompleted => _shown && _completed;

        /// <summary>text を出す。completed なら完了の見た目にする。text が null か空なら隠す。</summary>
        public void Show(string text, bool completed)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Hide();
                return;
            }

            _text = text;
            _completed = completed;
            _shown = true;
            Refresh();
        }

        /// <summary>隠す。</summary>
        public void Hide()
        {
            _text = string.Empty;
            _completed = false;
            _shown = false;
            Refresh();
        }

        protected override void OnBound(VisualElement element)
        {
            Refresh();
        }

        protected override void OnUnbinding(VisualElement element)
        {
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
