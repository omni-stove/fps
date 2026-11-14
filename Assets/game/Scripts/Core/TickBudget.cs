using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// サーバーの 1 tick にかかった時間 (ミリ秒) を集め、平均・最大・p95 を出して予算 (<see cref="AverageBudgetMs"/>・<see cref="MaxBudgetMs"/>) と比べる。
    /// 時間は外から渡す。
    /// </summary>
    /// <remarks>
    /// p95 は nearest-rank 法 (小さい順に並べて ceil(0.95 × 件数) 番目) で、予算とは比べずに報告だけする。
    /// p95 を出すために記録した値はすべて持つ。120 秒を 60 fps で回して 7200 件ほどなので、ヒストグラムにはしない。
    /// </remarks>
    public sealed class TickBudget
    {
        /// <summary>平均の予算の既定値 (ミリ秒)。60 Hz の 1 フレーム (約 16.7 ms) のうち、サーバーの試合の処理に回せる分。</summary>
        public const double DefaultAverageBudgetMs = 4d;

        /// <summary>最大の予算の既定値 (ミリ秒)。これを超える tick が 1 つでもあれば、その 1 フレームは 60 Hz に収まらない恐れがある。</summary>
        public const double DefaultMaxBudgetMs = 12d;

        /// <summary><see cref="P95Ms"/> の百分位。</summary>
        public const double Percentile = 95d;

        private readonly List<double> _samples = new List<double>();
        private double _sum;
        private double _max;

        public TickBudget()
            : this(DefaultAverageBudgetMs, DefaultMaxBudgetMs)
        {
        }

        /// <param name="averageBudgetMs">平均の予算 (ミリ秒)。0 より大きい有限値。</param>
        /// <param name="maxBudgetMs">最大の予算 (ミリ秒)。0 より大きい有限値。</param>
        /// <exception cref="ArgumentOutOfRangeException">どちらかが 0 以下・NaN・無限大。</exception>
        public TickBudget(double averageBudgetMs, double maxBudgetMs)
        {
            ThrowIfNotPositiveFinite(nameof(averageBudgetMs), averageBudgetMs);
            ThrowIfNotPositiveFinite(nameof(maxBudgetMs), maxBudgetMs);
            AverageBudgetMs = averageBudgetMs;
            MaxBudgetMs = maxBudgetMs;
        }

        /// <summary>平均の予算 (ミリ秒)。平均がこれを超えたら予算オーバー。</summary>
        public double AverageBudgetMs { get; }

        /// <summary>最大の予算 (ミリ秒)。最大がこれを超えたら予算オーバー。</summary>
        public double MaxBudgetMs { get; }

        /// <summary>記録した tick の数。</summary>
        public int Count => _samples.Count;

        /// <summary>平均 (ミリ秒)。1 つも記録していなければ 0。</summary>
        public double AverageMs => _samples.Count == 0 ? 0d : _sum / _samples.Count;

        /// <summary>最大 (ミリ秒)。1 つも記録していなければ 0。</summary>
        public double MaxMs => _max;

        /// <summary>
        /// <see cref="Percentile"/> 百分位 (ミリ秒、nearest-rank 法)。1 つも記録していなければ 0。
        /// 呼ぶたびに並べ替えるので、毎フレームではなく集計の終わりに読む。
        /// </summary>
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

        /// <summary>1 tick の時間を足す。</summary>
        /// <param name="milliseconds">0 以上の有限値。</param>
        /// <exception cref="ArgumentOutOfRangeException">負・NaN・無限大。</exception>
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

        /// <summary>
        /// 予算を守れていない理由を並べる。1 つも記録していない (計れていない) ときもそれを理由にする。守れていれば空。
        /// 予算ちょうどは守れているとみなす。
        /// </summary>
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

        /// <summary>1 つ以上記録していて、平均と最大がどちらも予算以内なら true。</summary>
        public bool IsWithinBudget => Violations().Count == 0;

        /// <summary>ログに出す 1 行。<c>ticks=… avg=… max=… p95=… (budget avg=… max=…)</c>。</summary>
        public override string ToString()
        {
            return "ticks=" + Count.ToString(CultureInfo.InvariantCulture)
                + " avg=" + Format(AverageMs) + "ms max=" + Format(MaxMs) + "ms p95=" + Format(P95Ms)
                + "ms (budget avg=" + Format(AverageBudgetMs) + "ms max=" + Format(MaxBudgetMs) + "ms)";
        }

        /// <summary>ミリ秒をログと JSON に出す形 (小数 4 桁まで、カルチャによらない) にする。</summary>
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
