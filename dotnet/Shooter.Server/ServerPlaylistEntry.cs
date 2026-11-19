using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    // プレイリストの 1 項目を、専用サーバーで遊ぶ形 (マップの名前と、ミューテーターを重ねた後のルール) にしたもの
    internal sealed class ServerPlaylistEntry
    {
        public ServerPlaylistEntry(int position, PlaylistEntry entry, string rulesName, ServerRulesData data)
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must not be negative");
            }

            if (string.IsNullOrWhiteSpace(rulesName))
            {
                throw new ArgumentException("a playlist entry needs the name of its rules", nameof(rulesName));
            }

            Position = position;
            Entry = entry ?? throw new ArgumentNullException(nameof(entry));
            RulesName = rulesName;
            Data = data ?? throw new ArgumentNullException(nameof(data));
        }

        public int Position { get; }

        public PlaylistEntry Entry { get; }

        // Unity ではシーンを読むが、専用サーバーではシーンと同じ名前のマップ (server-data/maps) を読む
        public string Map => Entry.Scene;

        public string RulesName { get; }

        public ServerRulesData Data { get; }

        public GameRules Rules => Data.Rules;

        public IReadOnlyList<string> MutatorIds => Entry.MutatorIds;

        // 表示の文字でなくルールの名前をキーとして送るのは、言語ごとの文字をクライアントが自分の表で引くため (MapVoteStateMessage.NextPlaylistEntryName)
        public string Name => RulesName;
    }
}
