using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 既定の分隊への指示の印の生成役。既定の HUD (<see cref="Hud"/>) に <see cref="SquadOrderMarkerView"/> を付ける。ほかの HUD には出さない (null を返す)。
    /// シーンを読み込む前 (エディターでは読み込み時にも) に <see cref="SquadOrderUi.UseDefaultViewFactory"/> で登録する。
    /// </summary>
    public sealed class DefaultSquadOrderViewFactory : ISquadOrderViewFactory
    {
        public ISquadOrderView Create(IHud hud)
        {
            return hud is Hud defaultHud ? SquadOrderMarkerView.AttachTo(defaultHud) : null;
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SquadOrderUi.UseDefaultViewFactory(new DefaultSquadOrderViewFactory());
        }
    }
}
