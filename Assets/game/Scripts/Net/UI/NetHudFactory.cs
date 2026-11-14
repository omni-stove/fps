using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// 既定のオンラインの HUD の生成役。<see cref="NetHud.Create"/> で HUD を作る。
    /// シーンを読み込む前 (エディターでは読み込み時にも) に <see cref="NetUi.UseDefaultHudFactory"/> で登録する。
    /// </summary>
    public sealed class NetHudFactory : INetHudFactory
    {
        public INetHud Create(NetPlayer player)
        {
            return NetHud.Create(player);
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            NetUi.UseDefaultHudFactory(new NetHudFactory());
        }
    }
}
