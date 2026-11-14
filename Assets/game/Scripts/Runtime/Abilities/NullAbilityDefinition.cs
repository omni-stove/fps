using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 何もしない能力。効果が出たらその場で終わる。枠・回数・待ち時間・HUD・ログを確かめるためのもので、
    /// 射撃場 (<c>Fps.Editor.TpsRangeSceneBuilder</c>) が Ability1 に入れる。
    /// </summary>
    [CreateAssetMenu(fileName = "NullAbility", menuName = "Fps/Abilities/Null Ability")]
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
