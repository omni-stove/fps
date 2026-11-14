namespace Fps.Core
{
    public readonly struct StaggerVerdict
    {
        public StaggerVerdict(StaggerOutcome outcome, StaggerKind kind, float seconds, float accumulatedDamage, float threshold)
        {
            Outcome = outcome;
            Kind = kind;
            Seconds = seconds;
            AccumulatedDamage = accumulatedDamage;
            Threshold = threshold;
        }

        public StaggerOutcome Outcome { get; }

        // 怯まなかったときも、この被弾が起こし得た種類を入れる。ログで何が防がれたかを出すため。
        public StaggerKind Kind { get; }

        public float Seconds { get; }

        public float AccumulatedDamage { get; }

        public float Threshold { get; }

        public bool Staggered => Outcome == StaggerOutcome.Staggered;
    }
}
