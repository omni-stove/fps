using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class GameRulesMutator
    {
        public GameRulesMutator(string id, string rulesJson, string sourceName = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("a mutator needs an id", nameof(id));
            }

            Id = id;
            RulesJson = rulesJson ?? throw new ArgumentNullException(nameof(rulesJson));
            SourceName = string.IsNullOrWhiteSpace(sourceName) ? $"mutator '{id}'" : $"{sourceName} mutator '{id}'";
        }

        public string Id { get; }

        public string RulesJson { get; }

        public string SourceName { get; }

        public GameRules Apply(GameRules baseRules)
        {
            if (baseRules == null)
            {
                throw new ArgumentNullException(nameof(baseRules));
            }

            return GameRulesJson.Parse(RulesJson, SourceName, baseRules);
        }

        public static GameRules ApplyAll(GameRules baseRules, IEnumerable<GameRulesMutator> mutators)
        {
            if (baseRules == null)
            {
                throw new ArgumentNullException(nameof(baseRules));
            }

            if (mutators == null)
            {
                throw new ArgumentNullException(nameof(mutators));
            }

            var rules = baseRules;
            foreach (var mutator in mutators)
            {
                if (mutator == null)
                {
                    throw new ArgumentException("mutators must not contain null", nameof(mutators));
                }

                rules = mutator.Apply(rules);
            }

            return rules;
        }

        public static GameRulesMutator Find(IEnumerable<GameRulesMutator> mutators, string id)
        {
            if (mutators == null)
            {
                throw new ArgumentNullException(nameof(mutators));
            }

            foreach (var mutator in mutators)
            {
                if (mutator != null && string.Equals(mutator.Id, id, StringComparison.Ordinal))
                {
                    return mutator;
                }
            }

            return null;
        }
    }
}
