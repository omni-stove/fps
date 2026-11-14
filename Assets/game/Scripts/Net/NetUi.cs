using System;

namespace Fps.Net
{
    /// <summary>
    /// オンラインのゲーム進行が自分のプレイヤーの HUD を作るときの登録口。<see cref="Fps.Runtime.GameUi"/> と同じく、
    /// 既定の生成役は画面のアセンブリ (Fps.Net.UI) が起動時に <see cref="UseDefaultHudFactory"/> で登録し、
    /// 使う側は <see cref="HudFactory"/> に自分の生成役を入れると差し替えられる (null を入れると既定に戻る)。
    /// </summary>
    public static class NetUi
    {
        /// <summary>生成役が登録されていないので、HUD 無しで進めるときのログ。</summary>
        public const string NoHudLog = "[Net] no HUD factory is registered with NetUi.HudFactory; running without a HUD";

        private static INetHudFactory _hudFactory;
        private static INetHudFactory _defaultHudFactory;

        /// <summary>HUD を作る生成役。差し替えていなければ既定の生成役。どちらも無ければ null。</summary>
        public static INetHudFactory HudFactory
        {
            get => _hudFactory ?? _defaultHudFactory;
            set => _hudFactory = value;
        }

        /// <summary>
        /// 既定の生成役を factory にする。<see cref="HudFactory"/> を差し替えていればそちらを使い続ける
        /// (起動時の登録の順番に依らず、差し替えが既定に上書きされないよう、既定は別に持つ)。
        /// </summary>
        /// <exception cref="ArgumentNullException">factory が null。</exception>
        public static void UseDefaultHudFactory(INetHudFactory factory)
        {
            _defaultHudFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        // 画面のアセンブリが無い状態は、テストで登録済みの既定を外さないと作れない。使う側は差し替えれば済むので公開しない
        internal static void ClearDefaults()
        {
            _defaultHudFactory = null;
        }

        /// <summary>
        /// <see cref="HudFactory"/> で player の HUD を作る。生成役が登録されていなければ null を返す
        /// (画面のアセンブリ Fps.Net.UI を入れずに使っても、例外で止めずに HUD 無しで進めるため)。
        /// </summary>
        /// <exception cref="ArgumentNullException">player が null。</exception>
        /// <exception cref="InvalidOperationException">登録された生成役が null を返した。</exception>
        public static INetHud CreateHud(NetPlayer player)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            var factory = HudFactory;
            if (factory == null)
            {
                return null;
            }

            var hud = factory.Create(player);
            if (hud == null)
            {
                throw new InvalidOperationException($"{factory.GetType().Name}.Create returned no HUD");
            }

            return hud;
        }
    }
}
