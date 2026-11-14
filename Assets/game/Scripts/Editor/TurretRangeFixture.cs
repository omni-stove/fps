using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // タレットを試す場所。プレイヤーは手前 (PlayerGround) に立って +Z へタレットを置き、奥の足場に置いた敵を撃たせる。
    // 敵は無効のまま置き、smoke の手順 (TurretSmokeStep) が自分の番に有効にする。ほかの手順の間に撃ち合いを始めないように。
    // 足場の NavMesh は Play の頭に焼くが、焼いた後に有効にする物は渡さない (NavMeshBakeActivator は渡した物しか有効にしない)。
    // 置いたタレットの視界 (TurretSpec.DefaultSightRange) がほかの敵 (突進の囲いなど) に届かない所に置く。
    public sealed class TurretRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "TurretYard";
        public const string NavFloorName = "NavFloor";
        public const string PreyName = "TurretPrey";
        public const string PreyId = "turret-prey";
        public const string RaiderName = "TurretRaider";
        public const string RaiderId = "turret-raider";
        public const int FixtureOrder = 550;

        public const float NavFloorThickness = 0.1f;

        // 床の Plane (y=0) と面を重ねると、敵の足がどちらの面に乗るかが定まらないので、わずかに上げる。
        public const float NavFloorTop = 0.02f;

        // タレットに撃たれ続けてもタレットを壊し切れるよう、タレットを撃ち返す敵は倒れないほどの体力にする。
        public const int RaiderMaxHealth = 100000;

        public static readonly Vector3 Center = new Vector3(5.25f, 0f, -9f);
        public static readonly Vector3 Size = new Vector3(4.5f, 2.5f, 8f);

        public static readonly Vector3 PlayerGround = new Vector3(5f, 0f, -12.5f);

        public static readonly Vector3 NavFloorCenter = new Vector3(5.25f, 0f, -6.1f);
        public static readonly Vector2 NavFloorSize = new Vector2(4.2f, 2f);

        public static readonly Vector3 PreyGround = new Vector3(4f, 0f, -6f);
        public static readonly Vector3 RaiderGround = new Vector3(6.5f, 0f, -6f);

        public static readonly EnemyBody PreyBody = new EnemyBody(2f, 0.5f, 3.5f, Damageable.DefaultMaxHealth);
        public static readonly EnemyBody RaiderBody = new EnemyBody(2f, 0.5f, 3.5f, RaiderMaxHealth);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(Center + new Vector3(0f, Size.y / 2f, 0f), Size);

        public static Vector3 TurretGround => PlayerGround + new Vector3(0f, 0f, DeploySpec.DefaultMaxRange);

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            try
            {
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = Center;

                var navFloor = new GameObject(NavFloorName);
                navFloor.transform.SetParent(root.transform, false);
                navFloor.transform.position = NavFloorCenter;
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = "Slab";
                slab.transform.SetParent(navFloor.transform, false);
                slab.transform.localPosition = new Vector3(0f, NavFloorTop - (NavFloorThickness / 2f), 0f);
                slab.transform.localScale = new Vector3(NavFloorSize.x, NavFloorThickness, NavFloorSize.y);

                var weapon = MeleeEnemyRangeFixture.FindEnemyWeapon(scene);
                if (weapon != null)
                {
                    var facing = Quaternion.LookRotation(Vector3.back, Vector3.up);
                    CreateEnemy(scene, root.transform, PreyName, PreyId, PreyGround, facing, PreyBody, weapon);
                    CreateEnemy(scene, root.transform, RaiderName, RaiderId, RaiderGround, facing, RaiderBody, weapon);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(TurretRangeFixture)}] the range has no weapon for the turret's enemies; placing the floor only");
                }

                root.AddComponent<NavMeshBakeActivator>().Configure(navFloor.transform, new List<GameObject>(), SceneBuildHelpers.HumanoidAgentTypeId);
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }

        private static void CreateEnemy(
            Scene scene, Transform parent, string name, string id, Vector3 ground, Quaternion facing, EnemyBody body, WeaponDefinition weapon)
        {
            var enemy = SceneBuildHelpers.CreateEnemy(
                scene, parent, name, id, ground + new Vector3(0f, NavFloorTop, 0f), facing, new List<Vector3>(), body, weapon);
            enemy.gameObject.SetActive(false);
        }
    }
}
