using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // Detail に Core の GuardianStatus を置くのは、オフラインの GuardianRunner と同じ型にして、UI が GuardianStatus に登録した書式役をそのまま使えるようにするため。
    // サーバーは各チームのリーダーを場の物 (ObjectiveItemKind.Leader) で送る。持ち主がリーダーで、運んでいる (Carried) 間は立っていて、落ちている (Dropped) なら倒れた。
    // GuardianStatus は GuardianRespawn を引くので、届いたリーダーだけを入れた写しの ModeSession と GuardianRespawn を手元に作る。
    // 勝ち負けはサーバーが決めて送るので、写しでは決めない
    public sealed class ClientGuardianStatus
    {
        private readonly int _localPlayerId;
        private int _newestTick = -1;
        private GuardianRespawn _leaders;
        private Shown _shown;

        public ClientGuardianStatus(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
        }

        // Guardian の試合のスナップショットがまだ届いていないか、別のモードの試合なら null
        public ModeStatus Status { get; private set; }

        public GuardianStatus Guardian => Status?.Detail as GuardianStatus;

        public int? LeaderOf(Team team)
        {
            return Status == null ? null : team == Team.Alpha ? _shown.AlphaLeader : _shown.BravoLeader;
        }

        public bool HasLeaderFallen(Team team)
        {
            return Status != null && (team == Team.Alpha ? _shown.AlphaFallen : _shown.BravoFallen);
        }

        public bool IsStandingLeader(int playerId)
        {
            return Status != null
                && ((_shown.AlphaLeader == playerId && !_shown.AlphaFallen) || (_shown.BravoLeader == playerId && !_shown.BravoFallen));
        }

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
            if (match.Objective != MatchObjective.Guardian)
            {
                return Clear();
            }

            ReadLeaders(snapshot, out var alphaLeader, out var alphaFallen, out var bravoLeader, out var bravoFallen);
            var local = FindLocal(snapshot, out var ownTeam, out var localHealth);
            var ended = match.State == GameModeState.Ended;
            var ownFallen = local && (ownTeam == Team.Alpha ? alphaFallen : bravoFallen);
            var shown = new Shown(
                Math.Max(0, match.RemainingSeconds),
                match.AlphaScore,
                match.BravoScore,
                local ? Side.FromTeam(ownTeam) : (Side?)null,
                RespawnSeconds(snapshot),
                ended,
                ended && match.HasWinner ? Side.FromTeam(match.Winner) : (Side?)null,
                alphaLeader,
                alphaFallen,
                bravoLeader,
                bravoFallen,
                local,
                // オフラインと同じく、試合中に倒れていて、自分のチームのリーダーも倒れていれば、もう戻らない
                local && match.State == GameModeState.Playing && localHealth <= 0 && ownFallen);
            if (Status != null && shown.Equals(_shown))
            {
                return false;
            }

            if (Status == null || _leaders == null || !shown.SameLeaders(_shown))
            {
                _leaders = Mirror(shown);
            }

            _shown = shown;

            // ModeStatus は出した後も UI が持つので、得点の並びは使い回さずに毎回作る
            var sideScores = new[]
            {
                new SideScore(Side.Alpha, shown.AlphaScore),
                new SideScore(Side.Bravo, shown.BravoScore),
            };
            Status = new ModeStatus(
                shown.RemainingSeconds,
                sideScores,
                shown.OwnSide,
                shown.RespawnSeconds,
                shown.Ended,
                shown.Winner,
                new GuardianStatus(_leaders, shown.InMatch ? _localPlayerId : (int?)null, shown.LocalEliminated));
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
            _leaders = null;
            _shown = default;
            if (Status == null)
            {
                return false;
            }

            Status = null;
            return true;
        }

        // 同じチームに 2 つ届いたら先の 1 つを使う。両チームに同じ人が届いたら (壊れた行) 後の方を捨てる。写しの ModeSession が同じ人を 2 度入れられないため
        private static void ReadLeaders(SnapshotMessage snapshot, out int? alpha, out bool alphaFallen, out int? bravo, out bool bravoFallen)
        {
            alpha = null;
            bravo = null;
            alphaFallen = false;
            bravoFallen = false;
            var items = snapshot.ObjectiveItems;
            for (var i = 0; i < items.Count; i++)
            {
                ref var item = ref items[i];
                if (item.Kind != ObjectiveItemKind.Leader || !item.HasOwner)
                {
                    continue;
                }

                var fallen = item.State != ObjectiveItemState.Carried;
                if (item.Team == Team.Alpha && !alpha.HasValue && bravo != item.OwnerId)
                {
                    alpha = item.OwnerId;
                    alphaFallen = fallen;
                }
                else if (item.Team == Team.Bravo && !bravo.HasValue && alpha != item.OwnerId)
                {
                    bravo = item.OwnerId;
                    bravoFallen = fallen;
                }
            }
        }

        private static GuardianRespawn Mirror(Shown shown)
        {
            var leaders = new GuardianRespawn(new IndividualRespawn(0f));
            var session = new ModeSession(new TwoTeams(), float.MaxValue, Array.Empty<IScoreRule>(), leaders, Array.Empty<IWinCondition>());
            Put(session, leaders, Side.Alpha, shown.AlphaLeader, shown.AlphaFallen);
            Put(session, leaders, Side.Bravo, shown.BravoLeader, shown.BravoFallen);
            return leaders;
        }

        private static void Put(ModeSession session, GuardianRespawn leaders, Side side, int? leader, bool fallen)
        {
            if (!leader.HasValue)
            {
                return;
            }

            session.AddPlayer(leader.Value, side);
            leaders.SetLeader(session, leader.Value);
            if (fallen)
            {
                // 写しには倒れたことを伝える口が無いので、抜けたときと同じ Cancel でリーダーを倒れたことにする
                leaders.Cancel(leader.Value);
            }
        }

        private bool FindLocal(SnapshotMessage snapshot, out Team team, out int health)
        {
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId == _localPlayerId)
                {
                    team = row.Team;
                    health = row.Health;
                    return true;
                }
            }

            team = default;
            health = 0;
            return false;
        }

        private static float? RespawnSeconds(SnapshotMessage snapshot)
        {
            if (!snapshot.HasOwner || !snapshot.Owner.RespawnSeconds.HasValue)
            {
                return null;
            }

            return Math.Max(0, snapshot.Owner.RespawnSeconds.Value);
        }

        private readonly struct Shown : IEquatable<Shown>
        {
            public Shown(
                int remainingSeconds,
                int alphaScore,
                int bravoScore,
                Side? ownSide,
                float? respawnSeconds,
                bool ended,
                Side? winner,
                int? alphaLeader,
                bool alphaFallen,
                int? bravoLeader,
                bool bravoFallen,
                bool inMatch,
                bool localEliminated)
            {
                RemainingSeconds = remainingSeconds;
                AlphaScore = alphaScore;
                BravoScore = bravoScore;
                OwnSide = ownSide;
                RespawnSeconds = respawnSeconds;
                Ended = ended;
                Winner = winner;
                AlphaLeader = alphaLeader;
                AlphaFallen = alphaFallen;
                BravoLeader = bravoLeader;
                BravoFallen = bravoFallen;
                InMatch = inMatch;
                LocalEliminated = localEliminated;
            }

            public int RemainingSeconds { get; }

            public int AlphaScore { get; }

            public int BravoScore { get; }

            public Side? OwnSide { get; }

            public float? RespawnSeconds { get; }

            public bool Ended { get; }

            public Side? Winner { get; }

            public int? AlphaLeader { get; }

            public bool AlphaFallen { get; }

            public int? BravoLeader { get; }

            public bool BravoFallen { get; }

            public bool InMatch { get; }

            public bool LocalEliminated { get; }

            public bool SameLeaders(Shown other)
            {
                return AlphaLeader == other.AlphaLeader
                    && AlphaFallen == other.AlphaFallen
                    && BravoLeader == other.BravoLeader
                    && BravoFallen == other.BravoFallen;
            }

            public bool Equals(Shown other)
            {
                return SameLeaders(other)
                    && RemainingSeconds == other.RemainingSeconds
                    && AlphaScore == other.AlphaScore
                    && BravoScore == other.BravoScore
                    && OwnSide == other.OwnSide
                    && RespawnSeconds == other.RespawnSeconds
                    && Ended == other.Ended
                    && Winner == other.Winner
                    && InMatch == other.InMatch
                    && LocalEliminated == other.LocalEliminated;
            }

            public override bool Equals(object obj) => obj is Shown other && Equals(other);

            // HashCode.Combine は 8 個までしか取らないので、リーダーの欄を先にまとめる
            public override int GetHashCode() => HashCode.Combine(
                RemainingSeconds,
                AlphaScore,
                BravoScore,
                OwnSide,
                Ended,
                Winner,
                HashCode.Combine(AlphaLeader, AlphaFallen, BravoLeader, BravoFallen),
                LocalEliminated);
        }
    }
}
