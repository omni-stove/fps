using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fps.Runtime
{
    [Serializable]
    public sealed class GameContentTextTables
    {
        [SerializeField, Tooltip("言語コード (Resources/Localization/Languages の一覧の code。例: ja, en)")]
        private string _language = string.Empty;

        [SerializeField, Tooltip("この言語の表 (Resources/Localization/<言語>/*.json と同じ形の JSON)。Resources の表の後に、この並びで重ねる")]
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
