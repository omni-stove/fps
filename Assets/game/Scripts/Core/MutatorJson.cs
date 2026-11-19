using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class MutatorJson
    {
        public const string ResourcePath = "Tuning/mutators";

        public const string MutatorsItem = "mutators";

        public const string IdItem = "id";
        public const string RulesItem = "rules";

        private const string _kind = "mutator JSON";

        // 読み込むときはどのルールの上に重ねるかまだ分からないので、欄どうしの縛り (ブラインドファイアはカバーが On のときだけ) で
        // 断らない土台で値を確かめる。カバーを切ってブラインドファイアを入れるような、どの土台でも通らない組だけがここで止まる。
        private static readonly GameRules _checkBase = GameRules.Default.WithActions(GameRules.Default.Actions.WithCover(Cover.On));

        public static IReadOnlyList<GameRulesMutator> Empty { get; } = Array.AsReadOnly(Array.Empty<GameRulesMutator>());

        public static IReadOnlyList<string> ItemNames { get; } = new[] { MutatorsItem };

        public static IReadOnlyList<string> MutatorItemNames { get; } = new[] { IdItem, RulesItem };

        public static IReadOnlyList<GameRulesMutator> Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var mutators = SpecJsonReader.ReadList(fields, MutatorsItem, MutatorItemNames, ReadMutator);
                if (mutators == null)
                {
                    return Empty;
                }

                var ids = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < mutators.Count; i++)
                {
                    if (!ids.Add(mutators[i].Id))
                    {
                        var item = fields.ElementPath(MutatorsItem, i) + "." + IdItem;
                        throw fields.Rejected(item, $"id '{mutators[i].Id}' is used by more than one mutator");
                    }
                }

                return (IReadOnlyList<GameRulesMutator>)mutators.AsReadOnly();
            });
        }

        private static GameRulesMutator ReadMutator(ServerDataJson.Fields entry)
        {
            var id = SpecJsonReader.RequireText(entry, IdItem);
            var rulesNode = entry.Require(RulesItem);

            // Apply でも断るが、書き戻した文書の位置ではなく一覧の JSON の中の位置で示すため、一番上の項目名はここで確かめる。
            entry.Object(RulesItem, GameRulesJson.ItemNames);

            var mutator = new GameRulesMutator(id, GameRulesJson.WriteNode(rulesNode), entry.Source);
            try
            {
                mutator.Apply(_checkBase);
            }
            catch (GameRulesJsonException e)
            {
                var item = string.IsNullOrEmpty(e.Item) ? entry.ItemPath(RulesItem) : entry.ItemPath(RulesItem) + "." + e.Item;
                throw entry.Invalid(item, rulesNode, e.Message, e);
            }

            return mutator;
        }
    }
}
