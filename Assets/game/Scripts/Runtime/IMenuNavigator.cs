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
        /// <see cref="ReturnToMenu"/> でメニューのシーンを読む直前。シーンをまたいで残るもの (ネット対戦のクライアントなど) はここで片付ける。
        /// </summary>
        event Action ReturningToMenu;

        /// <summary>メニューでチュートリアルを選んだかを受け取り、持っていた分は消す。</summary>
        bool TakeTutorialRequest();

        /// <summary>
        /// メニューへ戻り、戻った理由 reason をメニューに渡す (null なら何も出さない。どう言うかはメニューが決める)。
        /// メニューのシーンがビルドに無ければエラーログを出して false を返し、今のシーンに留まる。
        /// </summary>
        bool ReturnToMenu(MenuReturnReason reason);
    }
}
