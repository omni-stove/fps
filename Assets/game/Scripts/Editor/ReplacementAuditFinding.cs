using System;

namespace Shooter.Editor
{
    public enum ReplacementAuditArea
    {
        Presentation,

        Strings,

        Reference,
    }

    public sealed class ReplacementAuditFinding
    {
        public ReplacementAuditFinding(ReplacementAuditArea area, string where, string problem)
        {
            if (string.IsNullOrWhiteSpace(where))
            {
                throw new ArgumentException("where must not be empty", nameof(where));
            }

            if (string.IsNullOrWhiteSpace(problem))
            {
                throw new ArgumentException("problem must not be empty", nameof(problem));
            }

            Area = area;
            Where = where;
            Problem = problem;
        }

        public ReplacementAuditArea Area { get; }

        public string Where { get; }

        public string Problem { get; }

        public override string ToString()
        {
            return $"[{Area}] {Where}: {Problem}";
        }
    }
}
