using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// メニューのオンラインのボタン (クイックマッチ) を始める差し込み口。
    /// ネット対戦のクライアント (Fps.NetV2) が <see cref="MenuNavigator.OnlineStarter"/> に入れる。
    /// Runtime は通信の手順を参照できない (参照される側) ので、手順の側から登録させる。
    /// </summary>
    public interface IOnlineMatchStarter
    {
        /// <summary>
        /// destination のオンラインの試合を始める。始められなければ false と、メニューに留まる理由 refusal (無ければ null。どう言うかはメニューが決める)。
        /// </summary>
        bool Start(MenuDestination destination, out MenuReturnReason refusal);
    }
}
