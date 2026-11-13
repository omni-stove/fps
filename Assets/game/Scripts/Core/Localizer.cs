using System;

namespace Fps.Core
{
    /// <summary>
    /// 今の言語の表 (<see cref="Current"/>) を持つ。言語を切り替える側が表を差し替え、画面の側は <see cref="Changed"/> を受けて文を引き直す。
    /// Unity のメインスレッドからだけ触る前提で、ロックは取らない。
    /// </summary>
    public static class Localizer
    {
        private static TextTable _current = TextTable.Empty;

        /// <summary>表が別のものに差し替わった後に呼ばれる。同じ表を入れ直したときは呼ばれない。</summary>
        public static event Action Changed;

        /// <summary>今の表。既定は空の表 (どのキーもキーそのものになる)。</summary>
        /// <exception cref="ArgumentNullException">null を入れようとした。</exception>
        public static TextTable Current
        {
            get => _current;
            set
            {
                if (value == null)
                {
                    throw new ArgumentNullException(nameof(value));
                }

                if (ReferenceEquals(value, _current))
                {
                    return;
                }

                _current = value;
                Changed?.Invoke();
            }
        }

        /// <summary>今の表で文にする。<see cref="TextTable.Resolve"/> と同じ。</summary>
        public static string Resolve(LocalizedText text)
        {
            return _current.Resolve(text);
        }
    }
}
