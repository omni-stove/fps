using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ホードのリスポーン。倒れた人は波 (<see cref="WaveSchedule"/>) を越えるまで待ち、波の間 (休憩中) になったらまとめて戻る。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>戦っている間に倒れた人は、同じ陣営に倒れていない人が 1 人でもいれば戻る見込みがある (<see cref="WillReturn"/>)。
    /// その人が波を越えれば戻れるため。最後の 1 人が倒れたら、陣営の全員がその場で戻れなくなる (陣営ごと脱落する)。</item>
    /// <item>波の間 (と最初の波の前) に倒れた人は、次の <see cref="Tick"/> で戻る。</item>
    /// <item>戻るまでの秒数は、波の間なら 0、戦っている間は敵の数で決まるので分からない (<see cref="GetRemaining"/> は null)。</item>
    /// </list>
    /// </remarks>
    public sealed class HordeRespawn : IRespawnPolicy
    {
        private readonly WaveSchedule _schedule;

        // 戻す順が Tick ごとに変わらないよう、playerId の昇順に持つ。
        private readonly SortedSet<int> _waiting = new SortedSet<int>();

        /// <param name="schedule">波の予定。休憩中かどうかだけを読む。</param>
        public HordeRespawn(WaveSchedule schedule)
        {
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        }

        /// <summary>波の間 (休憩中か、最初の波の前) なら true。この間は倒れた人がすぐ戻る。</summary>
        public bool IsBetweenWaves => IsBetween(_schedule.Phase);

        /// <summary>波を越えるのを待っている人の数。</summary>
        public int WaitingCount => _waiting.Count;

        /// <summary>まとめて戻すのは時刻ではなく波の終わりなので、常に null。</summary>
        public float? SecondsUntilNextWave => null;

        /// <summary>phase が波の間 (休憩中か、最初の波の前) なら true。</summary>
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

        /// <summary>波の間なら 0。戦っている間と、待っていない人は null。</summary>
        public float? GetRemaining(int playerId)
        {
            return _waiting.Contains(playerId) && IsBetweenWaves ? 0f : (float?)null;
        }
    }
}
