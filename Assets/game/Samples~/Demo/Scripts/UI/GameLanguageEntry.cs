using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shooter.Runtime
{
    public sealed class GameLanguageEntry
    {
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
