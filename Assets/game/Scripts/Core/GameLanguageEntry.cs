using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fps.Core
{
    /// <summary>言語の一覧 (<see cref="GameLanguageList"/>) の 1 行。言語・その言語自身での名前・その言語を選ぶ OS の言語。</summary>
    public sealed class GameLanguageEntry
    {
        /// <param name="language">言語。</param>
        /// <param name="nativeName">その言語自身での名前 (例: <c>日本語</c>)。</param>
        /// <param name="systemLanguages">初めて起動したときにこの言語を選ぶ OS の言語の名前 (Unity の <c>SystemLanguage</c> の名前。例: <c>Japanese</c>)。null なら無し。</param>
        /// <exception cref="ArgumentOutOfRangeException">language が <c>default</c>。</exception>
        /// <exception cref="ArgumentException">nativeName が null か空白だけ、または systemLanguages に null か空白だけのものがある。</exception>
        public GameLanguageEntry(GameLanguage language, string nativeName, IEnumerable<string> systemLanguages = null)
        {
            if (!language.IsDefined)
            {
                throw new ArgumentOutOfRangeException(nameof(language), language, "the language has no code");
            }

            if (string.IsNullOrWhiteSpace(nativeName))
            {
                throw new ArgumentException("the native name must not be empty", nameof(nativeName));
            }

            var systems = new List<string>();
            foreach (var system in systemLanguages ?? Array.Empty<string>())
            {
                if (string.IsNullOrWhiteSpace(system))
                {
                    throw new ArgumentException("a system language name must not be empty", nameof(systemLanguages));
                }

                systems.Add(system);
            }

            Language = language;
            NativeName = nativeName;
            SystemLanguages = new ReadOnlyCollection<string>(systems);
        }

        public GameLanguage Language { get; }

        public string NativeName { get; }

        public IReadOnlyList<string> SystemLanguages { get; }
    }
}
