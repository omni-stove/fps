using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Server.Eos;

namespace Shooter.Server
{
    // プレイヤー Id でなく ProductUserId で数えるのは、抜けてつなぎ直すと Id が変わっても同じ人の数を 1 つにまとめるため。
    internal sealed class ServerMatchStats
    {
        private static readonly int _statCount = Enum.GetValues(typeof(PlayerStat)).Length;

        // EOS の ProductUserId は 16 進なので大文字と小文字を区別しない
        private readonly Dictionary<string, long[]> _byUser = new Dictionary<string, long[]>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _order = new List<string>();

        public IReadOnlyList<string> Users => _order;

        public void Clear()
        {
            _byUser.Clear();
            _order.Clear();
        }

        // 数がまだ無い人も、試合に出た人として送る相手に入れる
        public void Join(string userId)
        {
            Counts(userId);
        }

        public void Add(string userId, PlayerStat stat, long amount = 1)
        {
            var counts = Counts(userId);
            if (counts != null)
            {
                counts[(int)stat] += amount;
            }
        }

        public long Get(string userId, PlayerStat stat)
        {
            return userId != null && _byUser.TryGetValue(userId, out var counts) ? counts[(int)stat] : 0;
        }

        // 自滅と味方撃ちはキルに数えず、倒された人の死には数える (TeamDeathmatchSession の数え方と同じ)
        public void RecordKill(string killerId, string victimId, IReadOnlyList<string> assisterIds, bool headshot, bool friendly, bool self)
        {
            Add(victimId, PlayerStat.Deaths);
            if (self || friendly)
            {
                return;
            }

            Add(killerId, PlayerStat.Kills);
            if (headshot)
            {
                Add(killerId, PlayerStat.Headshots);
            }

            if (assisterIds == null)
            {
                return;
            }

            foreach (var assister in assisterIds)
            {
                Add(assister, PlayerStat.Assists);
            }
        }

        public IReadOnlyList<StatIngest> Build(LeaderboardSpec spec, string userId)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return Build(spec, stat => Get(userId, stat));
        }

        // 合計 (Sum) の 0 は足しても変わらないので送らない。最大・最小・最新は 0 も値なので送る。
        // 同じ統計の名前を 2 つのボードが使うときは、先のボードの値を 1 回だけ送る。
        public static IReadOnlyList<StatIngest> Build(LeaderboardSpec spec, Func<PlayerStat, long> valueOf)
        {
            ArgumentNullException.ThrowIfNull(spec);
            ArgumentNullException.ThrowIfNull(valueOf);
            var result = new List<StatIngest>(spec.Boards.Count);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var board in spec.Boards)
            {
                if (!seen.Add(board.StatName))
                {
                    continue;
                }

                var value = valueOf(board.Stat);
                if (value == 0 && board.Aggregation == LeaderboardAggregation.Sum)
                {
                    continue;
                }

                result.Add(new StatIngest(board.StatName, StatIngest.Clamp(value)));
            }

            return result;
        }

        public static IReadOnlyList<LeaderboardDefinition> DuplicateStatNames(LeaderboardSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var duplicates = new List<LeaderboardDefinition>();
            foreach (var board in spec.Boards)
            {
                if (!seen.Add(board.StatName))
                {
                    duplicates.Add(board);
                }
            }

            return duplicates;
        }

        private long[] Counts(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return null;
            }

            if (!_byUser.TryGetValue(userId, out var counts))
            {
                counts = new long[_statCount];
                _byUser.Add(userId, counts);
                _order.Add(userId);
            }

            return counts;
        }
    }
}
