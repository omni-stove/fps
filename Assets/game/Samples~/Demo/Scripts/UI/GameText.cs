using System;

namespace Shooter.Runtime
{
    // 表を覚えずに引くたびに関数を呼ぶのは、言語が変わった後に組み立てた文字を新しい言語にするため。
    // 引き先が無いときにキーそのものを返すのは、どのキーを引いたかが画面で分かるようにするため。
    public static class GameText
    {
        private static Func<StringTable> _source;

        public static Func<StringTable> Source => _source;

        public static void Use(Func<StringTable> source)
        {
            _source = source;
        }

        public static string Get(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var table = _source?.Invoke();
            return table == null ? key : table.Get(key);
        }

        public static string Format(string key, params object[] args)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            var table = _source?.Invoke();
            return table == null ? key : table.Format(key, ResolveNested(table, args));
        }

        public static string Resolve(LocalizedText text)
        {
            var table = _source?.Invoke();
            if (table != null)
            {
                return table.Resolve(text);
            }

            return text.Key;
        }

        // 呼び出し側の配列を書き換えないよう、入れ子の文があるときだけ写しを作る
        private static object[] ResolveNested(StringTable table, object[] args)
        {
            object[] resolved = null;
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] is LocalizedText nested)
                {
                    resolved ??= (object[])args.Clone();
                    resolved[i] = table.Resolve(nested);
                }
            }

            return resolved ?? args;
        }
    }
}
