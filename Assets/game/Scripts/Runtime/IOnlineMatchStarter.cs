using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// メニューのオンラインのボタン (ロビーを作る・入る・クイックマッチ) を、Netcode の NetTest のシーンではなく別の通信手順で始める差し込み口。
    /// <c>-netstack v2</c> のクライアント (Fps.NetV2) が <see cref="MenuNavigator.OnlineStarter"/> に入れる。
    /// Runtime は通信の手順を参照できない (参照される側) ので、手順の側から登録させる。
    /// </summary>
    public interface IOnlineMatchStarter
    {
        /// <summary>destination のオンラインの試合を始める。始められなければ false と、メニューに出す理由 refusal。</summary>
        bool Start(MenuDestination destination, out LocalizedText refusal);
    }
}
