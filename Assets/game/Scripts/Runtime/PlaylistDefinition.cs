using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Playlist", menuName = "Shooter/Playlist")]
    public sealed class PlaylistDefinition : ScriptableObject
    {
        [SerializeField, Tooltip("プレイリストの Id。ログに出る")]
        private string _id = string.Empty;

        [SerializeField, Tooltip("項目を並べた順に回すか (Sequential)、一巡ごとに混ぜるか (Shuffle)")]
        private PlaylistOrder _order = PlaylistOrder.Sequential;

        [SerializeField, Min(0f), Tooltip("試合が終わってから次の項目のシーンを読むまでの秒数")]
        private float _nextDelaySeconds = Playlist.DefaultNextDelaySeconds;

        [SerializeField, Tooltip("回す項目 (モードのシーン・ルール名・ミューテーターの Id)。最後の後は最初に戻る")]
        private List<Entry> _entries = new List<Entry>();

        [SerializeField, Tooltip("項目のルール名が指すルール。項目にルール名を書くなら、ここに同じ名前で入れる")]
        private List<NamedRules> _rules = new List<NamedRules>();

        public string Id
        {
            get => _id;
            set => _id = value ?? string.Empty;
        }

        public PlaylistOrder Order
        {
            get => _order;
            set => _order = value;
        }

        public float NextDelaySeconds
        {
            get => _nextDelaySeconds;
            set => _nextDelaySeconds = value;
        }

        public IReadOnlyList<Entry> Entries => _entries ??= new List<Entry>();

        public IReadOnlyList<NamedRules> Rules => _rules ??= new List<NamedRules>();

        public void SetEntries(IEnumerable<Entry> entries)
        {
            _entries = entries == null ? new List<Entry>() : new List<Entry>(entries);
        }

        public void SetRules(IEnumerable<NamedRules> rules)
        {
            _rules = rules == null ? new List<NamedRules>() : new List<NamedRules>(rules);
        }

        public Playlist ToPlaylist()
        {
            var entries = new List<PlaylistEntry>();
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i] ?? throw new InvalidOperationException($"playlist definition '{name}' has an empty entry at {i}");
                try
                {
                    entries.Add(new PlaylistEntry(entry.Scene, entry.Rules, entry.MutatorIds));
                }
                catch (ArgumentException e)
                {
                    throw new InvalidOperationException($"playlist definition '{name}' entry {i} is invalid: {e.Message}", e);
                }
            }

            try
            {
                return new Playlist(_id, _order, entries, _nextDelaySeconds);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"playlist definition '{name}' is invalid: {e.Message}", e);
            }
        }

        public IReadOnlyDictionary<string, GameRules> RulesByName()
        {
            var table = new Dictionary<string, GameRules>(StringComparer.Ordinal);
            for (var i = 0; i < Rules.Count; i++)
            {
                var slot = Rules[i];
                if (slot == null || string.IsNullOrWhiteSpace(slot.Name))
                {
                    throw new InvalidOperationException($"playlist definition '{name}' rules {i} has no name");
                }

                // 参照切れ (破棄されたアセット) も Unity の == null で拾う
                if (slot.Rules == null)
                {
                    throw new InvalidOperationException($"playlist definition '{name}' rules '{slot.Name}' has no GameRulesDefinition");
                }

                if (table.ContainsKey(slot.Name))
                {
                    throw new InvalidOperationException($"playlist definition '{name}' has more than one rules named '{slot.Name}'");
                }

                table.Add(slot.Name, slot.Rules.ToRules());
            }

            return table;
        }

        [Serializable]
        public sealed class Entry
        {
            [SerializeField, Tooltip("モードのシーンの名前かパス (Assets/.../*.unity)。Build Settings に入っていること")]
            private string _scene = string.Empty;

            [SerializeField, Tooltip("この試合のルール名 (下のルールの表の名前)。空ならシーンのルールのまま")]
            private string _rules = string.Empty;

            [SerializeField, Tooltip("ルールの上にこの順で重ねるミューテーターの Id (mutators.json の id)。知らない Id は理由をログに出して飛ばす")]
            private string[] _mutatorIds = Array.Empty<string>();

            public Entry()
            {
            }

            public Entry(string scene, string rules = null, IEnumerable<string> mutatorIds = null)
            {
                _scene = scene ?? string.Empty;
                _rules = rules ?? string.Empty;
                _mutatorIds = mutatorIds == null ? Array.Empty<string>() : new List<string>(mutatorIds).ToArray();
            }

            public string Scene => _scene ?? string.Empty;

            public string Rules => _rules ?? string.Empty;

            public IReadOnlyList<string> MutatorIds => _mutatorIds ?? Array.Empty<string>();
        }

        [Serializable]
        public sealed class NamedRules
        {
            [SerializeField, Tooltip("項目から指すルール名。server-data/rules に書き出す名前と揃えておくと迷わない")]
            private string _name = string.Empty;

            [SerializeField, Tooltip("この名前で使うルール")]
            private GameRulesDefinition _rules;

            public NamedRules()
            {
            }

            public NamedRules(string name, GameRulesDefinition rules)
            {
                _name = name ?? string.Empty;
                _rules = rules;
            }

            public string Name => _name ?? string.Empty;

            public GameRulesDefinition Rules => _rules;
        }
    }
}
