using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;
using RaidOutcome = Shooter.Core.ExtractionOutcome;
using StashOutcome = Shooter.Protocol.ExtractionOutcome;

namespace Shooter.NetV2
{
    // 届いたスナップショットと StashStateMessage から、オフラインの ExtractionRunner が出す ExtractionStatus と同じ値を読む。
    // ExtractionStatus は Runtime の型なので、この組み立て (Unity 抜きで確かめられる) は値だけを持ち、ModeStatus に包むのは NetV2Hud がする。
    // 出撃の長さ・地点の開く時刻・脱出に待つ秒は送られないので、手元の調整値 (ExtractionSpec) と残りの秒から決める。
    // 持ち帰った物・失った物も送られないので、出撃が終わった知らせの前後で持ち物庫に増えた分を持ち帰った物、持っていた残りを失った物にする
    public sealed class ClientExtractionStatus
    {
        private static readonly ItemCount[] _none = Array.Empty<ItemCount>();

        private readonly int _localPlayerId;
        private readonly ExtractionSpec _spec;
        private readonly Dictionary<string, long> _stashBefore = new Dictionary<string, long>(StringComparer.Ordinal);
        private ItemCount[] _carriedBefore = _none;
        private bool _hasRaidingState;
        private Shown _shown;

        public ClientExtractionStatus(int localPlayerId, ExtractionSpec spec = null)
        {
            _localPlayerId = localPlayerId;
            _spec = spec ?? ExtractionSpec.Default;
        }

        // Extraction の試合のスナップショットが届いていなければ false
        public bool IsActive => _shown != null;

        public RaidOutcome Outcome { get; private set; }

        public bool IsOver => Outcome != RaidOutcome.Raiding;

        public float RemainingSeconds => _shown?.RemainingSeconds ?? 0f;

        public float ElapsedSeconds => _shown?.ElapsedSeconds ?? 0f;

        public bool PointsOpen => _shown?.PointsOpen ?? false;

        public float SecondsUntilOpen => _shown?.SecondsUntilOpen ?? 0f;

        public int OpenPointCount => _shown?.OpenPointCount ?? 0;

        // 脱出地点で待っていなければ null
        public float? HoldProgress => _shown?.HoldProgress;

        public float HoldRemainingSeconds => _shown?.HoldRemainingSeconds ?? 0f;

        public bool StandingOnClosedPoint => _shown?.StandingOnClosedPoint ?? false;

        public bool LootInReach => _shown?.LootInReach ?? false;

        public IReadOnlyList<ItemCount> Carried { get; private set; } = _none;

        public IReadOnlyList<ItemCount> Stored { get; private set; } = _none;

        public IReadOnlyList<ItemCount> Lost { get; private set; } = _none;

        public Side? OwnSide => _shown?.OwnSide;

        public float? RespawnSeconds => _shown?.RespawnSeconds;

        public bool IsEnded => _shown?.IsEnded ?? false;

        public Side? Winner => _shown?.Winner;

        public int AlphaPoints => _shown?.AlphaPoints ?? 0;

        public int BravoPoints => _shown?.BravoPoints ?? 0;

        // 出撃の結果か持ち物が変わったら true
        public bool Receive(StashStateMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            var outcome = ToRaidOutcome(message.Outcome);
            var carried = ToItems(message.Carried);
            var changed = outcome != Outcome || !Shown.Same(outcome == RaidOutcome.Raiding ? carried : _none, Carried);
            if (outcome == RaidOutcome.Raiding)
            {
                // 終わった後の次の出撃なら、前の結果を捨てる
                Stored = _none;
                Lost = _none;
                _hasRaidingState = true;
                _carriedBefore = carried;
                Count(message.Stash, _stashBefore);
            }
            else if (Outcome == RaidOutcome.Raiding)
            {
                Settle(message.Stash);
                changed = true;
            }

            Outcome = outcome;
            Carried = outcome == RaidOutcome.Raiding ? carried : _none;
            return changed;
        }

        // Extraction の試合でなくなったら false を出して値を捨てる。出す値が変わったら true
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ref var match = ref snapshot.Match;
            if (match.Objective != MatchObjective.Extraction)
            {
                return Clear();
            }

            var remaining = Math.Max(0, match.RemainingSeconds);
            var elapsed = Math.Max(0f, _spec.RaidSeconds - remaining);
            var local = snapshot.Players.IndexOf(_localPlayerId);
            var alive = local >= 0 && snapshot.Players[local].Health > 0;
            var position = local >= 0 ? snapshot.Players[local].Position : Vector3.Zero;
            var openCount = 0;
            var onClosed = false;
            var lootInReach = false;
            var items = snapshot.ObjectiveItems;
            for (var i = 0; i < items.Count; i++)
            {
                ref var item = ref items[i];
                switch (item.Kind)
                {
                    case ObjectiveItemKind.ExtractionPoint:
                        if (item.Opened)
                        {
                            openCount++;
                        }
                        else if (alive && WithinFlat(position, item.Position, item.Radius))
                        {
                            onClosed = true;
                        }

                        break;
                    case ObjectiveItemKind.LootBox:
                    case ObjectiveItemKind.SupplyCrate:
                    case ObjectiveItemKind.DeathBox:
                        if (alive && !item.Opened && item.Radius > 0f
                            && Vector3.DistanceSquared(position, item.Position) <= item.Radius * item.Radius)
                        {
                            lootInReach = true;
                        }

                        break;
                }
            }

            var pointsOpen = openCount > 0 || elapsed >= _spec.OpenAfterSeconds;
            float? holdProgress = null;
            var holdRemaining = 0f;
            float? respawn = null;
            if (snapshot.HasOwner)
            {
                var owner = snapshot.Owner;
                if (owner.HasExtractionPoint && !IsOver)
                {
                    var progress = Math.Min(1f, Math.Max(0f, owner.ExtractionProgress));
                    holdProgress = progress;
                    holdRemaining = Math.Max(0f, _spec.ExtractHoldSeconds * (1f - progress));
                }

                respawn = owner.RespawnSeconds.HasValue ? Math.Max(0, owner.RespawnSeconds.Value) : (float?)null;
            }

            var ended = match.State == GameModeState.Ended;
            var shown = new Shown(
                Outcome,
                remaining,
                elapsed,
                pointsOpen,
                pointsOpen ? 0f : Math.Max(0f, _spec.OpenAfterSeconds - elapsed),
                openCount,
                holdProgress,
                holdRemaining,
                !IsOver && !holdProgress.HasValue && onClosed,
                !IsOver && lootInReach,
                local >= 0 ? Side.FromTeam(snapshot.Players[local].Team) : (Side?)null,
                respawn,
                ended,
                ended && match.HasWinner ? Side.FromTeam(match.Winner) : (Side?)null,
                match.AlphaPoints,
                match.BravoPoints,
                Carried,
                Stored,
                Lost);
            if (_shown != null && shown.Equals(_shown))
            {
                return false;
            }

            _shown = shown;
            return true;
        }

        // 試合を離れたときや、Extraction でない試合に移ったときに呼ぶ。消したら true
        public bool Clear()
        {
            var had = _shown != null;
            _shown = null;
            Outcome = RaidOutcome.Raiding;
            Carried = _none;
            Stored = _none;
            Lost = _none;
            _carriedBefore = _none;
            _stashBefore.Clear();
            _hasRaidingState = false;
            return had;
        }

        public static RaidOutcome ToRaidOutcome(StashOutcome outcome)
        {
            switch (outcome)
            {
                case StashOutcome.None:
                    return RaidOutcome.Raiding;
                case StashOutcome.Extracted:
                    return RaidOutcome.Extracted;
                case StashOutcome.Killed:
                    return RaidOutcome.Died;
                case StashOutcome.TimedOut:
                    return RaidOutcome.TimedOut;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "unknown extraction outcome");
            }
        }

        // ExtractionSession.End と同じく、持っていた物の順に、持ち物庫に入った分を持ち帰った物、残りを失った物にする。
        // 出撃中の知らせを受けずに終わりの知らせだけを受けたら (終わってから入った)、何を持っていたか分からないので両方とも空にする
        private void Settle(StashItemList stash)
        {
            if (!_hasRaidingState)
            {
                Stored = _none;
                Lost = _none;
                return;
            }

            var after = new Dictionary<string, long>(StringComparer.Ordinal);
            Count(stash, after);
            var stored = new List<ItemCount>();
            var lost = new List<ItemCount>();
            foreach (var item in _carriedBefore)
            {
                after.TryGetValue(item.ItemId, out var now);
                _stashBefore.TryGetValue(item.ItemId, out var before);
                var added = (int)Math.Min(item.Count, Math.Max(0L, now - before));
                if (added > 0)
                {
                    stored.Add(new ItemCount(item.ItemId, added));

                    // 同じ物が持っていた物の中に 2 度あっても、増えた分を 2 度数えないようにする
                    _stashBefore[item.ItemId] = before + added;
                }

                if (item.Count - added > 0)
                {
                    lost.Add(new ItemCount(item.ItemId, item.Count - added));
                }
            }

            Stored = stored.ToArray();
            Lost = lost.ToArray();
            _hasRaidingState = false;
        }

        private static void Count(StashItemList list, Dictionary<string, long> into)
        {
            into.Clear();
            for (var i = 0; i < list.Count; i++)
            {
                var id = list.IdAt(i);
                into.TryGetValue(id, out var total);
                into[id] = total + list.CountAt(i);
            }
        }

        private static ItemCount[] ToItems(StashItemList list)
        {
            if (list.Count == 0)
            {
                return _none;
            }

            var items = new ItemCount[list.Count];
            for (var i = 0; i < items.Length; i++)
            {
                items[i] = new ItemCount(list.IdAt(i), list.CountAt(i));
            }

            return items;
        }

        // ExtractionZone.Contains と同じく、高さを見ずに円の中かで決める
        private static bool WithinFlat(Vector3 position, Vector3 center, float radius)
        {
            if (!(radius > 0f))
            {
                return false;
            }

            var x = position.X - center.X;
            var z = position.Z - center.Z;
            return (x * x) + (z * z) <= radius * radius;
        }

        private sealed class Shown : IEquatable<Shown>
        {
            public Shown(
                RaidOutcome outcome,
                float remainingSeconds,
                float elapsedSeconds,
                bool pointsOpen,
                float secondsUntilOpen,
                int openPointCount,
                float? holdProgress,
                float holdRemainingSeconds,
                bool standingOnClosedPoint,
                bool lootInReach,
                Side? ownSide,
                float? respawnSeconds,
                bool isEnded,
                Side? winner,
                int alphaPoints,
                int bravoPoints,
                IReadOnlyList<ItemCount> carried,
                IReadOnlyList<ItemCount> stored,
                IReadOnlyList<ItemCount> lost)
            {
                Outcome = outcome;
                RemainingSeconds = remainingSeconds;
                ElapsedSeconds = elapsedSeconds;
                PointsOpen = pointsOpen;
                SecondsUntilOpen = secondsUntilOpen;
                OpenPointCount = openPointCount;
                HoldProgress = holdProgress;
                HoldRemainingSeconds = holdRemainingSeconds;
                StandingOnClosedPoint = standingOnClosedPoint;
                LootInReach = lootInReach;
                OwnSide = ownSide;
                RespawnSeconds = respawnSeconds;
                IsEnded = isEnded;
                Winner = winner;
                AlphaPoints = alphaPoints;
                BravoPoints = bravoPoints;
                Carried = carried;
                Stored = stored;
                Lost = lost;
            }

            public RaidOutcome Outcome { get; }

            public float RemainingSeconds { get; }

            public float ElapsedSeconds { get; }

            public bool PointsOpen { get; }

            public float SecondsUntilOpen { get; }

            public int OpenPointCount { get; }

            public float? HoldProgress { get; }

            public float HoldRemainingSeconds { get; }

            public bool StandingOnClosedPoint { get; }

            public bool LootInReach { get; }

            public Side? OwnSide { get; }

            public float? RespawnSeconds { get; }

            public bool IsEnded { get; }

            public Side? Winner { get; }

            public int AlphaPoints { get; }

            public int BravoPoints { get; }

            public IReadOnlyList<ItemCount> Carried { get; }

            public IReadOnlyList<ItemCount> Stored { get; }

            public IReadOnlyList<ItemCount> Lost { get; }

            // 待つ進み具合は毎 tick 届くので、HUD に出る残りの秒が同じ間は同じとみなして出し直さない
            public bool Equals(Shown other)
            {
                return other != null
                    && Outcome == other.Outcome
                    && RemainingSeconds == other.RemainingSeconds
                    && PointsOpen == other.PointsOpen
                    && SecondsUntilOpen == other.SecondsUntilOpen
                    && OpenPointCount == other.OpenPointCount
                    && HoldProgress.HasValue == other.HoldProgress.HasValue
                    && HudFormat.DisplayedSeconds(HoldRemainingSeconds) == HudFormat.DisplayedSeconds(other.HoldRemainingSeconds)
                    && StandingOnClosedPoint == other.StandingOnClosedPoint
                    && LootInReach == other.LootInReach
                    && OwnSide == other.OwnSide
                    && RespawnSeconds == other.RespawnSeconds
                    && IsEnded == other.IsEnded
                    && Winner == other.Winner
                    && AlphaPoints == other.AlphaPoints
                    && BravoPoints == other.BravoPoints
                    && Same(Carried, other.Carried)
                    && Same(Stored, other.Stored)
                    && Same(Lost, other.Lost);
            }

            public override bool Equals(object obj) => obj is Shown other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(Outcome, RemainingSeconds, OpenPointCount, HoldProgress.HasValue);

            // 外のクラスに置くと、Shown からの呼び出しが CA1859 に数えられず配列にせよと言われるため、ここに置く
            internal static bool Same(IReadOnlyList<ItemCount> a, IReadOnlyList<ItemCount> b)
            {
                if (a.Count != b.Count)
                {
                    return false;
                }

                for (var i = 0; i < a.Count; i++)
                {
                    if (!string.Equals(a[i].ItemId, b[i].ItemId, StringComparison.Ordinal) || a[i].Count != b[i].Count)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
