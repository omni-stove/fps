using System;
using System.Collections.Generic;
using System.Text;

namespace Shooter.Core
{
    public static class MutatorLog
    {
        public const string LogTag = "[Mutator]";

        private static readonly HashSet<string> _draftItems = DraftItems();

        // デバッグパネルの適用ログ (GameRulesDebugDraft.Changes) と同じ書き方で並べ、パネルで切り替えない欄
        // (playerMaxHealth・zoneDamage など) は、ルールの JSON の項目名で書き足す。Changes だけでは体力や部位の倍率を変えても出ない。
        public static IReadOnlyList<string> Changes(GameRules before, GameRules after)
        {
            if (before == null)
            {
                throw new ArgumentNullException(nameof(before));
            }

            if (after == null)
            {
                throw new ArgumentNullException(nameof(after));
            }

            var changes = new List<string>(GameRulesDebugDraft.Changes(before, after));
            var from = new GameRulesJson.Parser(GameRulesJson.Write(before)).ParseDocument();
            var to = new GameRulesJson.Parser(GameRulesJson.Write(after)).ParseDocument();
            AddChanges(string.Empty, from, to, changes);
            return changes;
        }

        public static string Applied(GameRulesMutator mutator, GameRules before, GameRules after)
        {
            if (mutator == null)
            {
                throw new ArgumentNullException(nameof(mutator));
            }

            var changes = Changes(before, after);
            var body = changes.Count == 0 ? "no changes" : string.Join(", ", changes);
            return $"{LogTag} applied id={mutator.Id}: {body}";
        }

        public static string Skipped(GameRulesMutator mutator, Exception reason)
        {
            if (mutator == null)
            {
                throw new ArgumentNullException(nameof(mutator));
            }

            if (reason == null)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            return $"{LogTag} skipped id={mutator.Id}: {reason.Message}";
        }

        public static string UnknownId(string id, IReadOnlyList<GameRulesMutator> available)
        {
            if (available == null)
            {
                throw new ArgumentNullException(nameof(available));
            }

            var known = new List<string>(available.Count);
            foreach (var mutator in available)
            {
                known.Add(mutator.Id);
            }

            var list = known.Count == 0 ? "(none)" : string.Join(", ", known);
            return $"{LogTag} skipped id={id}: no mutator has this id; known ids are {list}";
        }

        private static void AddChanges(string prefix, GameRulesJson.Node from, GameRulesJson.Node to, List<string> changes)
        {
            foreach (var member in from.Members)
            {
                var other = Member(to, member.Key);
                var path = prefix + member.Key;
                if (member.Value.Kind == GameRulesJson.NodeKind.Object && other != null && other.Kind == GameRulesJson.NodeKind.Object)
                {
                    AddChanges(path + ".", member.Value, other, changes);
                    continue;
                }

                if (_draftItems.Contains(member.Key))
                {
                    continue;
                }

                var fromText = Text(member.Value);
                var toText = other == null ? "(none)" : Text(other);
                if (!string.Equals(fromText, toText, StringComparison.Ordinal))
                {
                    changes.Add($"{path}: {fromText} -> {toText}");
                }
            }
        }

        private static GameRulesJson.Node Member(GameRulesJson.Node node, string key)
        {
            foreach (var member in node.Members)
            {
                if (string.Equals(member.Key, key, StringComparison.Ordinal))
                {
                    return member.Value;
                }
            }

            return null;
        }

        private static string Text(GameRulesJson.Node node)
        {
            switch (node.Kind)
            {
                case GameRulesJson.NodeKind.Array:
                    var builder = new StringBuilder("[");
                    for (var i = 0; i < node.Elements.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(Text(node.Elements[i]));
                    }

                    return builder.Append(']').ToString();
                case GameRulesJson.NodeKind.True:
                    return "true";
                case GameRulesJson.NodeKind.False:
                    return "false";
                case GameRulesJson.NodeKind.Null:
                    return "null";
                default:
                    return node.Text;
            }
        }

        // デバッグの欄の名前は JSON の項目名の頭を大文字にしたものなので、頭を小文字に戻して Changes が出す欄を JSON の側から外す。
        private static HashSet<string> DraftItems()
        {
            var items = new HashSet<string>(StringComparer.Ordinal) { GameRulesJson.GrenadeCountItem };
            foreach (var field in GameRulesDebugDraft.Fields)
            {
                var name = field.ToString();
                items.Add(char.ToLowerInvariant(name[0]) + name.Substring(1));
            }

            return items;
        }
    }
}
