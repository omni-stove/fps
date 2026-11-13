using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// フリーフォーオール (全員が敵の個人戦) のモード。1 人 1 陣営 (<see cref="FreeForAll"/>) で、別の人を倒すと 1 点 (<see cref="KillScore"/>)、
    /// 倒れたら RespawnSeconds 後に戻る (<see cref="IndividualRespawn"/>)。誰かが ScoreLimit に届くか (<see cref="ScoreLimitWin"/>)、
    /// 時間切れで最多得点の 1 人 (<see cref="TimeLimitWin"/>) が勝つ。最多得点が複数なら引き分け。
    /// </summary>
    public static class FreeForAllMode
    {
        public const float DefaultTimeLimitSeconds = 300f;
        public const int DefaultScoreLimit = 15;
        public const float DefaultRespawnSeconds = 3f;

        /// <summary>
        /// 部品を組んだ <see cref="ModeSession"/> を Waiting で返す。参加者は <see cref="ModeSession.Join"/> か、
        /// 他の人と重ならない陣営を指定した <see cref="ModeSession.AddPlayer(int, Side)"/> で入れる。
        /// </summary>
        /// <param name="timeLimitSeconds">制限時間 (秒)。0 より大きい値。</param>
        /// <param name="scoreLimit">勝利に必要なキル数。1 以上。</param>
        /// <param name="respawnSeconds">倒れてから戻るまでの秒数。0 以上の有限値。</param>
        /// <exception cref="ArgumentOutOfRangeException">値が範囲の外。</exception>
        public static ModeSession Create(float timeLimitSeconds, int scoreLimit, float respawnSeconds)
        {
            if (scoreLimit < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(scoreLimit), scoreLimit, "scoreLimit must be at least 1");
            }

            return new ModeSession(
                new FreeForAll(),
                timeLimitSeconds,
                new IScoreRule[] { new KillScore() },
                new IndividualRespawn(respawnSeconds),
                new IWinCondition[] { new ScoreLimitWin(scoreLimit), new TimeLimitWin() });
        }

        /// <summary>
        /// side の順位と 1 位との差。得点は HUD と同じく <see cref="ModeHudText.DisplayScore"/> で数え、同点は同じ順位にする
        /// (5, 3, 3, 1 なら 1 位, 2 位, 2 位, 4 位)。数えるのは得点を持つ陣営 (<see cref="ModeSession.Sides"/>) で、抜けた人の陣営も含む。
        /// </summary>
        /// <exception cref="ArgumentNullException">session が null。</exception>
        /// <exception cref="ArgumentException">side が session の陣営にない。</exception>
        public static FreeForAllStanding Standing(ModeSession session, Side side)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var sides = session.Sides;
            var own = -1;
            var found = false;
            foreach (var s in sides)
            {
                if (s == side)
                {
                    own = ModeHudText.DisplayScore(session.GetScore(s));
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                throw new ArgumentException($"{side} has not joined the mode", nameof(side));
            }

            var rank = 1;
            var bestOther = (int?)null;
            foreach (var s in sides)
            {
                if (s == side)
                {
                    continue;
                }

                var score = ModeHudText.DisplayScore(session.GetScore(s));
                if (score > own)
                {
                    rank++;
                }

                if (!bestOther.HasValue || score > bestOther.Value)
                {
                    bestOther = score;
                }
            }

            return new FreeForAllStanding(rank, sides.Count, own, bestOther.HasValue ? own - bestOther.Value : (int?)null);
        }

        /// <summary>HUD に出す順位の行。</summary>
        /// <example>
        /// 6 人中 3 位で 1 位と 4 点差なら <c>順位 3/6  1位と -4</c>、単独 1 位で 2 位と 2 点差なら <c>順位 1/6  2位と +2</c>、
        /// 1 位が並んでいれば <c>順位 1/6  1位と同点</c>、1 人だけなら <c>順位 1/1</c>
        /// </example>
        public static string RankText(FreeForAllStanding standing)
        {
            var text = "順位 " + Number(standing.Rank) + "/" + Number(standing.SideCount);
            if (!standing.Margin.HasValue)
            {
                return text;
            }

            var margin = standing.Margin.Value;
            if (margin > 0)
            {
                return text + "  2位と +" + Number(margin);
            }

            return margin == 0 ? text + "  1位と同点" : text + "  1位と " + Number(margin);
        }

        /// <summary>今の順位を <see cref="RankText"/> で返す。</summary>
        public static string RankText(ModeSession session, Side side) => RankText(Standing(session, side));

        private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>フリーフォーオールでの 1 人 (1 陣営) の順位 (<see cref="FreeForAllMode.Standing"/>)。</summary>
    public readonly struct FreeForAllStanding : IEquatable<FreeForAllStanding>
    {
        /// <summary>順位。1 から。自分より得点の多い陣営の数 + 1 なので、同点は同じ順位になる。</summary>
        public int Rank { get; }

        /// <summary>順位を数えた陣営の数 (自分を含む)。</summary>
        public int SideCount { get; }

        /// <summary>自分の得点 (HUD に出す整数)。</summary>
        public int Score { get; }

        /// <summary>
        /// 自分の得点から、自分以外でいちばん多い得点を引いた値。正なら単独 1 位で 2 位との差、0 なら 1 位に並んでいる、
        /// 負なら 1 位との差。自分しかいなければ null。
        /// </summary>
        public int? Margin { get; }

        /// <summary>単独で 1 位なら true。</summary>
        public bool IsSoleLeader => Margin.HasValue ? Margin.Value > 0 : SideCount == 1;

        public FreeForAllStanding(int rank, int sideCount, int score, int? margin)
        {
            if (sideCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(sideCount), sideCount, "sideCount must be at least 1");
            }

            if (rank < 1 || rank > sideCount)
            {
                throw new ArgumentOutOfRangeException(nameof(rank), rank, "rank must be between 1 and sideCount");
            }

            Rank = rank;
            SideCount = sideCount;
            Score = score;
            Margin = margin;
        }

        public bool Equals(FreeForAllStanding other)
        {
            return Rank == other.Rank && SideCount == other.SideCount && Score == other.Score && Margin == other.Margin;
        }

        public override bool Equals(object obj) => obj is FreeForAllStanding other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Rank;
                hash = (hash * 397) ^ SideCount;
                hash = (hash * 397) ^ Score;
                return (hash * 397) ^ (Margin ?? int.MinValue);
            }
        }

        public override string ToString()
        {
            return $"rank {Rank}/{SideCount} score {Score} margin {(Margin.HasValue ? Margin.Value.ToString(CultureInfo.InvariantCulture) : "-")}";
        }
    }
}
