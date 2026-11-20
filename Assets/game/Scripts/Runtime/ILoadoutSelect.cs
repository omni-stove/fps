using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public interface ILoadoutSelect
    {
        bool IsAnyOpen { get; }

        bool IsOpen { get; }

        // 決めた並びをどこへ送るかは開いた側 (ネット対戦のクライアント) が決めるので、画面は選んだ武器と能力の Id を渡すだけにする。
        event Action<LoadoutChoice> Confirmed;

        // 出す画面が無い (UI が HUD を作っていない) ときは何もしない。HUD 無しでも試合は進めるため。
        // 能力のカタログが無いゲームでも武器は選べるよう、abilities は null で開ける (そのとき Confirmed の AbilityIds も null)。
        void Open(LoadoutSelection selection, AbilitySelection abilities);

        void Close();

        void ShowSending();

        void ShowAnswer(bool accepted, string reason);
    }
}
