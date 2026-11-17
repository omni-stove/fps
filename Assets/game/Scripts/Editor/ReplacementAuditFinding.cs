using System;

namespace Shooter.Editor
{
    /// <summary><see cref="ReplacementAudit"/> が見つけた差し替え漏れの種類。</summary>
    public enum ReplacementAuditArea
    {
        /// <summary>音とエフェクト (<see cref="Shooter.Runtime.PresentationCatalog"/>) の漏れ。</summary>
        Presentation,

        /// <summary>文言のキーと、game 側の言語の表の漏れ。</summary>
        Strings,

        /// <summary>シーン・プレハブ・アセットの必須の参照 (<see cref="Shooter.Runtime.RequiredReferenceAttribute"/>) の漏れ。</summary>
        Reference,
    }

    /// <summary><see cref="ReplacementAudit"/> が見つけた差し替え漏れ 1 つ。</summary>
    public sealed class ReplacementAuditFinding
    {
        /// <exception cref="ArgumentException">where か problem が空。</exception>
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

        /// <summary>どこの漏れか (アセットのパス、シーンのパスと階層、言語コードなど)。</summary>
        public string Where { get; }

        /// <summary>何が足りないか。</summary>
        public string Problem { get; }

        public override string ToString()
        {
            return $"[{Area}] {Where}: {Problem}";
        }
    }
}
