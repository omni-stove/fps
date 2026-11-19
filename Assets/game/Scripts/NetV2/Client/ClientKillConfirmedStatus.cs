using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // オフラインの KillConfirmedRunner と同じく、Detail に KillConfirmed を持つ ModeStatus にして、同じ HUD の書式役で文にできるようにする。
    // KillConfirmed はサーバーが数えた確定・阻止の数と場のタグを写すだけで、ここでは点数も勝敗も決めない。
    // 出直しの残り秒はスナップショットに欄が無いので出さない
    public sealed class ClientKillConfirmedStatus
    {
        private readonly int _localPlayerId;
        private readonly KillConfirmed _detail = new KillConfirmed(KillConfirmedSpec.Default);
        private readonly HashSet<int> _present = new HashSet<int>();
        private readonly List<int> _gone = new List<int>();
        private int _newestTick = -1;
        private Shown _shown;

        public ClientKillConfirmedStatus(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
        }

        // Kill Confirmed の試合のスナップショットがまだ届いていないか、別のモードの試合なら null
        public ModeStatus Status { get; private set; }

        public KillConfirmed Detail => _detail;

        // Status を作り直したら (null に戻したときも) true。呼ぶ側はそのときだけ GameSignals.ModeStatus に出し直す
        public bool Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            // 遅れて届いた古いスナップショットで、新しい状態を巻き戻さない
            if (snapshot.Tick <= _newestTick)
            {
                return false;
            }

            _newestTick = snapshot.Tick;
            ref var match = ref snapshot.Match;
            if (match.Objective != MatchObjective.KillConfirmed)
            {
                return Clear();
            }

            var tagsChanged = CopyTags(snapshot.ObjectiveItems);
            _detail.SetCounts(Side.Alpha, match.AlphaPoints, match.AlphaDenied);
            _detail.SetCounts(Side.Bravo, match.BravoPoints, match.BravoDenied);

            var ended = match.State == GameModeState.Ended;
            var shown = new Shown(
                match.RemainingSeconds,
                match.AlphaScore,
                match.BravoScore,
                match.AlphaPoints,
                match.BravoPoints,
                match.AlphaDenied,
                match.BravoDenied,
                OwnSideOf(snapshot),
                ended,
                ended && match.HasWinner ? Side.FromTeam(match.Winner) : (Side?)null);
            if (Status != null && !tagsChanged && shown.Equals(_shown))
            {
                return false;
            }

            _shown = shown;
            // ModeStatus は出した後も UI が持つので、得点の並びは使い回さずに毎回作る
            var sideScores = new[]
            {
                new SideScore(Side.Alpha, shown.AlphaScore),
                new SideScore(Side.Bravo, shown.BravoScore),
            };
            Status = new ModeStatus(shown.RemainingSeconds, sideScores, shown.OwnSide, null, shown.Ended, shown.Winner, _detail);
            return true;
        }

        // 試合から抜けたときに呼ぶ。次の接続の tick が小さくても受け付けるよう、届いた tick も忘れる
        public void Reset()
        {
            _newestTick = -1;
            Clear();
        }

        private bool Clear()
        {
            _detail.Reset();
            _shown = default;
            if (Status == null)
            {
                return false;
            }

            Status = null;
            return true;
        }

        private bool CopyTags(SnapshotList<ObjectiveItemSnapshot> items)
        {
            var changed = false;
            _present.Clear();
            for (var i = 0; i < items.Count; i++)
            {
                ref var item = ref items[i];
                // 0 以下の Id は Core のタグに無いので、壊れた行として写さない
                if (item.Kind != ObjectiveItemKind.Tag || item.ItemId < 1)
                {
                    continue;
                }

                var ownerId = item.HasOwner ? item.OwnerId : PlayerIds.None;
                var ownerSide = Side.FromTeam(item.Team);
                _present.Add(item.ItemId);
                var existing = _detail.FindTag(item.ItemId);
                if (existing != null)
                {
                    if (existing.OwnerId == ownerId && existing.OwnerSide == ownerSide)
                    {
                        continue;
                    }

                    // 次の試合でサーバーが Id を 1 から振り直すと、同じ Id で別の人のタグが届く
                    _detail.Discard(item.ItemId);
                }

                _detail.ShowTag(item.ItemId, ownerId, ownerSide);
                changed = true;
            }

            _gone.Clear();
            foreach (var tag in _detail.Tags)
            {
                if (!_present.Contains(tag.Id))
                {
                    _gone.Add(tag.Id);
                }
            }

            foreach (var tagId in _gone)
            {
                _detail.Discard(tagId);
                changed = true;
            }

            _gone.Clear();
            return changed;
        }

        private Side? OwnSideOf(SnapshotMessage snapshot)
        {
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId == _localPlayerId)
                {
                    return Side.FromTeam(row.Team);
                }
            }

            return null;
        }

        private readonly struct Shown : IEquatable<Shown>
        {
            public Shown(
                int remainingSeconds,
                int alphaScore,
                int bravoScore,
                int alphaConfirmed,
                int bravoConfirmed,
                int alphaDenied,
                int bravoDenied,
                Side? ownSide,
                bool ended,
                Side? winner)
            {
                RemainingSeconds = remainingSeconds;
                AlphaScore = alphaScore;
                BravoScore = bravoScore;
                AlphaConfirmed = alphaConfirmed;
                BravoConfirmed = bravoConfirmed;
                AlphaDenied = alphaDenied;
                BravoDenied = bravoDenied;
                OwnSide = ownSide;
                Ended = ended;
                Winner = winner;
            }

            public int RemainingSeconds { get; }

            public int AlphaScore { get; }

            public int BravoScore { get; }

            public int AlphaConfirmed { get; }

            public int BravoConfirmed { get; }

            public int AlphaDenied { get; }

            public int BravoDenied { get; }

            public Side? OwnSide { get; }

            public bool Ended { get; }

            public Side? Winner { get; }

            public bool Equals(Shown other)
            {
                return RemainingSeconds == other.RemainingSeconds
                    && AlphaScore == other.AlphaScore
                    && BravoScore == other.BravoScore
                    && AlphaConfirmed == other.AlphaConfirmed
                    && BravoConfirmed == other.BravoConfirmed
                    && AlphaDenied == other.AlphaDenied
                    && BravoDenied == other.BravoDenied
                    && OwnSide == other.OwnSide
                    && Ended == other.Ended
                    && Winner == other.Winner;
            }

            public override bool Equals(object obj) => obj is Shown other && Equals(other);

            // HashCode.Combine は 8 個までしか取らないので、数の欄を先にまとめる
            public override int GetHashCode() => HashCode.Combine(
                RemainingSeconds,
                HashCode.Combine(AlphaScore, BravoScore, AlphaConfirmed, BravoConfirmed, AlphaDenied, BravoDenied),
                OwnSide,
                Ended,
                Winner);
        }
    }
}
