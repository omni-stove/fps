using UnityEngine;

namespace Shooter.Runtime
{
    // ミニマップが目標の種類 (ミッションのエリアなど) を知らずに印を出せるよう、位置と出すかどうかだけを見せてもらう。
    public interface IMinimapObjective
    {
        Vector3 Position { get; }

        bool ShowsOnMinimap { get; }
    }
}
