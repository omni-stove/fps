using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class GameRulesOverride
    {
        private static readonly List<GameRulesMutator> _mutators = new List<GameRulesMutator>();

        public static GameRules Current { get; private set; }

        public static IReadOnlyList<GameRulesMutator> Mutators => _mutators.AsReadOnly();

        public static void Set(GameRules rules)
        {
            Current = rules ?? throw new ArgumentNullException(nameof(rules));
        }

        // デバッグパネルの「元に戻す」は自分で入れた上書きだけを消すので、積んだミューテーターは ClearMutators で別に消す。
        public static void Clear()
        {
            Current = null;
        }

        public static void PushMutator(GameRulesMutator mutator)
        {
            _mutators.Add(mutator ?? throw new ArgumentNullException(nameof(mutator)));
        }

        public static void PushMutator(MutatorDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            PushMutator(definition.ToMutator());
        }

        public static GameRulesMutator PushMutator(string id)
        {
            return PushMutator(id, KitTuning.Mutators);
        }

        public static GameRulesMutator PushMutator(string id, IReadOnlyList<GameRulesMutator> available)
        {
            if (available == null)
            {
                throw new ArgumentNullException(nameof(available));
            }

            var mutator = GameRulesMutator.Find(available, id);
            if (mutator == null)
            {
                var known = available.Count == 0 ? "(none)" : string.Join(", ", available.Select(m => m.Id));
                throw new ArgumentException($"no mutator has the id '{id}'; known ids are {known}", nameof(id));
            }

            PushMutator(mutator);
            return mutator;
        }

        public static void ClearMutators()
        {
            _mutators.Clear();
        }

        public static GameRules ApplyMutators(GameRules baseRules)
        {
            return ApplyMutators(baseRules, _mutators);
        }

        // 1 つが今のルールに合わなくても試合は止めず、ほかの調整値 (KitTuning) と同じくエラーを出してその 1 つだけを飛ばす。
        public static GameRules ApplyMutators(GameRules baseRules, IReadOnlyList<GameRulesMutator> mutators)
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
            foreach (var mutator in mutators.ToList())
            {
                if (mutator == null)
                {
                    throw new ArgumentException("mutators must not contain null", nameof(mutators));
                }

                GameRules next;
                try
                {
                    next = mutator.Apply(rules);
                }
                catch (GameRulesJsonException e)
                {
                    Debug.LogError(MutatorLog.Skipped(mutator, e));
                    continue;
                }

                Debug.Log(MutatorLog.Applied(mutator, rules, next));
                rules = next;
            }

            return rules;
        }

        // Enter Play Mode でドメインを読み直さない設定なので、前の Play の上書きが残らないよう Play のたびに消す。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Current = null;
            _mutators.Clear();
        }
    }
}
