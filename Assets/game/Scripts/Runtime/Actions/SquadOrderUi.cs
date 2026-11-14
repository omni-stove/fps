using System;

namespace Fps.Runtime
{
    /// <summary>
    /// 分隊への指示の印の表示 (<see cref="ISquadOrderView"/>) を作る登録口。既定の生成役は UI のアセンブリ (Fps.Runtime.UI) が起動時に
    /// <see cref="UseDefaultViewFactory"/> で登録し、使う側は <see cref="ViewFactory"/> に自分の生成役を入れると差し替えられる (null で既定に戻る)。
    /// </summary>
    /// <remarks>
    /// <see cref="IHud"/> に項目を足すと、HUD を自分で実装しているゲームがすべて直す羽目になるので、<see cref="PingUi"/> と同じ形の別の口にしている。
    /// </remarks>
    public static class SquadOrderUi
    {
        private static ISquadOrderViewFactory _factory;
        private static ISquadOrderViewFactory _defaultFactory;

        /// <summary>指示の印の表示を作る生成役。差し替えていなければ既定。どちらも無ければ null。</summary>
        public static ISquadOrderViewFactory ViewFactory
        {
            get => _factory ?? _defaultFactory;
            set => _factory = value;
        }

        /// <summary>既定の生成役を factory にする。<see cref="ViewFactory"/> を差し替えていればそちらを使い続ける。</summary>
        /// <exception cref="ArgumentNullException">factory が null。</exception>
        public static void UseDefaultViewFactory(ISquadOrderViewFactory factory)
        {
            _defaultFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary><see cref="ViewFactory"/> で hud に指示の印の表示を付ける。hud か生成役が無いか、生成役が出せないと答えたら null。</summary>
        public static ISquadOrderView CreateView(IHud hud)
        {
            var factory = ViewFactory;
            if (factory == null || !GameUi.IsAlive(hud))
            {
                return null;
            }

            return factory.Create(hud);
        }
    }
}
