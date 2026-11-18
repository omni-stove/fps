using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 調整値の JSON の読み取りに共通する手順。各 *Json が同じ断り方 (TuningJsonException と項目のパス) をそろえるために置く。
    internal static class SpecJsonReader
    {
        public static T Read<T>(string text, string sourceName, string kind, IReadOnlyList<string> itemNames, Func<ServerDataJson.Fields, T> build)
        {
            if (build == null)
            {
                throw new ArgumentNullException(nameof(build));
            }

            var source = ServerDataJson.SourceOf(sourceName, kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, kind, ServerDataJson.ParseDocument(text, source, kind), string.Empty, itemNames);
                return build(fields);
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        // Spec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、知っている名前ならその項目を、知らなければ囲みを示す。
        public static ServerDataJsonException Rejected(ServerDataJson.Fields fields, IReadOnlyList<string> known, ArgumentException e)
        {
            foreach (var name in known)
            {
                if (string.Equals(name, e.ParamName, StringComparison.Ordinal))
                {
                    return fields.Rejected(fields.ItemPath(name), e.Message, e);
                }
            }

            return fields.Rejected(fields.Path.Length == 0 ? null : fields.Path, e.Message, e);
        }

        public static T Build<T>(ServerDataJson.Fields fields, IReadOnlyList<string> known, Func<T> build)
        {
            try
            {
                return build();
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, known, e);
            }
        }

        // 一覧の項目は書かなければ null を返し、Spec の既定の一覧を使わせる。空の配列は「無し」と読む。
        public static List<T> ReadList<T>(ServerDataJson.Fields fields, string arrayName, IReadOnlyList<string> known, Func<ServerDataJson.Fields, T> read)
        {
            if (fields.Find(arrayName) == null)
            {
                return null;
            }

            var nodes = fields.Array(arrayName);
            var list = new List<T>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                var entry = fields.Element(nodes[i], arrayName, i, known);
                list.Add(Build(entry, known, () => read(entry)));
            }

            return list;
        }

        public static List<string> ReadStrings(ServerDataJson.Fields fields, string arrayName)
        {
            if (fields.Find(arrayName) == null)
            {
                return null;
            }

            var nodes = fields.Array(arrayName);
            var list = new List<string>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                list.Add(fields.ToText(fields.ElementPath(arrayName, i), nodes[i]));
            }

            return list;
        }

        public static string RequireText(ServerDataJson.Fields fields, string name)
        {
            return fields.ToText(fields.ItemPath(name), fields.Require(name));
        }
    }
}
