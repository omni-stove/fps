using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    // ExtractionSession は出した後も動き続けるので、UI が持ち続けても変わらないよう、出したときの値を写して持つ。
    public sealed class ExtractionStatus
    {
        public ExtractionStatus(
            ExtractionSession session,
            bool standingOnClosedPoint,
            bool lootInReach,
            string interactKeyLabel)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            Outcome = session.Outcome;
            RemainingSeconds = session.RemainingSeconds;
            ElapsedSeconds = session.ElapsedSeconds;
            PointsOpen = session.PointsOpen;
            SecondsUntilOpen = session.SecondsUntilOpen;
            OpenPointCount = session.OpenPointIds.Count;
            IsHolding = session.HoldingPointId != null;
            HoldProgress = session.HoldProgress;
            HoldRemainingSeconds = IsHolding ? Math.Max(0f, session.Spec.ExtractHoldSeconds - session.HoldSeconds) : 0f;
            StandingOnClosedPoint = !session.IsOver && !IsHolding && standingOnClosedPoint;
            LootInReach = !session.IsOver && lootInReach;
            InteractKeyLabel = interactKeyLabel;
            Carried = Copy(session.Carried);
            Stored = Copy(session.Stored);
            Lost = Copy(session.Lost);
            CarriedCount = Total(Carried);
            StoredCount = Total(Stored);
            LostCount = Total(Lost);
        }

        public ExtractionOutcome Outcome { get; }

        public bool IsOver => Outcome != ExtractionOutcome.Raiding;

        public float RemainingSeconds { get; }

        public float ElapsedSeconds { get; }

        public bool PointsOpen { get; }

        public float SecondsUntilOpen { get; }

        public int OpenPointCount { get; }

        public bool IsHolding { get; }

        public float HoldProgress { get; }

        public float HoldRemainingSeconds { get; }

        public bool StandingOnClosedPoint { get; }

        public bool LootInReach { get; }

        public string InteractKeyLabel { get; }

        public IReadOnlyList<ItemCount> Carried { get; }

        public IReadOnlyList<ItemCount> Stored { get; }

        public IReadOnlyList<ItemCount> Lost { get; }

        public int CarriedCount { get; }

        public int StoredCount { get; }

        public int LostCount { get; }

        private static ItemCount[] Copy(IReadOnlyList<ItemCount> items)
        {
            var copy = new ItemCount[items.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = items[i];
            }

            return copy;
        }

        private static int Total(IReadOnlyList<ItemCount> items)
        {
            var total = 0L;
            foreach (var item in items)
            {
                total += item.Count;
            }

            return (int)Math.Min(total, int.MaxValue);
        }
    }
}
