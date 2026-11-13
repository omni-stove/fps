using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// プレイヤーごとに、入力が無いまま経った時間を数え、残り <see cref="WarningSeconds"/> 秒で警告、0 で追い出しを知らせる
    /// (<see cref="GameRules.IdleKickSeconds"/>)。入力の有無や試合の状態は知らず、呼び出し側が <see cref="Touch"/>・
    /// <see cref="SetCounting"/>・<see cref="Advance"/> で伝える。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>数えない間 (ダウン中・死亡中・試合の合間) は残りを止めるだけで戻さない。倒されるたびに時計が戻ると、
    /// 放置して撃たれ続ける人がいつまでも残るため。</item>
    /// <item>警告は「残りが <see cref="WarningSeconds"/> 以下」かつ「数えている」間だけ出ている。数えるのをやめたら消し
    /// (<see cref="IdleChange.WarningCleared"/>)、残りが警告の範囲のまま数え直したら出し直す (<see cref="IdleChange.Warned"/>)。</item>
    /// <item><see cref="KickSeconds"/> が <see cref="WarningSeconds"/> 以下なら、数え始めた最初の <see cref="Advance"/> で警告する。</item>
    /// </list>
    /// </remarks>
    public sealed class IdleTracker
    {
        /// <summary>警告を出す残り秒数の既定値。</summary>
        public const float DefaultWarningSeconds = 15f;

        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        /// <summary>入力が無ければ追い出すまでの秒数。0 より大きい有限値。</summary>
        public float KickSeconds { get; }

        /// <summary>残りがこの秒数以下になったら警告する。0 より大きい有限値。</summary>
        public float WarningSeconds { get; }

        /// <summary>数えているプレイヤーの数。</summary>
        public int Count => _entries.Count;

        /// <summary>数えているプレイヤーの Id。並びは決まっていない。</summary>
        public IEnumerable<int> PlayerIds => _entries.Keys;

        /// <param name="kickSeconds">追い出すまでの秒数。0 より大きい有限値 (0 で切らないルールでは作らない)。</param>
        /// <param name="warningSeconds">警告を出す残り秒数。0 より大きい有限値。</param>
        public IdleTracker(float kickSeconds, float warningSeconds = DefaultWarningSeconds)
        {
            if (!(kickSeconds > 0f) || float.IsInfinity(kickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(kickSeconds), kickSeconds, "kickSeconds must be positive and finite");
            }

            if (!(warningSeconds > 0f) || float.IsInfinity(warningSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(warningSeconds), warningSeconds, "warningSeconds must be positive and finite");
            }

            KickSeconds = kickSeconds;
            WarningSeconds = warningSeconds;
        }

        /// <summary>playerId を残り <see cref="KickSeconds"/>、数えている状態で加える。</summary>
        /// <exception cref="ArgumentException">もう加えてある。</exception>
        public void Add(int playerId)
        {
            if (_entries.ContainsKey(playerId))
            {
                throw new ArgumentException("player " + playerId + " is already tracked", nameof(playerId));
            }

            _entries.Add(playerId, new Entry(KickSeconds));
        }

        /// <summary>playerId を外す。加えてあれば true。</summary>
        public bool Remove(int playerId)
        {
            return _entries.Remove(playerId);
        }

        /// <summary>playerId を数えているか。</summary>
        public bool Contains(int playerId)
        {
            return _entries.ContainsKey(playerId);
        }

        /// <summary>
        /// playerId に入力があった。残りを <see cref="KickSeconds"/> に戻す。警告を出していれば <see cref="IdleChange.WarningCleared"/>。
        /// 加えていない・もう追い出した人は何もしない。
        /// </summary>
        public IdleChange Touch(int playerId)
        {
            if (!_entries.TryGetValue(playerId, out var entry) || entry.Kicked)
            {
                return IdleChange.None;
            }

            entry.Remaining = KickSeconds;
            if (!entry.Warned)
            {
                return IdleChange.None;
            }

            entry.Warned = false;
            return IdleChange.WarningCleared;
        }

        /// <summary>
        /// playerId の時間を数えるかを決める。やめるときに警告を出していれば <see cref="IdleChange.WarningCleared"/>、
        /// 数え直すときに残りが警告の範囲なら <see cref="IdleChange.Warned"/>。状態が変わらなければ <see cref="IdleChange.None"/>。
        /// 加えていない・もう追い出した人は何もしない。
        /// </summary>
        public IdleChange SetCounting(int playerId, bool counting)
        {
            if (!_entries.TryGetValue(playerId, out var entry) || entry.Kicked || entry.Counting == counting)
            {
                return IdleChange.None;
            }

            entry.Counting = counting;
            if (!counting)
            {
                if (!entry.Warned)
                {
                    return IdleChange.None;
                }

                entry.Warned = false;
                return IdleChange.WarningCleared;
            }

            if (entry.Remaining > WarningSeconds)
            {
                return IdleChange.None;
            }

            entry.Warned = true;
            return IdleChange.Warned;
        }

        /// <summary>
        /// 数えている playerId の残りを deltaSeconds 減らす。0 になれば <see cref="IdleChange.Kicked"/> (以後この人は何も返さない)、
        /// 警告の範囲に入れば <see cref="IdleChange.Warned"/>。数えていない・加えていない人は何もしない。
        /// </summary>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
        public IdleChange Advance(int playerId, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (!_entries.TryGetValue(playerId, out var entry) || entry.Kicked || !entry.Counting)
            {
                return IdleChange.None;
            }

            entry.Remaining = Math.Max(0f, entry.Remaining - deltaSeconds);
            if (entry.Remaining <= 0f)
            {
                entry.Kicked = true;
                entry.Warned = false;
                return IdleChange.Kicked;
            }

            if (entry.Warned || entry.Remaining > WarningSeconds)
            {
                return IdleChange.None;
            }

            entry.Warned = true;
            return IdleChange.Warned;
        }

        /// <summary>playerId が追い出されるまでの残り秒数。加えていなければ null。</summary>
        public float? GetRemaining(int playerId)
        {
            return _entries.TryGetValue(playerId, out var entry) ? entry.Remaining : (float?)null;
        }

        /// <summary>playerId に警告を出しているか。</summary>
        public bool IsWarned(int playerId)
        {
            return _entries.TryGetValue(playerId, out var entry) && entry.Warned;
        }

        /// <summary>playerId の時間を数えているか。加えていない・追い出した人は false。</summary>
        public bool IsCounting(int playerId)
        {
            return _entries.TryGetValue(playerId, out var entry) && entry.Counting && !entry.Kicked;
        }

        /// <summary>playerId を追い出したか (<see cref="IdleChange.Kicked"/> を返した後か)。</summary>
        public bool IsKicked(int playerId)
        {
            return _entries.TryGetValue(playerId, out var entry) && entry.Kicked;
        }

        private sealed class Entry
        {
            public Entry(float remaining)
            {
                Remaining = remaining;
                Counting = true;
            }

            public float Remaining { get; set; }

            public bool Counting { get; set; }

            /// <summary>警告を出していて、まだ消していない。</summary>
            public bool Warned { get; set; }

            public bool Kicked { get; set; }
        }
    }
}
