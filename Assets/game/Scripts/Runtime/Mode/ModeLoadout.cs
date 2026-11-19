using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    // モードが参加者に武器を持たせる手順。プレイヤー (WeaponController) と bot (EnemyAgent) で持たせ方が違うので分けて置く。
    internal static class ModeLoadout
    {
        // 持たせ直したら true、もう持っていたら false、WeaponController が無ければ null。
        public static bool? EquipController(SideMember member, WeaponDefinition definition)
        {
            return EquipController(member, new[] { definition });
        }

        // 枠の順に並べた武器を持たせる。持たせ直したら true、もう同じ並びを持っていたら false、WeaponController が無ければ null。
        public static bool? EquipController(SideMember member, IReadOnlyList<WeaponDefinition> definitions)
        {
            var controller = member.GetComponentInChildren<WeaponController>(true);
            if (controller == null)
            {
                return null;
            }

            if (!controller.IsInitialized)
            {
                // Awake で作る前なら、作るときにこの並びを持たせる。
                if (SameWeapons(controller.Definitions, definitions))
                {
                    return false;
                }

                controller.Definitions = definitions;
                return true;
            }

            if (SameWeapons(controller.HeldDefinitions, definitions))
            {
                return false;
            }

            // 1 本だけ持たせるモードでも持ち替えの入力を外さないのは、後で複数の枠を持たせ直したときに持ち替えられなくならないため。
            controller.Initialize(
                controller.Aim, controller.AttackAction, controller.ReloadAction, controller.NextAction, controller.PreviousAction, definitions);
            return true;
        }

        // 持たせ直したら true、もう持っていたら false、EnemyAgent が無ければ null。
        public static bool? EquipAgent(SideMember member, WeaponDefinition definition, GameRules rules)
        {
            var agent = member.GetComponent<EnemyAgent>();
            if (agent == null)
            {
                return null;
            }

            if (agent.WeaponDefinition == definition && agent.WeaponRules == rules)
            {
                return false;
            }

            agent.EquipWeapon(definition, rules);
            return true;
        }

        // 今持っている武器の並び。持たせる前 (Awake の前) なら持たせる予定の並び。WeaponController が無ければ空。
        public static IReadOnlyList<WeaponDefinition> HeldBy(SideMember member)
        {
            var controller = member != null ? member.GetComponentInChildren<WeaponController>(true) : null;
            if (controller == null)
            {
                return System.Array.Empty<WeaponDefinition>();
            }

            return CopyOf(controller.IsInitialized ? controller.HeldDefinitions : controller.Definitions);
        }

        // 枠の順に、買った武器 (bought の枠の番号) で元の並び (fallback) を上書きする。どちらにも無い枠は詰める。
        public static List<WeaponDefinition> Slotted(IReadOnlyList<WeaponDefinition> fallback, IReadOnlyList<KeyValuePair<int, WeaponDefinition>> bought)
        {
            var bySlot = new SortedDictionary<int, WeaponDefinition>();
            if (fallback != null)
            {
                for (var slot = 0; slot < fallback.Count; slot++)
                {
                    if (fallback[slot] != null)
                    {
                        bySlot[slot] = fallback[slot];
                    }
                }
            }

            if (bought != null)
            {
                foreach (var pair in bought)
                {
                    if (pair.Key >= 0 && pair.Value != null)
                    {
                        bySlot[pair.Key] = pair.Value;
                    }
                }
            }

            return new List<WeaponDefinition>(bySlot.Values);
        }

        public static GameRules RulesOf(SideMember player)
        {
            var controller = player != null ? player.GetComponentInChildren<WeaponController>(true) : null;
            return controller != null ? controller.Rules : GameRules.Default;
        }

        private static WeaponDefinition[] CopyOf(IReadOnlyList<WeaponDefinition> definitions)
        {
            var copy = new WeaponDefinition[definitions.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = definitions[i];
            }

            return copy;
        }

        private static bool SameWeapons(IReadOnlyList<WeaponDefinition> held, IReadOnlyList<WeaponDefinition> wanted)
        {
            if (held.Count != wanted.Count)
            {
                return false;
            }

            for (var i = 0; i < held.Count; i++)
            {
                if (held[i] != wanted[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
