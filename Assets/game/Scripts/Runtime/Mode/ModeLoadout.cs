using Shooter.Core;

namespace Shooter.Runtime
{
    // モードが参加者に 1 本の武器だけを持たせる手順。プレイヤー (WeaponController) と bot (EnemyAgent) で持たせ方が違うので分けて置く。
    internal static class ModeLoadout
    {
        // 持たせ直したら true、もう持っていたら false、WeaponController が無ければ null。
        public static bool? EquipController(SideMember member, WeaponDefinition definition)
        {
            var controller = member.GetComponentInChildren<WeaponController>(true);
            if (controller == null)
            {
                return null;
            }

            if (!controller.IsInitialized)
            {
                // Awake で作る前なら、作るときにこの武器だけを持たせる。
                if (controller.Definitions.Count == 1 && controller.Definitions[0] == definition)
                {
                    return false;
                }

                controller.Definitions = new[] { definition };
                return true;
            }

            if (controller.Definitions.Count == 1 && controller.Definition == definition)
            {
                return false;
            }

            controller.Initialize(controller.Aim, controller.AttackAction, controller.ReloadAction, null, null, new[] { definition });
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

        public static GameRules RulesOf(SideMember player)
        {
            var controller = player != null ? player.GetComponentInChildren<WeaponController>(true) : null;
            return controller != null ? controller.Rules : GameRules.Default;
        }
    }
}
