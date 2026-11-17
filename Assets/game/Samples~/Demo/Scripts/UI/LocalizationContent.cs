using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Shooter.Runtime
{
    // 入口 (GameContent) はキットの物で言語を扱わないので、Resources の外に置いた表を重ねる先は UI の側に別に持つ。
    // 名前ではなく型で探すので、Resources/ShooterKit/ に好きな名前で 1 つ置けばよい。
    [CreateAssetMenu(fileName = "LocalizationContent", menuName = "Shooter/Localization Content")]
    public sealed class LocalizationContent : ScriptableObject
    {
        private static LocalizationContent _override;
        private static LocalizationContent _found;
        private static bool _searched;

        [SerializeField, Tooltip("Text tables per language, layered after Resources/Localization/<language>/; a key here wins over the same key there")]
        private List<GameContentTextTables> _texts = new List<GameContentTextTables>();

        public static LocalizationContent Current
        {
            get
            {
                if (_override != null)
                {
                    return _override;
                }

                if (!_searched)
                {
                    _found = Resources.LoadAll<LocalizationContent>(GameContent.ResourceFolder)
                        .Where(c => c != null)
                        .OrderBy(c => c.name, StringComparer.Ordinal)
                        .FirstOrDefault();
                    _searched = true;
                }

                return _found != null ? _found : null;
            }
        }

        public IReadOnlyList<GameContentTextTables> Texts => _texts ??= new List<GameContentTextTables>();

        public static void Use(LocalizationContent content)
        {
            _override = content;
            Localizer.Reload();
        }

        public IReadOnlyList<TextAsset> TextsFor(string languageCode)
        {
            var tables = new List<TextAsset>();
            foreach (var entry in Texts)
            {
                if (entry == null || !string.Equals(entry.Language.Trim(), languageCode, StringComparison.Ordinal))
                {
                    continue;
                }

                tables.AddRange(entry.Tables.Where(t => t != null));
            }

            return tables;
        }

        public void SetTexts(IEnumerable<GameContentTextTables> texts)
        {
            _texts = texts == null ? new List<GameContentTextTables>() : new List<GameContentTextTables>(texts);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            _override = null;
            _found = null;
            _searched = false;
        }
    }
}
