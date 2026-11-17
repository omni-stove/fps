using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// アシストの調整値 (<see cref="AssistSpec"/>) を JSON から読む。項目の名前は <see cref="AssistSpec"/> のコンストラクターの引数名で、
    /// 省略した項目は <see cref="AssistSpec.Default"/> の値になる。
    /// </summary>
    public static class AssistJson
    {
        public const string WindowSecondsItem = "windowSeconds";
        public const string MinDamageItem = "minDamage";

        private const string _kind = "assist JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            WindowSecondsItem,
            MinDamageItem,
        };

        /// <param name="text">JSON の文字列。先頭の BOM は読み飛ばす。</param>
        /// <param name="sourceName">例外の文に出す読み込み元の名前 (ファイルのパスなど)。</param>
        /// <exception cref="ArgumentNullException">text が null。</exception>
        /// <exception cref="TuningJsonException">
        /// JSON の文法に合わない、知らない項目がある、型が合わない、または値が <see cref="AssistSpec"/> の検査に通らない。
        /// </exception>
        public static AssistSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                try
                {
                    return new AssistSpec(
                        fields.ReadFloat(WindowSecondsItem, AssistSpec.DefaultWindowSeconds),
                        fields.ReadInt(MinDamageItem, AssistSpec.DefaultMinDamage));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(Known(e.ParamName) ? e.ParamName : null, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        // AssistSpec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
        private static bool Known(string name)
        {
            foreach (var item in ItemNames)
            {
                if (string.Equals(item, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
