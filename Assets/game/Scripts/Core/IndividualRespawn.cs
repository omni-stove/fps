using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>倒された人ごとに、倒されてから <see cref="RespawnSeconds"/> 後に戻す。0 秒なら倒されたその場で戻す。</summary>
    public sealed class IndividualRespawn : IRespawnPolicy
    {
        // 抜けてすぐ入り直した人が前の待ちを引き継がないよう、待ちごとに別のオブジェクトにして同じものかを比べる。
        private readonly Dictionary<int, Waiter> _waiters = new Dictionary<int, Waiter>();

        /// <summary>倒されてから戻るまでの秒数。0 以上の有限値。</summary>
        public float RespawnSeconds { get; }

        /// <param name="respawnSeconds">倒されてから戻るまでの秒数。0 以上の有限値。</param>
        public IndividualRespawn(float respawnSeconds)
        {
            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must not be negative and must be finite");
            }

            RespawnSeconds = respawnSeconds;
        }

        public float? SecondsUntilNextWave => null;

        public bool WillReturn(ModeSession session, int playerId) => true;

        public bool BeginWait(ModeSession session, int playerId)
        {
            if (RespawnSeconds == 0f)
            {
                _waiters.Remove(playerId);
                return true;
            }

            _waiters[playerId] = new Waiter(RespawnSeconds);
            return false;
        }

        public void Cancel(int playerId) => _waiters.Remove(playerId);

        public void CancelAll() => _waiters.Clear();

        public void Reset() => _waiters.Clear();

        public float? GetRemaining(int playerId)
        {
            return _waiters.TryGetValue(playerId, out var waiter) ? Math.Max(0f, waiter.Remaining) : (float?)null;
        }

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

            List<KeyValuePair<int, Waiter>> ready = null;
            foreach (var pair in _waiters)
            {
                pair.Value.Remaining -= elapsedSeconds;
                if (pair.Value.Remaining <= 0f)
                {
                    (ready ??= new List<KeyValuePair<int, Waiter>>()).Add(pair);
                }
            }

            if (ready == null)
            {
                return;
            }

            Waiter.SortByWaitedLongest(ready);
            foreach (var pair in ready)
            {
                if (session.State != GameModeState.Playing)
                {
                    return;
                }

                if (_waiters.TryGetValue(pair.Key, out var current)
                    && ReferenceEquals(current, pair.Value)
                    && current.Remaining <= 0f)
                {
                    _waiters.Remove(pair.Key);
                    respawn(pair.Key);
                }
            }
        }

        internal sealed class Waiter
        {
            /// <summary>倒されてから RespawnSeconds たつまでの残り秒数。過ぎると負になる。</summary>
            public float Remaining { get; set; }

            public Waiter(float remaining)
            {
                Remaining = remaining;
            }

            /// <summary>早く待ち終わった順 (同時なら playerId 順) に並べ、呼び出し順を決まったものにする。</summary>
            public static void SortByWaitedLongest(List<KeyValuePair<int, Waiter>> ready)
            {
                ready.Sort((a, b) =>
                {
                    var byTime = a.Value.Remaining.CompareTo(b.Value.Remaining);
                    return byTime != 0 ? byTime : a.Key.CompareTo(b.Key);
                });
            }
        }
    }
}
