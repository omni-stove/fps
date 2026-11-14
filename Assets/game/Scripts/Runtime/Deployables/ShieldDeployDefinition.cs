using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    [CreateAssetMenu(fileName = "DeployShield", menuName = "Fps/Abilities/Deploy Shield")]
    public sealed class ShieldDeployDefinition : DeployAbilityDefinition
    {
        public const string PlateName = "Plate";

        protected override GameObject CreatePlaceholder(DeploySpec spec)
        {
            var root = CreateBox(Id, spec, true);
            root.transform.Find("Box").name = PlateName;
            return root;
        }

        // 敵が盾に向かって歩き続けないよう、NavMesh に穴を開けて避けさせる。隠れ場所 (CoverPoint) にはしない。
        protected override void Configure(GameObject placed, DeploySpec spec)
        {
            CarveNavMesh(placed, spec);
        }
    }
}
