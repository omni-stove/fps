using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>射撃場のチュートリアル (<see cref="TutorialPresenter"/>) の今の手順 (または完了) を 1 行で出す表示。</summary>
    public interface ITutorialPrompt
    {
        /// <summary>text を出す。completed なら完了の見た目にする。text が空白だけの文なら隠す。</summary>
        void Show(LocalizedText text, bool completed);

        /// <summary>隠す。</summary>
        void Hide();
    }
}
