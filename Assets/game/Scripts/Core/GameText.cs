using System;

namespace Fps.Core
{
    /// <summary>
    /// Core の書式 (<see cref="HudFormat"/> など) が画面に出す文字を引く先。今の言語の <see cref="StringTable"/> を返す関数を
    /// <see cref="Use"/> で受け取り、引くたびに呼ぶ (言語が変わった後に組み立てた文字は新しい言語になる)。
    /// ゲームでは Fps.Runtime の Localizer が渡す。
    /// 引き先が無い・null を返すときはキーそのものを返し、どのキーを引いたかが画面で分かるようにする。
    /// </summary>
    public static class GameText
    {
        private static Func<StringTable> _source;

        /// <summary>今の引き先 (<see cref="Use"/> で渡した関数)。無ければ null。テストが差し替えて戻すのに使う。</summary>
        public static Func<StringTable> Source => _source;

        /// <summary>文字を引く表を返す関数を source にする。null なら外す (以後はキーそのものを返す)。</summary>
        public static void Use(Func<StringTable> source)
        {
            _source = source;
        }

        /// <summary>今の表で key の文字列。表が無ければキーそのもの。</summary>
        /// <exception cref="ArgumentNullException">key が null。</exception>
        public static string Get(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            var table = _source?.Invoke();
            return table == null ? key : table.Get(key);
        }

        /// <summary>
        /// 今の表で key の文字列に args を差し込む (<see cref="StringTable.Format"/>)。args にある <see cref="LocalizedText"/> は
        /// 同じ表で文にしてから差し込む。表が無ければキーそのもの。
        /// </summary>
        /// <exception cref="ArgumentNullException">key か args が null。</exception>
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

        /// <summary>
        /// 今の表で text を文にする (<see cref="StringTable.Resolve"/>)。表が無ければ、<see cref="LocalizedText.Literal"/> はその文字列、
        /// それ以外はキーそのもの (<c>default</c> は空文字)。
        /// </summary>
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
