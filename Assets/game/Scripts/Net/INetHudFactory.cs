namespace Fps.Net
{
    /// <summary>
    /// 自分のプレイヤーの HUD (<see cref="INetHud"/>) を作る生成役。<see cref="NetUi.HudFactory"/> に登録して使う。
    /// </summary>
    public interface INetHudFactory
    {
        /// <summary>
        /// player の HUD を作り、player とその武器・今の試合を購読させる。作れなければ作りかけのものを消して例外を投げる。
        /// </summary>
        INetHud Create(NetPlayer player);
    }
}
