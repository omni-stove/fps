using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using RaidOutcome = Shooter.Core.ExtractionOutcome;

namespace Shooter.NetV2
{
    // オフラインの ModeRunnerBase と同じく、試合が終わったら自分の結果を CombatEvents.MatchFinished に出し、XP と腕前レートを記録させる。
    public sealed class NetV2MatchResultPart : INetV2ClientPart
    {
        private readonly MatchClient _client;
        private readonly ClientExtractionStatus _extraction;

        // 終わった試合の画面の途中でつないだときに遊んでいない試合を数えないよう、終わっていない状態を見てから出す
        private bool _sawPlaying;
        private bool _raised;
        private bool _disposed;

        public NetV2MatchResultPart(MatchClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));

            // Receive しか使わず、自分の Id も調整値も読まない
            _extraction = new ClientExtractionStatus(0);
        }

        // 脱出の勝ち負けはスナップショットに無く、出撃の結果の知らせでだけ届くので、受けて覚えるだけにする
        public bool Handle(IMessage message)
        {
            if (message is StashStateMessage stash)
            {
                _extraction.Receive(stash);
            }

            return false;
        }

        // Ended のスナップショットは次の試合が始まるまで毎 tick 届くので、出したら次に終わっていない状態を見るまで出さない
        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null)
            {
                return;
            }

            if (snapshot.Match.State != GameModeState.Ended)
            {
                _sawPlaying = true;
                _raised = false;
                return;
            }

            if (!_sawPlaying || _raised)
            {
                return;
            }

            // 出撃の結果は Reliable、スナップショットは Unreliable で届く順が決まらないので、結果が届くまで確定させず Ended の間に届いた時点で出す
            if (snapshot.Match.Objective == MatchObjective.Extraction
                && snapshot.Players.IndexOf(_client.PlayerId) >= 0
                && _extraction.Outcome == RaidOutcome.Raiding)
            {
                return;
            }

            _raised = true;
            var result = ResultOf(snapshot, _client.PlayerId, _extraction.Outcome);
            if (result.HasValue)
            {
                CombatEvents.RaiseMatchFinished(result.Value);
            }
        }

        public void Tick(float deltaSeconds)
        {
        }

        public void Dispose()
        {
            _disposed = true;
        }

        // 観戦しているだけで自分の行が無ければ、自分の勝ち負けが無いので null。Battle Royale で分隊に入っていなければ同じく null。
        // 専用サーバーの試合は相手の腕前を送ってこないので、相手は初めの値として腕前を動かす
        public static MatchResult? ResultOf(SnapshotMessage snapshot, int localPlayerId, RaidOutcome raidOutcome = RaidOutcome.Raiding)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var own = snapshot.Players.IndexOf(localPlayerId);
            if (own < 0)
            {
                return null;
            }

            var row = snapshot.Players[own];
            if (snapshot.Match.Objective == MatchObjective.BattleRoyale && row.OutsideSquad)
            {
                return null;
            }

            var match = snapshot.Match;
            MatchOutcome outcome;
            int ownScore;
            if (match.Objective == MatchObjective.FreeForAll)
            {
                var status = ClientFreeForAllStatus.From(snapshot, localPlayerId);
                var ownSide = new Side(localPlayerId);
                outcome = OutcomeOf(status.Winner, ownSide);
                ownScore = ScoreOf(status, ownSide);
            }
            else if (match.Objective == MatchObjective.GunGame)
            {
                // サーバーはスナップショットの勝者をチームでしか送れず、同じチームの負けた人まで勝ちになるので、段が単独で最も高い人を勝者にする
                outcome = OutcomeOf(GunGameLeader(snapshot.Players), new Side(localPlayerId));
                ownScore = row.Kills;
            }
            else if (match.Objective == MatchObjective.BattleRoyale)
            {
                // サーバーは勝者のチームを送らず分隊で決めるので、分隊の立っている人数から勝った分隊を引く
                outcome = OutcomeOf(ClientBattleRoyaleStatus.LeadingSquad(snapshot.Players), new Side(row.SquadId));
                ownScore = 0;
            }
            else if (match.Objective == MatchObjective.Extraction)
            {
                // 勝ちは脱出できたかで決まり、チームの勝者は送られない。出撃の結果は StashStateMessage でしか届かない
                outcome = raidOutcome == RaidOutcome.Extracted ? MatchOutcome.Won
                    : raidOutcome == RaidOutcome.Raiding ? MatchOutcome.Draw
                    : MatchOutcome.Lost;
                ownScore = 0;
            }
            else
            {
                var winner = match.HasWinner ? Side.FromTeam(match.Winner) : (Side?)null;
                outcome = OutcomeOf(winner, Side.FromTeam(row.Team));
                ownScore = row.Team == Team.Alpha ? TeamScore(match, match.AlphaScore, match.AlphaPoints) : TeamScore(match, match.BravoScore, match.BravoPoints);
            }

            return new MatchResult(outcome, Math.Max(0, row.Kills), Math.Max(0, row.Deaths), Math.Max(0, ownScore), true);
        }

        private static Side? GunGameLeader(SnapshotList<PlayerSnapshot> players)
        {
            int? leader = null;
            var best = int.MinValue;
            var tied = false;
            for (var i = 0; i < players.Count; i++)
            {
                var tier = players[i].GunGameTier;
                if (tier > best)
                {
                    best = tier;
                    leader = players[i].PlayerId;
                    tied = false;
                }
                else if (tier == best)
                {
                    tied = true;
                }
            }

            return leader.HasValue && !tied ? new Side(leader.Value) : (Side?)null;
        }

        private static MatchOutcome OutcomeOf(Side? winner, Side ownSide)
        {
            return !winner.HasValue ? MatchOutcome.Draw : winner.Value == ownSide ? MatchOutcome.Won : MatchOutcome.Lost;
        }

        // HUD (NetV2Hud) と同じく、目的のある試合は目的の点、目的の無い試合はキルの点を陣営の得点とする
        private static int TeamScore(in MatchSnapshot match, int score, int points)
        {
            return match.Objective == MatchObjective.None ? score : points;
        }

        private static int ScoreOf(ModeStatus status, Side ownSide)
        {
            foreach (var entry in status.SideScores)
            {
                if (entry.Side == ownSide)
                {
                    return entry.Score > 0d ? (int)Math.Min(int.MaxValue, Math.Floor(entry.Score)) : 0;
                }
            }

            return 0;
        }
    }
}
