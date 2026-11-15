using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// 近くで Interact したら何が起きるかを 1 行で出す案内 (例: 「[E] 長押しで弾薬を補給」)。出す物 (owner) ごとに出し入れし、
    /// 後から出した物が前の物を置き換える。隠すのは今出している owner だけができる (別の物が出した案内を消さない)。
    /// </summary>
    public interface IInteractPrompt
    {
        /// <summary>owner の案内として text を出す。text が空白だけの文なら owner の案内を隠す。</summary>
        void Show(object owner, LocalizedText text);

        /// <summary>今出しているのが owner の案内なら隠す。</summary>
        void Hide(object owner);
    }
}
