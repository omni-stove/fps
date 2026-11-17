using System;
using System.Collections.Generic;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>ロードアウトを選ぶ画面の開閉の様子。ゲーム進行は <see cref="GameUi.IsLoadoutSelectOpen"/> から読む。</summary>
    public interface ILoadoutSelect
    {
        /// <summary>カーソルを解放して開いているロードアウトの画面があるか。</summary>
        bool IsAnyOpen { get; }

        bool IsOpen { get; }

        // 決めた並びをどこへ送るかは開いた側 (ネット対戦のクライアント) が決めるので、画面は選んだ武器の Id を渡すだけにする。
        event Action<IReadOnlyList<string>> Confirmed;

        // 出す画面が無い (UI が HUD を作っていない) ときは何もしない。HUD 無しでも試合は進めるため。
        void Open(LoadoutSelection selection);

        void Close();

        void ShowSending();

        void ShowAnswer(bool accepted, string reason);
    }
}
