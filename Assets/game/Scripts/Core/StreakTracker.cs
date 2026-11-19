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

        private string _selected;

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

        // 使える報酬が無ければ null。
        public string SelectedRewardId => _selected;

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

                // 選び直した報酬を、後から来た報酬で勝手に替えない。
                _selected ??= reward.Id;
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

        // 選んでいる報酬を使う。
        public StreakStatus Use(out string rewardId)
        {
            rewardId = _selected;
            if (rewardId == null)
            {
                return Status;
            }

            return Use(rewardId, out _);
        }

        public StreakStatus Use(string rewardId, out bool used)
        {
            used = rewardId != null && _ready.Remove(rewardId);
            if (!used)
            {
                return Status;
            }

            if (string.Equals(_selected, rewardId, StringComparison.Ordinal))
            {
                _selected = FirstReadyInSlotOrder();
            }

            Status = BuildStatus();
            return Status;
        }

        public bool IsReady(string rewardId)
        {
            return rewardId != null && _ready.Contains(rewardId);
        }

        // 枠の並び (HUD の行の並び) で次の使える報酬へ移り、最後の次は先頭に戻る。使える報酬が 1 つ以下なら替えない。
        public bool SelectNext()
        {
            if (_ready.Count < 2)
            {
                return false;
            }

            var rewards = Spec.Rewards;
            var current = -1;
            for (var i = 0; i < rewards.Count; i++)
            {
                if (string.Equals(rewards[i].Id, _selected, StringComparison.Ordinal))
                {
                    current = i;
                    break;
                }
            }

            for (var step = 1; step <= rewards.Count; step++)
            {
                var candidate = rewards[(current + step + rewards.Count) % rewards.Count].Id;
                if (_ready.Contains(candidate) && !string.Equals(candidate, _selected, StringComparison.Ordinal))
                {
                    _selected = candidate;
                    Status = BuildStatus();
                    return true;
                }
            }

            return false;
        }

        public bool Select(string rewardId)
        {
            if (!IsReady(rewardId) || string.Equals(rewardId, _selected, StringComparison.Ordinal))
            {
                return false;
            }

            _selected = rewardId;
            Status = BuildStatus();
            return true;
        }

        public StreakStatus Reset()
        {
            Kills = 0;
            _earnedThisLife.Clear();
            _ready.Clear();
            _selected = null;
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

            return new StreakStatus(Kills, slots, _selected);
        }

        private string FirstReadyInSlotOrder()
        {
            foreach (var reward in Spec.Rewards)
            {
                if (_ready.Contains(reward.Id))
                {
                    return reward.Id;
                }
            }

            return null;
        }
    }
}
