using System;
using Shooter.Core;

namespace Shooter.NetV2
{
    // 決まった時点でなく次の試合が始まってから替えるのは、サーバーが当たり判定と出現地点を替えるのが次の試合の始まり (ServerMatch.ChangeMapForNextMatch) で、
    // 試合の合間はまだ前のマップで動いているため。次の試合の始まりは、スナップショットの試合の状態が Ended から抜けたことで知る。
    public sealed class ClientMapChange
    {
        private GameModeState? _state;

        public string CurrentMap { get; private set; } = string.Empty;

        public string PendingMap { get; private set; }

        public void Enter(string map)
        {
            CurrentMap = map ?? string.Empty;
            PendingMap = null;
            _state = null;
        }

        // 決まっていない投票の様子 (空の Id) が届いたら、新しい投票が始まったので前に決まったマップを捨てる。
        public void Decide(string mapId)
        {
            PendingMap = string.IsNullOrEmpty(mapId) || string.Equals(mapId, CurrentMap, StringComparison.Ordinal) ? null : mapId;
        }

        public bool TryTakeChange(GameModeState state, out string map)
        {
            var previous = _state;
            _state = state;
            map = null;
            if (PendingMap == null || previous != GameModeState.Ended || state == GameModeState.Ended)
            {
                return false;
            }

            map = PendingMap;
            PendingMap = null;
            return true;
        }
    }
}
