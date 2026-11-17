using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class HordeRespawn : IRespawnPolicy
    {
        private readonly WaveSchedule _schedule;

        // 戻す順が Tick ごとに変わらないよう、playerId の昇順に持つ。
        private readonly SortedSet<int> _waiting = new SortedSet<int>();

        public HordeRespawn(WaveSchedule schedule)
        {
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        }

        public bool IsBetweenWaves => IsBetween(_schedule.Phase);

        public int WaitingCount => _waiting.Count;

        // まとめて戻すのは時刻ではなく波の終わりなので、秒数は出せない。
        public float? SecondsUntilNextWave => null;

        public static bool IsBetween(WavePhase phase)
        {
            return phase == WavePhase.Resting || phase == WavePhase.Waiting;
        }

        public bool WillReturn(ModeSession session, int playerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (IsBetweenWaves)
            {
                return true;
            }

            var side = session.GetSide(playerId);
            foreach (var other in session.GetPlayerIds())
            {
                if (other != playerId && session.GetSide(other) == side && !session.IsDown(other))
                {
                    return true;
                }
            }

            return false;
        }

        public bool BeginWait(ModeSession session, int playerId)
        {
            // 波の間に倒れても、ダメージの処理の途中では戻さず次の Tick で戻す。
            _waiting.Add(playerId);
            return false;
        }

        public void Cancel(int playerId) => _waiting.Remove(playerId);

        public void CancelAll() => _waiting.Clear();

        public void Reset() => _waiting.Clear();

        public void Tick(ModeSession session, float elapsedSeconds, Action<int> respawn)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (respawn == null)
            {
                throw new ArgumentNullException(nameof(respawn));
            }

            if (!IsBetweenWaves || _waiting.Count == 0)
            {
                return;
            }

            var ready = new int[_waiting.Count];
            _waiting.CopyTo(ready);
            foreach (var playerId in ready)
            {
                if (session.State != GameModeState.Playing || !IsBetweenWaves)
                {
                    return;
                }

                if (_waiting.Remove(playerId))
                {
                    respawn(playerId);
                }
            }
        }

        // 戦っている間に null を返すのは、戻るまでの時間が敵の数で決まり秒数では分からないため。
        public float? GetRemaining(int playerId)
        {
            return _waiting.Contains(playerId) && IsBetweenWaves ? 0f : (float?)null;
        }
    }
}
