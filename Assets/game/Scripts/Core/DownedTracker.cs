using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ダウン中のプレイヤーごとに、ダウン中の体力・出血の残り秒数・蘇生の累計・誰に当てられたかを持つ。
    /// チームや試合の状態は知らず、<see cref="TeamDeathmatchSession"/> が検査してから呼ぶ。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>蘇生の累計は <see cref="AddReviveProgress"/> で進める。前の <see cref="Tick"/> から次の <see cref="Tick"/> までに
    /// 1 度も進めなかった人の累計は、その <see cref="Tick"/> で 0 に戻す (押すのをやめたら最初から)。</item>
    /// <item>同じ Tick の間に起こせるのは 1 人だけ。先に進めた人がその Tick の起こす人になり、他の人は <see cref="ReviveStep.Rejected"/>。
    /// 起こす人が Tick をまたいで替わったら、累計は 0 から数え直す。</item>
    /// <item>playerId の昇順に回すので、同じ入力なら同じ結果になる。</item>
    /// </list>
    /// </remarks>
    public sealed class DownedTracker
    {
        private readonly SortedDictionary<int, Entry> _entries = new SortedDictionary<int, Entry>();

        /// <summary>ダウンしたときの体力。1 以上。</summary>
        public int DownedHealth { get; }

        /// <summary>ダウンしてから出血で倒れるまでの秒数。</summary>
        public float BleedoutSeconds { get; }

        /// <summary>起き上がるのに要る蘇生の累計秒数。</summary>
        public float ReviveSeconds { get; }

        /// <summary>ダウン中の人数。</summary>
        public int Count => _entries.Count;

        /// <param name="downedHealth">ダウンしたときの体力。1 以上。</param>
        /// <param name="bleedoutSeconds">出血で倒れるまでの秒数。0 より大きい有限値。</param>
        /// <param name="reviveSeconds">蘇生に要る秒数。0 より大きい有限値。</param>
        public DownedTracker(int downedHealth, float bleedoutSeconds, float reviveSeconds)
        {
            if (downedHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(downedHealth), downedHealth, "downedHealth must be positive");
            }

            if (!(bleedoutSeconds > 0f) || float.IsInfinity(bleedoutSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(bleedoutSeconds), bleedoutSeconds, "bleedoutSeconds must be positive and finite");
            }

            if (!(reviveSeconds > 0f) || float.IsInfinity(reviveSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(reviveSeconds), reviveSeconds, "reviveSeconds must be positive and finite");
            }

            DownedHealth = downedHealth;
            BleedoutSeconds = bleedoutSeconds;
            ReviveSeconds = reviveSeconds;
        }

        /// <summary>
        /// maxHealth に fraction を掛けて切り上げた体力 (1 以上 maxHealth 以下)。
        /// </summary>
        /// <param name="maxHealth">最大体力。1 以上。</param>
        /// <param name="fraction">割合。0 より大きく 1 以下。</param>
        public static int HealthFor(int maxHealth, float fraction)
        {
            if (maxHealth <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be positive");
            }

            if (!(fraction > 0f && fraction <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(fraction), fraction, "fraction must be greater than 0 and at most 1");
            }

            // float のまま掛けると 0.3f が 0.30000001 なので 100 * 0.3f が 31 に切り上がる。
            // decimal への変換は有効桁 7 桁に丸めるので、書いたとおりの 0.3 で掛けられる。
            var health = (int)Math.Ceiling(maxHealth * (decimal)fraction);
            return Math.Max(1, Math.Min(maxHealth, health));
        }

        /// <summary>ダウン中なら true。</summary>
        public bool IsDowned(int playerId) => _entries.ContainsKey(playerId);

        /// <summary>ダウン中の人の id を昇順で返す。</summary>
        public IReadOnlyList<int> GetDownedIds() => new List<int>(_entries.Keys);

        /// <summary>ダウン中の体力。ダウンしていなければ 0。</summary>
        public int GetHealth(int playerId) => _entries.TryGetValue(playerId, out var entry) ? entry.Health : 0;

        /// <summary>出血で倒れるまでの残り秒数。ダウンしていなければ null。</summary>
        public float? GetBleedoutRemaining(int playerId) =>
            _entries.TryGetValue(playerId, out var entry) ? Math.Max(0f, entry.BleedoutRemaining) : (float?)null;

        /// <summary>蘇生の進み具合 (0 以上 1 以下)。ダウンしていなければ 0。</summary>
        public float GetReviveProgress(int playerId) =>
            _entries.TryGetValue(playerId, out var entry) ? Math.Min(1f, entry.ReviveSeconds / ReviveSeconds) : 0f;

        /// <summary>ダウンさせた人。抜けていたら null。ダウンしていなければ null。</summary>
        public int? GetDownerId(int playerId) => _entries.TryGetValue(playerId, out var entry) ? entry.DownerId : null;

        /// <summary>
        /// 出血など、とどめを刺した人がいないときにキルを付ける敵。ダウンさせたのが敵ならその人、
        /// そうでなければダウン中に最後に当てた敵。どちらもいない (抜けた) なら null。
        /// </summary>
        public int? GetBleedoutCreditId(int playerId)
        {
            if (!_entries.TryGetValue(playerId, out var entry))
            {
                return null;
            }

            return entry.DownedByEnemy && entry.DownerId.HasValue ? entry.DownerId : entry.LastEnemyId;
        }

        /// <summary>ダウンさせた敵を含めて、最後に当てた敵。いない (抜けた) なら null。ダウンしていなければ null。</summary>
        public int? GetLastEnemyId(int playerId) => _entries.TryGetValue(playerId, out var entry) ? entry.LastEnemyId : null;

        /// <summary>playerId をダウンさせる。体力は <see cref="DownedHealth"/>、出血は <see cref="BleedoutSeconds"/> から数える。</summary>
        /// <param name="playerId">ダウンする人。</param>
        /// <param name="downerId">ダウンさせた人。</param>
        /// <param name="byEnemy">downerId が敵なら true。</param>
        /// <exception cref="InvalidOperationException">すでにダウンしている。</exception>
        public void Down(int playerId, int downerId, bool byEnemy)
        {
            if (_entries.ContainsKey(playerId))
            {
                throw new InvalidOperationException($"player {playerId} is already downed");
            }

            _entries[playerId] = new Entry
            {
                Health = DownedHealth,
                BleedoutRemaining = BleedoutSeconds,
                DownerId = downerId,
                DownedByEnemy = byEnemy,
                LastEnemyId = byEnemy ? downerId : (int?)null,
            };
        }

        /// <summary>ダウン中の体力を amount 減らす。</summary>
        /// <param name="playerId">ダウン中の人。</param>
        /// <param name="attackerId">当てた人。</param>
        /// <param name="byEnemy">attackerId が敵なら true。最後に当てた敵を覚え直す。</param>
        /// <param name="amount">ダメージ。0 以上。</param>
        /// <returns>減らした後の体力。</returns>
        /// <exception cref="InvalidOperationException">ダウンしていない。</exception>
        public int TakeDamage(int playerId, int attackerId, bool byEnemy, int amount)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(amount), amount, "amount must not be negative");
            }

            var entry = Find(playerId);
            entry.Health = Math.Max(0, entry.Health - amount);
            if (byEnemy)
            {
                entry.LastEnemyId = attackerId;
            }

            return entry.Health;
        }

        /// <summary>
        /// reviverId が targetId を deltaSeconds だけ起こす。累計が <see cref="ReviveSeconds"/> に達したら
        /// <see cref="ReviveStep.Completed"/> を返す (ダウンの状態は消さないので、呼んだ側が <see cref="Remove"/> する)。
        /// </summary>
        /// <param name="targetId">起こされる人。ダウンしていなければ <see cref="ReviveStep.Rejected"/>。</param>
        /// <param name="reviverId">起こす人。</param>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
        public ReviveStep AddReviveProgress(int targetId, int reviverId, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            if (!_entries.TryGetValue(targetId, out var entry))
            {
                return ReviveStep.Rejected;
            }

            if (entry.ReviverId != reviverId)
            {
                if (entry.ReviveTouched)
                {
                    return ReviveStep.Rejected;
                }

                entry.ReviverId = reviverId;
                entry.ReviveSeconds = 0f;
            }

            entry.ReviveTouched = true;
            entry.ReviveSeconds += deltaSeconds;
            return entry.ReviveSeconds >= ReviveSeconds ? ReviveStep.Completed : ReviveStep.Progressed;
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。前の Tick から蘇生を進めなかった人の累計を 0 に戻し、出血の残りを減らす。
        /// </summary>
        /// <param name="deltaSeconds">進める秒数。0 以上。</param>
        /// <returns>出血の残りが 0 になった人の id (昇順)。ダウンの状態は消さないので、呼んだ側が倒して <see cref="Remove"/> する。</returns>
        public IReadOnlyList<int> Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative");
            }

            List<int> bledOut = null;
            foreach (var pair in _entries)
            {
                var entry = pair.Value;
                if (!entry.ReviveTouched)
                {
                    entry.ReviverId = null;
                    entry.ReviveSeconds = 0f;
                }

                entry.ReviveTouched = false;
                entry.BleedoutRemaining -= deltaSeconds;
                if (entry.BleedoutRemaining <= 0f)
                {
                    (bledOut ??= new List<int>()).Add(pair.Key);
                }
            }

            return bledOut ?? (IReadOnlyList<int>)Array.Empty<int>();
        }

        /// <summary>playerId のダウンの状態を消す。ダウンしていたら true。</summary>
        public bool Remove(int playerId) => _entries.Remove(playerId);

        /// <summary>
        /// playerId が抜けたときに呼ぶ。本人のダウンの状態を消し、他の人のダウンさせた人・最後に当てた敵・起こす人からも外す
        /// (抜けた人にキルを付けないため)。本人がダウンしていたら true。
        /// </summary>
        public bool RemovePlayer(int playerId)
        {
            var removed = _entries.Remove(playerId);
            foreach (var entry in _entries.Values)
            {
                if (entry.DownerId == playerId)
                {
                    entry.DownerId = null;
                }

                if (entry.LastEnemyId == playerId)
                {
                    entry.LastEnemyId = null;
                }

                if (entry.ReviverId == playerId)
                {
                    entry.ReviverId = null;
                    entry.ReviveSeconds = 0f;
                    entry.ReviveTouched = false;
                }
            }

            return removed;
        }

        /// <summary>全員のダウンの状態を消す。</summary>
        public void Clear() => _entries.Clear();

        private Entry Find(int playerId)
        {
            if (!_entries.TryGetValue(playerId, out var entry))
            {
                throw new InvalidOperationException($"player {playerId} is not downed");
            }

            return entry;
        }

        private sealed class Entry
        {
            public int Health { get; set; }

            /// <summary>過ぎると負になる。</summary>
            public float BleedoutRemaining { get; set; }

            public float ReviveSeconds { get; set; }

            public int? ReviverId { get; set; }

            /// <summary>前の Tick から蘇生を進めたら true。</summary>
            public bool ReviveTouched { get; set; }

            public int? DownerId { get; set; }

            public bool DownedByEnemy { get; set; }

            public int? LastEnemyId { get; set; }
        }
    }
}
