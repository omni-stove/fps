using Fps.Core;
using UnityEngine;
using UnityEngine.AI;

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
            // 差し替えた見た目が自分の形の NavMeshObstacle を持っていれば、そちらを使う。
            if (placed.GetComponentInChildren<NavMeshObstacle>() != null)
            {
                return;
            }

            var obstacle = placed.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, spec.Height / 2f, 0f);
            obstacle.size = new Vector3(spec.Width, spec.Height, spec.Depth);
            obstacle.carving = true;
        }
    }
}
