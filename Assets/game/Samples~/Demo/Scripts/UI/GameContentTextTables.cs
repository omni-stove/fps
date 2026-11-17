using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [Serializable]
    public sealed class GameContentTextTables
    {
        [SerializeField, Tooltip("The language code (a code in the Resources/Localization/Languages list, e.g. ja, en)")]
        private string _language = string.Empty;

        [SerializeField, Tooltip("The tables for this language (JSON shaped like Resources/Localization/<language>/*.json), layered in this order after the Resources tables")]
        private List<TextAsset> _tables = new List<TextAsset>();

        public GameContentTextTables()
        {
        }

        public GameContentTextTables(string language, IEnumerable<TextAsset> tables)
        {
            _language = language ?? string.Empty;
            _tables = tables == null ? new List<TextAsset>() : new List<TextAsset>(tables);
        }

        public string Language => _language ?? string.Empty;

        public IReadOnlyList<TextAsset> Tables => _tables ??= new List<TextAsset>();
    }
}
