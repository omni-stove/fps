using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 届いたスナップショットから、オフラインの BattleRoyaleRunner と同じ形の ModeStatus (Detail は BattleRoyaleStatus) を作る。
    // 分隊は PlayerSnapshot.SquadId を陣営の番号 (new Side(SquadId)) にし、残っている分隊の数・エリア・自分の戻る待ちはサーバーが送る値をそのまま出す。
    // 順位はスナップショットに無いので、分隊の全員が倒れた (ダウン中でもない) のと同じスナップショットで残りの分隊の数が減っていれば、その数 + 1 位にする。
    // 終わったときにまだ順位の無い分隊は、オフラインと同じく立っている人数、同じなら残っている人数の多い順に並べる。
    // 誰が誰を起こしているかは送られないので、ReviveTargetId はいつも null にする
    public sealed class ClientBattleRoyaleStatus
    {
        private readonly int _localPlayerId;
        private readonly int _tickRate;
        private readonly ClientZone _zone = new ClientZone();
        private readonly SortedSet<int> _entrants = new SortedSet<int>();
        private readonly Dictionary<int, int> _placements = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _supplyNumbers = new Dictionary<int, int>();
        private readonly List<int> _out = new List<int>();
        private readonly List<SupplyDropMark> _supplyDrops = new List<SupplyDropMark>();
        private bool _tracking;
        private bool _ended;
        private int? _startTick;
        private int? _endTick;
        private bool _hasTimeLimit;
        private int _nextSupplyNumber = 1;
        private Side? _winner;
        private Shown _shown;

        public ClientBattleRoyaleStatus(int localPlayerId, int tickRate = TickClock.DefaultTickRate)
        {
            if (tickRate < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "tickRate must be at least 1");
            }

            _localPlayerId = localPlayerId;
            _tickRate = tickRate;
        }

        // Battle Royale の試合でなければ null。変わるたびに別のものになる
        public ModeStatus Status { get; private set; }

        public BattleRoyaleStatus BattleRoyale => Status?.Detail as BattleRoyaleStatus;

        // Status が変わったら true
        public bool Apply(SnapshotMessage snapshot, string interactKeyLabel = null)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            ref var match = ref snapshot.Match;
            if (match.Objective != MatchObjective.BattleRoyale)
            {
                return Reset();
            }

            // 終わった試合の次の試合が始まったら、分隊と順位を数え直す
            if (_tracking && _ended && match.State != GameModeState.Ended)
            {
                Forget();
            }

            _tracking = true;
            var players = snapshot.Players;
            if (match.State == GameModeState.Playing)
            {
                _startTick ??= snapshot.Tick;
                if (match.RemainingSeconds > 0)
                {
                    _hasTimeLimit = true;
                }

                for (var i = 0; i < players.Count; i++)
                {
                    if (!players[i].OutsideSquad)
                    {
                        _entrants.Add(players[i].SquadId);
                    }
                }

                PlaceEliminated(snapshot);
            }
            else if (match.State == GameModeState.Ended && !_ended)
            {
                _ended = true;
                _endTick = snapshot.Tick;
                PlaceRemaining(snapshot);
            }

            _zone.Apply(match);
            var view = new View(
                match.State,
                _hasTimeLimit,
                _hasTimeLimit ? Math.Max(0, match.RemainingSeconds) : 0f,
                ElapsedSeconds(snapshot),
                match.State == GameModeState.Ended ? _winner : null,
                Math.Max(0, match.SquadsRemaining),
                _entrants,
                _placements,
                players);

            var local = players.IndexOf(_localPlayerId);
            var localPresent = local >= 0;
            float? redeploySeconds = null;
            int? redeploysLeft = null;
            var unarmed = false;
            if (localPresent && snapshot.HasOwner)
            {
                var owner = snapshot.Owner;
                var squad = new Side(players[local].SquadId);
                redeploySeconds = owner.RedeploySeconds.HasValue ? Math.Max(0, owner.RedeploySeconds.Value) : (float?)null;
                var standingOrDown = view.IsStanding(_localPlayerId) || view.IsDowned(_localPlayerId);
                redeploysLeft = redeploySeconds.HasValue
                    || (!view.GetPlacement(squad).HasValue && standingOrDown && owner.RedeploysLeft > 0)
                    ? Math.Max(0, owner.RedeploysLeft)
                    : (int?)null;
                unarmed = owner.Weapon.WeaponCount == 0;
            }

            var atSupplyDrop = CollectSupplyDrops(snapshot, local, view);
            var shown = new Shown(
                view,
                localPresent ? new Side(players[local].SquadId) : (Side?)null,
                _zone.State,
                redeploySeconds,
                redeploysLeft,
                unarmed,
                _supplyDrops.ToArray(),
                atSupplyDrop,
                interactKeyLabel);
            if (Status != null && shown.SameAs(_shown))
            {
                return false;
            }

            _shown = shown;
            var sideScores = new SideScore[view.Sides.Count];
            for (var i = 0; i < sideScores.Length; i++)
            {
                sideScores[i] = new SideScore(view.Sides[i], 0d);
            }

            var ended = match.State == GameModeState.Ended;
            Status = new ModeStatus(
                view.HasTimeLimit ? view.RemainingSeconds : (float?)null,
                sideScores,
                shown.OwnSide,
                shown.RedeploySeconds,
                ended,
                ended ? view.Winner : null,
                new BattleRoyaleStatus(
                    view,
                    localPresent ? _localPlayerId : (int?)null,
                    null,
                    shown.Zone,
                    unarmed,
                    redeploySeconds,
                    redeploysLeft,
                    shown.SupplyDrops,
                    atSupplyDrop,
                    interactKeyLabel));
            return true;
        }

        // Status を捨てたら true
        public bool Reset()
        {
            var had = Status != null;
            Status = null;
            _shown = null;
            _tracking = false;
            _zone.Clear();
            Forget();
            return had;
        }

        private void Forget()
        {
            _ended = false;
            _startTick = null;
            _endTick = null;
            _hasTimeLimit = false;
            _winner = null;
            _entrants.Clear();
            _placements.Clear();
            _supplyNumbers.Clear();
            _nextSupplyNumber = 1;
        }

        // 秒の端数で出し直さないよう、数え上げの時計は 1 秒ごとにだけ進める
        private double ElapsedSeconds(SnapshotMessage snapshot)
        {
            if (!_startTick.HasValue)
            {
                return 0d;
            }

            // オフラインは決着した時刻で時計が止まるので、Ended の間は最初に受けた tick で数える
            var tick = _endTick ?? snapshot.Tick;
            return Math.Floor(Math.Max(0, tick - _startTick.Value) / (double)_tickRate);
        }

        private void PlaceEliminated(SnapshotMessage snapshot)
        {
            _out.Clear();
            var unplaced = 0;
            foreach (var squad in _entrants)
            {
                if (_placements.ContainsKey(squad))
                {
                    continue;
                }

                unplaced++;
                if (CountRemaining(snapshot.Players, squad) == 0)
                {
                    _out.Add(squad);
                }
            }

            var remaining = Math.Max(0, snapshot.Match.SquadsRemaining);

            // 倒れたのが見えても、サーバーがまだ分隊を外していなければ順位は決めない (外した後のスナップショットで決める)
            if (_out.Count == 0 || unplaced - _out.Count < remaining)
            {
                return;
            }

            foreach (var squad in _out)
            {
                _placements[squad] = remaining + 1;
            }
        }

        // BattleRoyaleSession.OnModeEnded と同じ並べ方。勝った分隊を先頭にし、勝者が決まらないときは立っている人数・残っている人数の多い順に並べる
        private void PlaceRemaining(SnapshotMessage snapshot)
        {
            var remaining = new List<int>();
            foreach (var squad in _entrants)
            {
                if (!_placements.ContainsKey(squad))
                {
                    remaining.Add(squad);
                }
            }

            var players = snapshot.Players;
            remaining.Sort((a, b) =>
            {
                var byCount = FinalCount(players, b).CompareTo(FinalCount(players, a));
                return byCount != 0 ? byCount : a.CompareTo(b);
            });

            var place = 1;
            for (var i = 0; i < remaining.Count; i++)
            {
                if (i > 0 && FinalCount(players, remaining[i - 1]) != FinalCount(players, remaining[i]))
                {
                    place = i + 1;
                }

                _placements[remaining[i]] = place;
            }

            _winner = null;
            if (remaining.Count == 1 || (remaining.Count > 1 && FinalCount(players, remaining[0]) != FinalCount(players, remaining[1])))
            {
                _winner = new Side(remaining[0]);
            }
        }

        // 終わったスナップショットだけから勝った分隊を引く。立っている人数・残っている人数が単独で最も多い分隊で、並べば null。分隊に入っていない人も分隊 0 として届くので、OutsideSquad の行は数えない
        public static Side? LeadingSquad(SnapshotList<PlayerSnapshot> players)
        {
            if (players == null)
            {
                throw new ArgumentNullException(nameof(players));
            }

            int? leader = null;
            var best = long.MinValue;
            var tied = false;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].OutsideSquad)
                {
                    continue;
                }

                var squad = players[i].SquadId;
                var count = FinalCount(players, squad);
                if (count > best)
                {
                    best = count;
                    leader = squad;
                    tied = false;
                }
                else if (count == best && leader != squad)
                {
                    tied = true;
                }
            }

            return leader.HasValue && !tied ? new Side(leader.Value) : (Side?)null;
        }

        // オフラインの CaptureStandingAndPickLeader と同じく、立っている人数を上の桁、残っている人数を下の桁にする
        private static long FinalCount(SnapshotList<PlayerSnapshot> players, int squad)
        {
            var remaining = CountRemaining(players, squad);
            var downed = 0;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].SquadId == squad && !players[i].OutsideSquad && players[i].Downed.IsDowned)
                {
                    downed++;
                }
            }

            return ((long)(remaining - downed) << 32) | (uint)remaining;
        }

        private static int CountRemaining(SnapshotList<PlayerSnapshot> players, int squad)
        {
            var count = 0;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.SquadId == squad && !row.OutsideSquad && IsRemaining(row))
                {
                    count++;
                }
            }

            return count;
        }

        // オフラインでダウン中の人は倒れた (ModeSession.IsDown) ことにならないので、ダウン中も残っている人に数える
        private static bool IsRemaining(in PlayerSnapshot row) => row.Health > 0 || row.Downed.IsDowned;

        // 補給の箱の番号はオフラインと同じく試合の中で落ちた順にするため、初めて見た順に振る。開けた箱は載せない
        private bool CollectSupplyDrops(SnapshotMessage snapshot, int local, View view)
        {
            _supplyDrops.Clear();
            var atDrop = false;
            var localStanding = local >= 0 && view.IsStanding(_localPlayerId);
            var localPosition = local >= 0 ? snapshot.Players[local].Position : Vector3.Zero;
            var items = snapshot.ObjectiveItems;
            for (var i = 0; i < items.Count; i++)
            {
                ref var item = ref items[i];
                if (item.Kind != ObjectiveItemKind.SupplyCrate)
                {
                    continue;
                }

                if (!_supplyNumbers.TryGetValue(item.ItemId, out var number))
                {
                    number = _nextSupplyNumber++;
                    _supplyNumbers[item.ItemId] = number;
                }

                if (item.Opened)
                {
                    continue;
                }

                var landed = HasLanded(item);
                _supplyDrops.Add(new SupplyDropMark(number, item.Position, landed));
                if (landed && localStanding && item.Radius > 0f
                    && Vector3.DistanceSquared(localPosition, item.Position) <= item.Radius * item.Radius)
                {
                    atDrop = true;
                }
            }

            _supplyDrops.Sort((a, b) => a.Number.CompareTo(b.Number));
            return atDrop;
        }

        // 落ちている途中の箱は置き場所 (Home) にまだ着いていないものとして送られる
        public static bool HasLanded(in ObjectiveItemSnapshot item) => item.State == ObjectiveItemState.Home;

        private sealed class Shown
        {
            public Shown(
                View view,
                Side? ownSide,
                ZoneState? zone,
                float? redeploySeconds,
                int? redeploysLeft,
                bool unarmed,
                SupplyDropMark[] supplyDrops,
                bool atSupplyDrop,
                string interactKeyLabel)
            {
                View = view;
                OwnSide = ownSide;
                Zone = zone;
                RedeploySeconds = redeploySeconds;
                RedeploysLeft = redeploysLeft;
                Unarmed = unarmed;
                SupplyDrops = supplyDrops;
                AtSupplyDrop = atSupplyDrop;
                InteractKeyLabel = interactKeyLabel;
            }

            public View View { get; }

            public Side? OwnSide { get; }

            public ZoneState? Zone { get; }

            public float? RedeploySeconds { get; }

            public int? RedeploysLeft { get; }

            public bool Unarmed { get; }

            public SupplyDropMark[] SupplyDrops { get; }

            public bool AtSupplyDrop { get; }

            public string InteractKeyLabel { get; }

            public bool SameAs(Shown other)
            {
                if (other == null || !View.SameAs(other.View) || OwnSide != other.OwnSide || RedeploySeconds != other.RedeploySeconds
                    || RedeploysLeft != other.RedeploysLeft || Unarmed != other.Unarmed || AtSupplyDrop != other.AtSupplyDrop
                    || !string.Equals(InteractKeyLabel, other.InteractKeyLabel, StringComparison.Ordinal)
                    || Zone.HasValue != other.Zone.HasValue || SupplyDrops.Length != other.SupplyDrops.Length)
                {
                    return false;
                }

                if (Zone.HasValue && !SameZone(Zone.Value, other.Zone.Value))
                {
                    return false;
                }

                for (var i = 0; i < SupplyDrops.Length; i++)
                {
                    var a = SupplyDrops[i];
                    var b = other.SupplyDrops[i];
                    if (a.Number != b.Number || a.Position != b.Position || a.HasLanded != b.HasLanded)
                    {
                        return false;
                    }
                }

                return true;
            }

            private static bool SameZone(ZoneState a, ZoneState b)
            {
                return a.PhaseIndex == b.PhaseIndex && a.Stage == b.Stage && a.Center == b.Center && a.Radius == b.Radius
                    && a.TargetCenter == b.TargetCenter && a.TargetRadius == b.TargetRadius
                    && a.SecondsUntilShrink == b.SecondsUntilShrink && a.SecondsUntilStageEnds == b.SecondsUntilStageEnds;
            }
        }

        // HUD が後から読んでも変わらないよう、出したときの人と順位を写して持つ
        private sealed class View : IBattleRoyaleView
        {
            private readonly Row[] _rows;
            private readonly Side[] _sides;
            private readonly Dictionary<int, int> _placements;

            public View(
                GameModeState state,
                bool hasTimeLimit,
                float remainingSeconds,
                double elapsedSeconds,
                Side? winner,
                int squadsRemaining,
                SortedSet<int> entrants,
                Dictionary<int, int> placements,
                SnapshotList<PlayerSnapshot> players)
            {
                State = state;
                HasTimeLimit = hasTimeLimit;
                RemainingSeconds = remainingSeconds;
                ElapsedSeconds = elapsedSeconds;
                Winner = winner;
                SquadsRemaining = squadsRemaining;
                _placements = new Dictionary<int, int>(placements);
                EntrantCount = Math.Max(entrants.Count, squadsRemaining + _placements.Count);

                _rows = new Row[players.Count];
                var squads = new SortedSet<int>(entrants);
                for (var i = 0; i < _rows.Length; i++)
                {
                    ref var row = ref players[i];
                    _rows[i] = new Row(
                        row.PlayerId,
                        row.SquadId,
                        IsRemaining(row),
                        row.Downed.IsDowned,
                        Math.Max(0, row.Downed.BleedoutSeconds),
                        Math.Min(DownedSync.FullPercent, (int)row.Downed.RevivePercent));
                    squads.Add(row.SquadId);
                }

                _sides = new Side[squads.Count];
                var at = 0;
                foreach (var squad in squads)
                {
                    _sides[at++] = new Side(squad);
                }
            }

            public GameModeState State { get; }

            public bool HasTimeLimit { get; }

            public float RemainingSeconds { get; }

            public double ElapsedSeconds { get; }

            public Side? Winner { get; }

            public IReadOnlyList<Side> Sides => _sides;

            public int EntrantCount { get; }

            public int SquadsRemaining { get; }

            public bool HasPlayer(int playerId) => Find(playerId) >= 0;

            public Side GetSide(int playerId) => new Side(Require(playerId).Squad);

            public int CountMembers(Side squad) => Count(squad, row => true);

            public int CountRemaining(Side squad) => Count(squad, row => row.Remaining);

            public int CountStanding(Side squad) => Count(squad, row => row.Remaining && !row.Downed);

            public int CountDowned(Side squad) => Count(squad, row => row.Downed);

            public int? GetPlacement(Side squad) => _placements.TryGetValue(squad.Id, out var place) ? place : (int?)null;

            public bool IsDowned(int playerId)
            {
                var at = Find(playerId);
                return at >= 0 && _rows[at].Downed;
            }

            public bool IsStanding(int playerId)
            {
                var at = Find(playerId);
                return at >= 0 && _rows[at].Remaining && !_rows[at].Downed;
            }

            public float? GetBleedoutRemaining(int playerId)
            {
                var at = Find(playerId);
                return at >= 0 && _rows[at].Downed ? _rows[at].BleedoutSeconds : (float?)null;
            }

            public float GetReviveProgress(int playerId)
            {
                var at = Find(playerId);
                return at >= 0 && _rows[at].Downed ? _rows[at].RevivePercent / (float)DownedSync.FullPercent : 0f;
            }

            public bool SameAs(View other)
            {
                if (State != other.State || HasTimeLimit != other.HasTimeLimit || RemainingSeconds != other.RemainingSeconds
                    || ElapsedSeconds != other.ElapsedSeconds || Winner != other.Winner || SquadsRemaining != other.SquadsRemaining
                    || EntrantCount != other.EntrantCount || _rows.Length != other._rows.Length || _sides.Length != other._sides.Length
                    || _placements.Count != other._placements.Count)
                {
                    return false;
                }

                for (var i = 0; i < _rows.Length; i++)
                {
                    if (!_rows[i].Equals(other._rows[i]))
                    {
                        return false;
                    }
                }

                for (var i = 0; i < _sides.Length; i++)
                {
                    if (_sides[i] != other._sides[i])
                    {
                        return false;
                    }
                }

                foreach (var pair in _placements)
                {
                    if (!other._placements.TryGetValue(pair.Key, out var place) || place != pair.Value)
                    {
                        return false;
                    }
                }

                return true;
            }

            private int Count(Side squad, Func<Row, bool> match)
            {
                var count = 0;
                foreach (var row in _rows)
                {
                    if (row.Squad == squad.Id && match(row))
                    {
                        count++;
                    }
                }

                return count;
            }

            private int Find(int playerId)
            {
                for (var i = 0; i < _rows.Length; i++)
                {
                    if (_rows[i].PlayerId == playerId)
                    {
                        return i;
                    }
                }

                return -1;
            }

            private Row Require(int playerId)
            {
                var at = Find(playerId);
                if (at < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "no player with this id is in the snapshot");
                }

                return _rows[at];
            }
        }

        private readonly struct Row : IEquatable<Row>
        {
            public Row(int playerId, int squad, bool remaining, bool downed, int bleedoutSeconds, int revivePercent)
            {
                PlayerId = playerId;
                Squad = squad;
                Remaining = remaining;
                Downed = downed;
                BleedoutSeconds = bleedoutSeconds;
                RevivePercent = revivePercent;
            }

            public int PlayerId { get; }

            public int Squad { get; }

            public bool Remaining { get; }

            public bool Downed { get; }

            public int BleedoutSeconds { get; }

            public int RevivePercent { get; }

            public bool Equals(Row other)
            {
                return PlayerId == other.PlayerId && Squad == other.Squad && Remaining == other.Remaining && Downed == other.Downed
                    && BleedoutSeconds == other.BleedoutSeconds && RevivePercent == other.RevivePercent;
            }

            public override bool Equals(object obj) => obj is Row other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(PlayerId, Squad, Remaining, Downed, BleedoutSeconds, RevivePercent);
        }
    }
}
