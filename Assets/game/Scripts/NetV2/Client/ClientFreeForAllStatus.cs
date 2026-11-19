using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 専用サーバーの FFA の状態を、オフラインの FreeForAllRunner が GameSignals.ModeStatus に出すのと同じ形の ModeStatus にする。
    // 陣営はプレイヤー Id と同じ番号で、点はプレイヤーの行のキル数に 1 キルの点 (MatchSnapshot.AlphaPoints) を掛けたもの。
    public static class ClientFreeForAllStatus
    {
        // FFA の試合でなければ null
        public static ModeStatus From(SnapshotMessage snapshot, int localPlayerId)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var match = snapshot.Match;
            if (match.Objective != MatchObjective.FreeForAll)
            {
                return null;
            }

            var pointsPerKill = PointsPerKill(match);
            var players = snapshot.Players;
            var count = 0;
            for (var i = 0; i < players.Count; i++)
            {
                if (players[i].PlayerId >= 0)
                {
                    count++;
                }
            }

            // ModeStatus は出した後も UI が持つので、使い回さずに毎回作る
            var scores = new SideScore[count];
            var next = 0;
            var ownScore = (int?)null;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (row.PlayerId < 0)
                {
                    continue;
                }

                var score = ScoreOf(row.Kills, pointsPerKill);
                scores[next++] = new SideScore(new Side(row.PlayerId), score);
                if (row.PlayerId == localPlayerId)
                {
                    ownScore = score;
                }
            }

            // オフラインの ModeSession.Sides と同じく陣営の番号の順に並べる
            Array.Sort(scores, (a, b) => a.Side.CompareTo(b.Side));

            var ended = match.State == GameModeState.Ended;
            var ownSide = ownScore.HasValue ? new Side(localPlayerId) : (Side?)null;
            return new ModeStatus(
                match.RemainingSeconds,
                scores,
                ownSide,
                ownScore.HasValue ? RespawnSeconds(snapshot) : null,
                ended,
                ended ? SoleLeader(scores) : null,
                ownScore.HasValue ? Standing(scores, localPlayerId, ownScore.Value) : (object)null);
        }

        // 古いサーバーや壊れた値で 0 以下が届いても、キル数をそのまま点にする
        public static int PointsPerKill(in MatchSnapshot match)
        {
            return match.AlphaPoints >= 1 ? match.AlphaPoints : FreeForAllMode.DefaultPointsPerKill;
        }

        private static float? RespawnSeconds(SnapshotMessage snapshot)
        {
            if (!snapshot.HasOwner || !snapshot.Owner.RespawnSeconds.HasValue)
            {
                return null;
            }

            return Math.Max(0, snapshot.Owner.RespawnSeconds.Value);
        }

        public static int ScoreOf(int kills, int pointsPerKill)
        {
            if (kills <= 0 || pointsPerKill <= 0)
            {
                return 0;
            }

            return (int)Math.Min(int.MaxValue, (long)kills * pointsPerKill);
        }

        // オフラインの FreeForAllMode.Standing と同じ数え方 (自分より点の多い人の数 + 1 が順位、差は自分以外の最高点との差)
        private static FreeForAllStanding Standing(SideScore[] scores, int localPlayerId, int ownScore)
        {
            var rank = 1;
            var bestOther = (int?)null;
            foreach (var entry in scores)
            {
                if (entry.Side.Id == localPlayerId)
                {
                    continue;
                }

                var score = (int)entry.Score;
                if (score > ownScore)
                {
                    rank++;
                }

                if (!bestOther.HasValue || score > bestOther.Value)
                {
                    bestOther = score;
                }
            }

            return new FreeForAllStanding(rank, scores.Length, ownScore, bestOther.HasValue ? ownScore - bestOther.Value : (int?)null);
        }

        // 時間切れで並んだら引き分けにするオフラインの TimeLimitWin (ModeSession.LeadingSide) にそろえる。
        // キル数の上限に届いて終わったときは、届いた 1 人だけが最高点になる
        private static Side? SoleLeader(SideScore[] scores)
        {
            Side? leader = null;
            var best = double.NegativeInfinity;
            var tied = false;
            foreach (var entry in scores)
            {
                if (entry.Score > best)
                {
                    best = entry.Score;
                    leader = entry.Side;
                    tied = false;
                }
                else if (entry.Score == best)
                {
                    tied = true;
                }
            }

            return tied ? null : leader;
        }
    }
}
