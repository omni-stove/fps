using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 既定の印の表示の生成役。既定の HUD (<see cref="Hud"/>) に <see cref="PingMarkersView"/> を付ける。ほかの HUD には出さない (null を返す)。
    /// シーンを読み込む前 (エディターでは読み込み時にも) に <see cref="PingUi.UseDefaultViewFactory"/> で登録する。
    /// </summary>
    public sealed class DefaultPingViewFactory : IPingViewFactory
    {
        public IPingView Create(IHud hud)
        {
            return hud is Hud defaultHud ? PingMarkersView.AttachTo(defaultHud) : null;
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            PingUi.UseDefaultViewFactory(new DefaultPingViewFactory());
        }
    }
}
