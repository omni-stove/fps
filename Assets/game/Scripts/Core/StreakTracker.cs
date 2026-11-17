using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class StreakTracker
    {
        private static readonly IReadOnlyList<string> _noneEarned = Array.Empty<string>();

        private readonly List<string> _ready = new List<string>();

        // 得た報酬を使っても、同じ命のうちに数が届き直して二度得ることのないよう、使ったかどうかとは別に覚える。
        private readonly HashSet<string> _earnedThisLife = new HashSet<string>(StringComparer.Ordinal);

        public StreakTracker(StreakSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            ReadyRewardIds = _ready.AsReadOnly();
            Status = BuildStatus();
        }

        public StreakSpec Spec { get; }

        public int Kills { get; private set; }

        public StreakStatus Status { get; private set; }

        public IReadOnlyList<string> ReadyRewardIds { get; }

        public StreakStatus Killed()
        {
            return Killed(out _);
        }

        public StreakStatus Killed(out IReadOnlyList<string> earned)
        {
            Kills++;
            List<string> newly = null;
            foreach (var reward in Spec.Rewards)
            {
                if (Kills < reward.KillsRequired || _earnedThisLife.Contains(reward.Id))
                {
                    continue;
                }

                _earnedThisLife.Add(reward.Id);

                // 枠は報酬ごとに 1 つで、持っている数を表せない。持ったままの報酬は重ねずに今のものを残す。
                if (_ready.Contains(reward.Id))
                {
                    continue;
                }

                _ready.Add(reward.Id);
                newly ??= new List<string>();
                newly.Add(reward.Id);
            }

            earned = newly ?? _noneEarned;
            Status = BuildStatus();
            return Status;
        }

        public StreakStatus Died()
        {
            Kills = 0;
            _earnedThisLife.Clear();
            Status = BuildStatus();
            return Status;
        }

        public StreakStatus Use(out string rewardId)
        {
            if (_ready.Count == 0)
            {
                rewardId = null;
                return Status;
            }

            rewardId = _ready[0];
            _ready.RemoveAt(0);
            Status = BuildStatus();
            return Status;
        }

        public StreakStatus Reset()
        {
            Kills = 0;
            _earnedThisLife.Clear();
            _ready.Clear();
            Status = BuildStatus();
            return Status;
        }

        private StreakStatus BuildStatus()
        {
            var rewards = Spec.Rewards;
            var slots = new StreakSlot[rewards.Count];
            for (var i = 0; i < slots.Length; i++)
            {
                var reward = rewards[i];
                slots[i] = new StreakSlot(reward.Id, reward.DisplayNameKey, reward.KillsRequired, _ready.Contains(reward.Id));
            }

            return new StreakStatus(Kills, slots);
        }
    }
}
