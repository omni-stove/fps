using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class DownedTracker
    {
        // playerId の昇順に回し、同じ入力なら同じ結果になるようにする。
        private readonly SortedDictionary<int, Entry> _entries = new SortedDictionary<int, Entry>();

        public int DownedHealth { get; }

        public float BleedoutSeconds { get; }

        public float ReviveSeconds { get; }

        public int Count => _entries.Count;

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
            // (decimal)fraction の変換は実行環境によって 0.30000001... を残すことがあるので、
            // 最短の往復表記 ("0.3") を経由して、書いたとおりの値で掛ける。
            var exact = decimal.Parse(
                fraction.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture);
            var health = (int)Math.Ceiling(maxHealth * exact);
            return Math.Max(1, Math.Min(maxHealth, health));
        }

        public bool IsDowned(int playerId) => _entries.ContainsKey(playerId);

        public IReadOnlyList<int> GetDownedIds() => new List<int>(_entries.Keys);

        public int GetHealth(int playerId) => _entries.TryGetValue(playerId, out var entry) ? entry.Health : 0;

        public float? GetBleedoutRemaining(int playerId) =>
            _entries.TryGetValue(playerId, out var entry) ? Math.Max(0f, entry.BleedoutRemaining) : (float?)null;

        public float GetReviveProgress(int playerId) =>
            _entries.TryGetValue(playerId, out var entry) ? Math.Min(1f, entry.ReviveSeconds / ReviveSeconds) : 0f;

        public int? GetDownerId(int playerId) => _entries.TryGetValue(playerId, out var entry) ? entry.DownerId : null;

        public int? GetBleedoutCreditId(int playerId)
        {
            if (!_entries.TryGetValue(playerId, out var entry))
            {
                return null;
            }

            return entry.DownedByEnemy && entry.DownerId.HasValue ? entry.DownerId : entry.LastEnemyId;
        }

        public int? GetLastEnemyId(int playerId) => _entries.TryGetValue(playerId, out var entry) ? entry.LastEnemyId : null;

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

        // 前の Tick から蘇生を進めなかった人の累計を 0 に戻し、押すのをやめたら最初からにする。
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

        public bool Remove(int playerId) => _entries.Remove(playerId);

        // 他の人のダウンさせた人・最後に当てた敵からも外すのは、抜けた人にキルを付けないため。
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

            public float BleedoutRemaining { get; set; }

            public float ReviveSeconds { get; set; }

            public int? ReviverId { get; set; }

            public bool ReviveTouched { get; set; }

            public int? DownerId { get; set; }

            public bool DownedByEnemy { get; set; }

            public int? LastEnemyId { get; set; }
        }
    }
}
