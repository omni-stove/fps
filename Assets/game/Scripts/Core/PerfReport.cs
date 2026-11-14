using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// 性能の計測 (PerfSmokeRun) の結果を <c>Logs/perf.json</c> に書く JSON にする。
    /// 平均・最大・p95 とその予算、守れたか (<c>passed</c>)、守れなかった理由 (<c>violations</c>) を、計った条件と一緒に出す。
    /// </summary>
    public static class PerfReport
    {
        /// <param name="scene">計ったシーンの名前。</param>
        /// <param name="bots">計っている間の Bot の数。0 以上。</param>
        /// <param name="measuredSeconds">計った秒数。0 以上の有限値。</param>
        /// <param name="budget">集計した tick の時間と予算。</param>
        /// <param name="problems">予算のほかに見つかった問題 (エラーのログなど)。null なら無し。どれかがあれば <c>passed</c> は false。</param>
        public static string ToJson(string scene, int bots, double measuredSeconds, TickBudget budget, IReadOnlyList<string> problems)
        {
            if (scene == null)
            {
                throw new ArgumentNullException(nameof(scene));
            }

            if (budget == null)
            {
                throw new ArgumentNullException(nameof(budget));
            }

            if (bots < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bots), bots, "bots must not be negative");
            }

            if (double.IsNaN(measuredSeconds) || double.IsInfinity(measuredSeconds) || measuredSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(measuredSeconds), measuredSeconds, "measuredSeconds must be finite and not negative");
            }

            var violations = new List<string>(budget.Violations());
            if (problems != null)
            {
                violations.AddRange(problems);
            }

            var json = new StringBuilder();
            json.Append("{\n");
            AppendField(json, "scene", Quote(scene));
            AppendField(json, "bots", bots.ToString(CultureInfo.InvariantCulture));
            AppendField(json, "measuredSeconds", TickBudget.Format(measuredSeconds));
            AppendField(json, "ticks", budget.Count.ToString(CultureInfo.InvariantCulture));
            AppendField(json, "averageMs", TickBudget.Format(budget.AverageMs));
            AppendField(json, "maxMs", TickBudget.Format(budget.MaxMs));
            AppendField(json, "p95Ms", TickBudget.Format(budget.P95Ms));
            AppendField(json, "averageBudgetMs", TickBudget.Format(budget.AverageBudgetMs));
            AppendField(json, "maxBudgetMs", TickBudget.Format(budget.MaxBudgetMs));
            AppendField(json, "passed", violations.Count == 0 ? "true" : "false");

            json.Append("  \"violations\": [");
            for (var i = 0; i < violations.Count; i++)
            {
                json.Append(i == 0 ? "\n    " : ",\n    ").Append(Quote(violations[i] ?? string.Empty));
            }

            json.Append(violations.Count == 0 ? "]\n" : "\n  ]\n");
            json.Append("}\n");
            return json.ToString();
        }

        private static void AppendField(StringBuilder json, string name, string value)
        {
            json.Append("  ").Append(Quote(name)).Append(": ").Append(value).Append(",\n");
        }

        /// <summary>text を JSON の文字列 (引用符付き) にする。</summary>
        public static string Quote(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var quoted = new StringBuilder(text.Length + 2);
            quoted.Append('"');
            foreach (var c in text)
            {
                switch (c)
                {
                    case '"':
                        quoted.Append("\\\"");
                        break;
                    case '\\':
                        quoted.Append("\\\\");
                        break;
                    case '\n':
                        quoted.Append("\\n");
                        break;
                    case '\r':
                        quoted.Append("\\r");
                        break;
                    case '\t':
                        quoted.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            quoted.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            quoted.Append(c);
                        }

                        break;
                }
            }

            quoted.Append('"');
            return quoted.ToString();
        }
    }
}
