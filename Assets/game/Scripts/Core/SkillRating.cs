using System;

namespace Shooter.Core
{
    public sealed class SkillRating
    {
        // Elo の決まりの幅。差がこの値で勝つ見込みが 10 倍になる。動きの大きさは KFactor で変えられるので、調整値にはしない。
        public const double Scale = 400d;

        public SkillRating(SkillSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public SkillSpec Spec { get; }

        public static double Expected(int own, int opponents)
        {
            return 1d / (1d + Math.Pow(10d, (opponents - (double)own) / Scale));
        }

        public static double ScoreOf(MatchOutcome outcome)
        {
            switch (outcome)
            {
                case MatchOutcome.Won:
                    return 1d;
                case MatchOutcome.Draw:
                    return 0.5d;
                case MatchOutcome.Lost:
                    return 0d;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "outcome must be a MatchOutcome value");
            }
        }

        public int Next(int own, int opponents, MatchOutcome outcome)
        {
            var change = Spec.KFactor * (ScoreOf(outcome) - Expected(own, opponents));
            var next = (long)own + (long)Math.Round(change, MidpointRounding.AwayFromZero);
            return Clamp(next);
        }

        public int Clamp(long rating)
        {
            return (int)Math.Max(Spec.MinRating, Math.Min(Spec.MaxRating, rating));
        }
    }
}
