using System;
using Shooter.Core;

namespace Shooter.NetV2
{
    // 決まった時点でなく次の試合が始まってから替えるのは、サーバーが当たり判定と出現地点を替えるのが次の試合の始まり (ServerMatch.ChangeMapForNextMatch) で、
    // 試合の合間はまだ前のマップで動いているため。次の試合の始まりは、スナップショットの試合の状態が Ended から抜けたことで知る。
    public sealed class ClientMapChange
    {
        public string CurrentMap { get; private set; } = string.Empty;

        public string PendingMap { get; private set; }

        public void Enter(string map)
        {
            CurrentMap = map ?? string.Empty;
            PendingMap = null;
        }

        // 決まっていない投票の様子 (空の Id) が届いたら、新しい投票が始まったので前に決まったマップを捨てる。
        public void Decide(string mapId)
        {
            PendingMap = string.IsNullOrEmpty(mapId) || string.Equals(mapId, CurrentMap, StringComparison.Ordinal) ? null : mapId;
        }

        // 決まったマップは Ended の間にしか入らず、Ended 以外の状態で残っていれば抜けたあとなので、直前の状態は見ない。
        // 読み込み中は状態を見られず抜けた瞬間を取りこぼすため。
        public bool TryTakeChange(GameModeState state, out string map)
        {
            map = null;
            if (PendingMap == null || state == GameModeState.Ended)
            {
                return false;
            }

            map = PendingMap;
            PendingMap = null;
            return true;
        }
    }
}
