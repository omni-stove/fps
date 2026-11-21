using System;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    // オフラインの ModeRunnerBase と同じく、試合が終わったら自分の結果を CombatEvents.MatchFinished に出し、XP と腕前レートを記録させる。
    public sealed class NetV2MatchResultPart : INetV2ClientPart
    {
        private readonly MatchClient _client;

        // 終わった試合の画面の途中でつないだときに遊んでいない試合を数えないよう、終わっていない状態を見てから出す
        private bool _sawPlaying;
        private bool _raised;
        private bool _disposed;

        public NetV2MatchResultPart(MatchClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public bool Handle(IMessage message)
        {
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

            _raised = true;
            var result = ResultOf(snapshot, _client.PlayerId);
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

        // 観戦しているだけで自分の行が無ければ、自分の勝ち負けが無いので null。
        // 専用サーバーの試合は相手の腕前を送ってこないので、相手は初めの値として腕前を動かす
        public static MatchResult? ResultOf(SnapshotMessage snapshot, int localPlayerId)
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
            else
            {
                var winner = match.HasWinner ? Side.FromTeam(match.Winner) : (Side?)null;
                outcome = OutcomeOf(winner, Side.FromTeam(row.Team));
                ownScore = row.Team == Team.Alpha ? TeamScore(match, match.AlphaScore, match.AlphaPoints) : TeamScore(match, match.BravoScore, match.BravoPoints);
            }

            return new MatchResult(outcome, Math.Max(0, row.Kills), Math.Max(0, row.Deaths), Math.Max(0, ownScore), true);
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
