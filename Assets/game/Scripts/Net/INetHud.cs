using UnityEngine;

namespace Fps.Net
{
    /// <summary>
    /// オンラインのゲーム進行 (<see cref="NetPlayer"/>) から見た、自分のプレイヤーの HUD。
    /// 作るのは <see cref="NetUi.HudFactory"/> に登録された生成役で、ゲーム進行は具体的な UI のクラスを知らない。
    /// </summary>
    public interface INetHud
    {
        // Component.gameObject がそのまま実装になるよう、Unity の名前に合わせる。
        /// <summary>HUD を載せている GameObject。プレイヤーが消えるときに GameObject ごと消す。</summary>
        GameObject gameObject { get; }

        /// <summary>「あと remainingSeconds 秒で切断」の放置の警告を出し、ここから数える。負・NaN は 0 として扱う。</summary>
        void ShowIdleWarning(float remainingSeconds);

        /// <summary>放置の警告を消す。出していなければ何もしない。</summary>
        void ClearIdleWarning();
    }
}
