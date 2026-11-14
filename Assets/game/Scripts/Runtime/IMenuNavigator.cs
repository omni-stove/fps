using System;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// メインメニューとの行き来のうち、ゲーム進行 (射撃場のチュートリアル・オンラインの起動) が使う部分。<see cref="GameUi.Menu"/> から使う。
    /// </summary>
    public interface IMenuNavigator
    {
        /// <summary>
        /// <see cref="ReturnToMenu"/> でメニューのシーンを読む直前。シーンをまたいで残るもの (NetworkManager など) はここで片付ける。
        /// </summary>
        event Action ReturningToMenu;

        /// <summary>メニューで選んだロビーの設定を受け取り、持っていた分は消す。無ければ <see cref="LobbyLaunchOptions.None"/>。</summary>
        LobbyLaunchOptions TakeLobbyRequest();

        /// <summary>メニューでチュートリアルを選んだかを受け取り、持っていた分は消す。</summary>
        bool TakeTutorialRequest();

        /// <summary>
        /// メニューへ戻り、notice をメニューに出す (空なら出さない)。メニューのシーンがビルドに無ければエラーログを出して false を返し、今のシーンに留まる。
        /// </summary>
        bool ReturnToMenu(LocalizedText notice);
    }
}
