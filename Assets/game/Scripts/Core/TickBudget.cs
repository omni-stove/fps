using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // p95 を出すために記録した値はすべて持つ。120 秒を 60 fps で回して 7200 件ほどなので、ヒストグラムにはしない。
    public sealed class TickBudget
    {
        // 60 Hz の 1 フレーム (約 16.7 ms) のうち、サーバーの試合の処理に回せる分。
        public const double DefaultAverageBudgetMs = 4d;

        // これを超える tick が 1 つでもあれば、その 1 フレームは 60 Hz に収まらない恐れがある。
        public const double DefaultMaxBudgetMs = 12d;

        public const double Percentile = 95d;

        private readonly List<double> _samples = new List<double>();
        private double _sum;
        private double _max;

        public TickBudget()
            : this(DefaultAverageBudgetMs, DefaultMaxBudgetMs)
        {
        }

        public TickBudget(double averageBudgetMs, double maxBudgetMs)
        {
            ThrowIfNotPositiveFinite(nameof(averageBudgetMs), averageBudgetMs);
            ThrowIfNotPositiveFinite(nameof(maxBudgetMs), maxBudgetMs);
            AverageBudgetMs = averageBudgetMs;
            MaxBudgetMs = maxBudgetMs;
        }

        public double AverageBudgetMs { get; }

        public double MaxBudgetMs { get; }

        public int Count => _samples.Count;

        public double AverageMs => _samples.Count == 0 ? 0d : _sum / _samples.Count;

        public double MaxMs => _max;

        // 呼ぶたびに並べ替えるので、毎フレームではなく集計の終わりに読む。
        public double P95Ms
        {
            get
            {
                if (_samples.Count == 0)
                {
                    return 0d;
                }

                var sorted = new List<double>(_samples);
                sorted.Sort();
                var rank = (int)Math.Ceiling(Percentile / 100d * sorted.Count);
                return sorted[Math.Max(rank, 1) - 1];
            }
        }

        public void Record(double milliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(milliseconds), milliseconds, "a tick time must be finite and not negative");
            }

            _samples.Add(milliseconds);
            _sum += milliseconds;
            if (milliseconds > _max)
            {
                _max = milliseconds;
            }
        }

        public IReadOnlyList<string> Violations()
        {
            var violations = new List<string>();
            if (_samples.Count == 0)
            {
                violations.Add("no ticks were recorded");
                return violations;
            }

            if (AverageMs > AverageBudgetMs)
            {
                violations.Add("average " + Format(AverageMs) + " ms exceeds the budget of " + Format(AverageBudgetMs) + " ms");
            }

            if (MaxMs > MaxBudgetMs)
            {
                violations.Add("max " + Format(MaxMs) + " ms exceeds the budget of " + Format(MaxBudgetMs) + " ms");
            }

            return violations;
        }

        public bool IsWithinBudget => Violations().Count == 0;

        public override string ToString()
        {
            return "ticks=" + Count.ToString(CultureInfo.InvariantCulture)
                + " avg=" + Format(AverageMs) + "ms max=" + Format(MaxMs) + "ms p95=" + Format(P95Ms)
                + "ms (budget avg=" + Format(AverageBudgetMs) + "ms max=" + Format(MaxBudgetMs) + "ms)";
        }

        public static string Format(double milliseconds)
        {
            return milliseconds.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static void ThrowIfNotPositiveFinite(string name, double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite and greater than zero");
            }
        }
    }
}
