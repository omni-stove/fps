using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class ExtractionSession
    {
        private readonly List<ItemCount> _carried = new List<ItemCount>();
        private readonly List<ItemCount> _stored = new List<ItemCount>();
        private readonly List<ItemCount> _lost = new List<ItemCount>();
        private readonly string[] _openPointIds;

        // 持ち込む物は出撃の時点で持ち物庫から出す。脱出で戻るまで持ち物庫に残すと、倒されても失わない。
        public ExtractionSession(ExtractionSpec spec, Stash stash, IReadOnlyList<string> openPointIds, IReadOnlyList<ItemCount> broughtIn = null)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Stash = stash ?? throw new ArgumentNullException(nameof(stash));
            _openPointIds = CheckPoints(openPointIds);

            var brought = broughtIn ?? Array.Empty<ItemCount>();
            if (!stash.TryTake(brought))
            {
                throw new ArgumentException("the stash does not hold everything in broughtIn", nameof(broughtIn));
            }

            var copy = new ItemCount[brought.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = brought[i];
                Merge(_carried, brought[i].ItemId, brought[i].Count);
            }

            BroughtIn = copy;
            Carried = _carried.AsReadOnly();
            Stored = _stored.AsReadOnly();
            Lost = _lost.AsReadOnly();
            OpenPointIds = Array.AsReadOnly(_openPointIds);
        }

        public event Action<ExtractionOutcome> Ended;

        public ExtractionSpec Spec { get; }

        public Stash Stash { get; }

        public IReadOnlyList<string> OpenPointIds { get; }

        public IReadOnlyList<ItemCount> BroughtIn { get; }

        public IReadOnlyList<ItemCount> Carried { get; }

        public IReadOnlyList<ItemCount> Stored { get; }

        public IReadOnlyList<ItemCount> Lost { get; }

        public ExtractionOutcome Outcome { get; private set; }

        public bool IsOver => Outcome != ExtractionOutcome.Raiding;

        public float ElapsedSeconds { get; private set; }

        public float RemainingSeconds => Math.Max(0f, Spec.RaidSeconds - ElapsedSeconds);

        public bool PointsOpen => ElapsedSeconds >= Spec.OpenAfterSeconds;

        public float SecondsUntilOpen => Math.Max(0f, Spec.OpenAfterSeconds - ElapsedSeconds);

        public string HoldingPointId { get; private set; }

        public float HoldSeconds { get; private set; }

        public float HoldProgress => Spec.ExtractHoldSeconds <= 0f ? (HoldingPointId != null ? 1f : 0f) : Math.Min(1f, HoldSeconds / Spec.ExtractHoldSeconds);

        // 地点の数より多くは開けないので、置いた地点が少なければその全部を開ける。
        public static IReadOnlyList<string> PickOpenPoints(ExtractionSpec spec, IReadOnlyList<string> pointIds, Random random)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            var order = CheckPoints(pointIds);
            var count = Math.Min(spec.OpenPoints, order.Length);
            for (var i = 0; i < count; i++)
            {
                var j = i + (int)(random.NextDouble() * (order.Length - i));
                var swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }

            var picked = new string[count];
            Array.Copy(order, picked, count);
            return picked;
        }

        public bool IsOpen(string pointId)
        {
            return pointId != null && PointsOpen && Array.IndexOf(_openPointIds, pointId) >= 0;
        }

        public void Pick(string itemId, int count)
        {
            ThrowIfOver();
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must be positive");
            }

            Merge(_carried, SpecGuard.LowercaseUuid(itemId, nameof(itemId)), count);
        }

        public int Drop(string itemId, int count)
        {
            ThrowIfOver();
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            for (var i = 0; i < _carried.Count; i++)
            {
                if (!string.Equals(_carried[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    continue;
                }

                var dropped = Math.Min(count, _carried[i].Count);
                if (dropped == _carried[i].Count)
                {
                    _carried.RemoveAt(i);
                }
                else
                {
                    _carried[i] = new ItemCount(itemId, _carried[i].Count - dropped);
                }

                return dropped;
            }

            return 0;
        }

        // 持ち物の本当の中身は Runtime の Inventory が持つので、使ったり捨てたりした後はそれで丸ごと置き換える。
        public void ReplaceCarried(IReadOnlyList<ItemCount> carried)
        {
            ThrowIfOver();
            if (carried == null)
            {
                throw new ArgumentNullException(nameof(carried));
            }

            var next = new List<ItemCount>();
            foreach (var item in carried)
            {
                if (item.ItemId == null)
                {
                    throw new ArgumentException("carried must not contain an empty count", nameof(carried));
                }

                Merge(next, SpecGuard.LowercaseUuid(item.ItemId, nameof(carried)), item.Count);
            }

            _carried.Clear();
            _carried.AddRange(next);
        }

        public ExtractionOutcome Tick(float deltaSeconds, string standingPointId)
        {
            SpecGuard.NonNegativeFinite(deltaSeconds, nameof(deltaSeconds));
            if (IsOver)
            {
                return Outcome;
            }

            var before = ElapsedSeconds;
            ElapsedSeconds = Math.Min(Spec.RaidSeconds, before + deltaSeconds);
            if (IsOpen(standingPointId))
            {
                // 開く前に立っていた分まで数えると、開いた瞬間に脱出できてしまう。
                var openFor = ElapsedSeconds - Math.Max(before, Spec.OpenAfterSeconds);
                HoldSeconds = string.Equals(HoldingPointId, standingPointId, StringComparison.Ordinal) ? HoldSeconds + openFor : openFor;
                HoldingPointId = standingPointId;
                if (HoldSeconds >= Spec.ExtractHoldSeconds)
                {
                    End(ExtractionOutcome.Extracted);
                    return Outcome;
                }
            }
            else
            {
                HoldingPointId = null;
                HoldSeconds = 0f;
            }

            if (ElapsedSeconds >= Spec.RaidSeconds)
            {
                End(ExtractionOutcome.TimedOut);
            }

            return Outcome;
        }

        public void Die()
        {
            if (!IsOver)
            {
                End(ExtractionOutcome.Died);
            }
        }

        private void End(ExtractionOutcome outcome)
        {
            Outcome = outcome;
            HoldingPointId = null;
            HoldSeconds = 0f;
            var keep = outcome == ExtractionOutcome.Extracted || (outcome == ExtractionOutcome.Died && Spec.KeepCarriedOnDeath);
            foreach (var item in _carried)
            {
                var stored = keep ? Stash.Add(item.ItemId, item.Count) : 0;
                if (stored > 0)
                {
                    _stored.Add(new ItemCount(item.ItemId, stored));
                }

                // 持ち物庫が一杯で入り切らなかった分も、持ち帰れなかった物として失う。
                if (item.Count - stored > 0)
                {
                    _lost.Add(new ItemCount(item.ItemId, item.Count - stored));
                }
            }

            _carried.Clear();
            Ended?.Invoke(outcome);
        }

        private void ThrowIfOver()
        {
            if (IsOver)
            {
                throw new InvalidOperationException("the raid has already ended (" + Outcome + ")");
            }
        }

        private static string[] CheckPoints(IReadOnlyList<string> pointIds)
        {
            if (pointIds == null)
            {
                throw new ArgumentNullException(nameof(pointIds));
            }

            var copy = new string[pointIds.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                var id = pointIds[i];
                if (string.IsNullOrEmpty(id))
                {
                    throw new ArgumentException("point ids must not be null or empty", nameof(pointIds));
                }

                if (Array.IndexOf(copy, id, 0, i) >= 0)
                {
                    throw new ArgumentException("point " + id + " is listed twice", nameof(pointIds));
                }

                copy[i] = id;
            }

            return copy;
        }

        private static void Merge(List<ItemCount> items, string itemId, int count)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(items[i].ItemId, itemId, StringComparison.Ordinal))
                {
                    items[i] = new ItemCount(itemId, (int)Math.Min((long)items[i].Count + count, int.MaxValue));
                    return;
                }
            }

            items.Add(new ItemCount(itemId, count));
        }
    }
}
