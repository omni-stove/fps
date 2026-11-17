using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "NullAbility", menuName = "Shooter/Abilities/Null Ability")]
    public sealed class NullAbilityDefinition : AbilityDefinition
    {
        public override IAbility CreateAbility()
        {
            return new NullAbility();
        }

        private sealed class NullAbility : IAbility
        {
            public bool CanUse(AbilityContext context)
            {
                return true;
            }

            public void Begin(AbilityContext context)
            {
            }

            public bool Tick(AbilityContext context, float deltaSeconds)
            {
                return !context.IsEffective;
            }

            public void End(AbilityContext context, bool interrupted)
            {
            }
        }
    }
}
