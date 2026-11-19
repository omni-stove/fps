using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 届いた StashStateMessage から、オフラインの Stash.Status と同じ StashStatus を作る。持ち物庫の広さは送られないので、手元の調整値 (StashSpec) の広さを使う。
    // オフラインと同じく、広さを越えた山も黙って消さず、山の数を広さとして見せる
    public sealed class ClientStash
    {
        private readonly int _capacity;
        private readonly List<StashEntry> _stacks = new List<StashEntry>();

        public ClientStash(int capacity = StashSpec.DefaultCapacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "capacity must be at least 1");
            }

            _capacity = capacity;
        }

        // まだ届いていなければ null
        public StashStatus? Status { get; private set; }

        // 最後に届いたうち、持ち物の Id として読めず見せなかった山の数。知らせるかは使う側が決める
        public int SkippedStacks { get; private set; }

        // Status が変わったら true
        public bool Receive(StashStateMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            _stacks.Clear();
            SkippedStacks = 0;
            var stash = message.Stash;
            for (var i = 0; i < stash.Count; i++)
            {
                if (TryEntry(stash.IdAt(i), stash.CountAt(i), out var entry))
                {
                    _stacks.Add(entry);
                }
                else
                {
                    SkippedStacks++;
                }
            }

            var status = new StashStatus(Math.Max(_capacity, _stacks.Count), _stacks);
            if (Status.HasValue && Status.Value == status)
            {
                return false;
            }

            Status = status;
            return true;
        }

        // 試合を離れたときに呼ぶ。消したら true
        public bool Clear()
        {
            SkippedStacks = 0;
            _stacks.Clear();
            if (!Status.HasValue)
            {
                return false;
            }

            Status = null;
            return true;
        }

        // 持ち物の Id は小文字の UUID に限られ (StashEntry)、サーバーの持ち物庫のファイルが壊れていても、その山を除いて残りを見せる
        private static bool TryEntry(string itemId, int count, out StashEntry entry)
        {
            try
            {
                entry = new StashEntry(itemId, count);
                return true;
            }
            catch (ArgumentException)
            {
                entry = null;
                return false;
            }
        }
    }
}
