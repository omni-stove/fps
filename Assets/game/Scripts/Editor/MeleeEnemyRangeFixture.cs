using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // 殴ってくる敵。射撃場の床は GameBootstrap が実行時に作り NavMesh が無いので、自前の足場を置き、Play 中に焼いてから敵を有効にする (NavMeshBakeActivator)。
    // 出現位置・ほかの smoke の手順の場所・殴る的から視界 (EnemySpec.DefaultSightRange) の外になる隅に置き、ほかの手順の間に撃ってこないようにする。
    public sealed class MeleeEnemyRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "MeleeEnemyYard";
        public const string EnemyName = "MeleeEnemy";
        public const string EnemyId = "MeleeEnemy";
        public const string NavFloorName = "NavFloor";
        public const int FixtureOrder = 510;

        public static readonly Vector3 EnemyGround = new Vector3(-21f, 0f, -21f);
        public static readonly Vector3 Size = new Vector3(5f, 2.5f, 5f);

        public const float NavFloorSize = 4f;
        public const float NavFloorThickness = 0.1f;

        // 床の Plane (y=0) と面を重ねると、敵の足がどちらの面に乗るかが定まらないので、わずかに上げる。
        public const float NavFloorTop = 0.02f;

        public static readonly EnemyBody EnemyBodySize = new EnemyBody(2f, 0.5f, 3.5f, Damageable.DefaultMaxHealth);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(EnemyGround + new Vector3(0f, Size.y / 2f, 0f), Size);

        // 殴られる側を試すとき、プレイヤーを置く位置 (足元)。敵の正面 (+Z、射撃場の中央の向き) で届く距離の内。
        public static Vector3 PlayerGround => EnemyGround + new Vector3(0f, 0f, 1.6f);

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.position = EnemyGround;

            var navFloor = new GameObject(NavFloorName);
            navFloor.transform.SetParent(root.transform, false);
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "Slab";
            slab.transform.SetParent(navFloor.transform, false);
            slab.transform.localPosition = new Vector3(0f, NavFloorTop - (NavFloorThickness / 2f), 0f);
            slab.transform.localScale = new Vector3(NavFloorSize, NavFloorThickness, NavFloorSize);

            var activated = new List<GameObject>();
            var weapon = FindEnemyWeapon(scene);
            if (weapon != null)
            {
                var enemy = SceneBuildHelpers.CreateEnemy(
                    scene,
                    root.transform,
                    EnemyName,
                    EnemyId,
                    EnemyGround + new Vector3(0f, NavFloorTop, 0f),
                    Quaternion.identity,
                    new List<Vector3>(),
                    EnemyBodySize,
                    weapon);

                // 殴るかは Play 中にシーンのルールで決まる (EnemyMelee が GameBootstrap のルールを読む)。
                enemy.gameObject.AddComponent<EnemyMelee>();
                enemy.gameObject.SetActive(false);
                activated.Add(enemy.gameObject);
            }
            else
            {
                Debug.LogWarning($"[{nameof(MeleeEnemyRangeFixture)}] the range has no weapon for the melee enemy; placing the floor only");
            }

            root.AddComponent<NavMeshBakeActivator>().Configure(navFloor.transform, activated, SceneBuildHelpers.HumanoidAgentTypeId);
            return root;
        }

        internal static WeaponDefinition FindEnemyWeapon(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var bootstrap = root.GetComponent<GameBootstrap>();
                if (bootstrap == null)
                {
                    continue;
                }

                foreach (var definition in bootstrap.WeaponDefinitions)
                {
                    if (definition != null)
                    {
                        return definition;
                    }
                }
            }

            return null;
        }
    }
}
