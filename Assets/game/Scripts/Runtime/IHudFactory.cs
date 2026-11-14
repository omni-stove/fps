using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ゲーム進行が使う画面 (HUD とプレイヤーのデバッグ表示) を作る生成役。<see cref="GameUi.HudFactory"/> に登録して使う。
    /// </summary>
    public interface IHudFactory
    {
        /// <summary>
        /// HUD を objectName の GameObject で作り、値の源を渡す。源はどれも null 可。
        /// 作れなければ作りかけのものを消して例外を投げる。
        /// </summary>
        /// <param name="objectName">HUD の GameObject の名前。</param>
        /// <param name="player">体力を出すプレイヤー。</param>
        /// <param name="weapon">弾数と照準を出す武器。</param>
        /// <param name="match">残り時間を出す試合。後で <see cref="IHud.BindMatch"/> で渡してもよい。</param>
        IHud Create(string objectName, Damageable player, WeaponController weapon, Match match);

        /// <summary>
        /// player に、reader の今のアクションを出すデバッグ表示を付ける。出さない Build では付けても何もしない。
        /// </summary>
        void AttachActionDebug(GameObject player, PlayerActionReader reader);
    }
}
