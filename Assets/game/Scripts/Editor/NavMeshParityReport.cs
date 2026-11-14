using System.Collections.Generic;
using System.Globalization;

namespace Fps.Editor
{
    public sealed class NavMeshParityReport
    {
        public int Pairs { get; set; }

        public int UnityComplete { get; set; }

        public int CoreComplete { get; set; }

        public int CoreWithLink { get; set; }

        public float MaxLengthDifference { get; set; }

        public List<string> Mismatches { get; } = new List<string>();

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} pairs, Unity complete {1}, Core complete {2}, Core with a link {3}, max length difference {4:F3} m, {5} mismatch(es)",
                Pairs, UnityComplete, CoreComplete, CoreWithLink, MaxLengthDifference, Mismatches.Count);
        }
    }
}
