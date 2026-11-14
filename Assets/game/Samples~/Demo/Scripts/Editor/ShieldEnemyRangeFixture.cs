using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // 盾持ちを試す仮の敵。射撃場の床は実行時に作られ NavMesh が無いので、殴ってくる敵 (MeleeEnemyRangeFixture) と同じく自前の足場を Play 中に焼く。
    // 開けた場所で回避を試す smoke の手順に反応しないよう、右手前の隅に置いて視界を狭める (開けた場所の中心から SightRange の外)。
    public sealed class ShieldEnemyRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "ShieldEnemyYard";
        public const string EnemyName = "ShieldEnemy";
        public const string EnemyId = "ShieldEnemy";
        public const string NavFloorName = "NavFloor";
        public const int FixtureOrder = 520;

        public const float NavFloorSize = 7f;
        public const float NavFloorThickness = 0.1f;

        // 床の Plane (y=0) と面を重ねると、敵の足がどちらの面に乗るかが定まらないので、わずかに上げる。
        public const float NavFloorTop = 0.02f;

        public const float SightRange = 10f;
        public const float AttackRange = 2f;
        public const float LoseSightSeconds = 2f;
        public const float SearchSeconds = 6f;
        public const float ReactionSeconds = 0.5f;
        public const int MaxHealth = 150;

        public static readonly Vector3 EnemyGround = new Vector3(21f, 0f, -21f);

        public static readonly EnemyBody EnemyBodySize = new EnemyBody(2f, 0.5f, 3.5f, MaxHealth);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(
            EnemyGround + new Vector3(0f, EnemyBodySize.Height / 2f, 0f),
            new Vector3(NavFloorSize, EnemyBodySize.Height, NavFloorSize));

        public static EnemySpec Spec() => new EnemySpec(
            SightRange,
            AttackRange,
            LoseSightSeconds,
            SearchSeconds,
            ReactionSeconds,
            archetype: EnemyArchetype.Shield,
            shield: ShieldSpec.Default);

        // 出現位置 (原点) の側を向かせ、射撃場の真ん中から歩いて来たプレイヤーが盾の正面に立つようにする。
        public static Quaternion Facing => SceneBuildHelpers.FacingTowards(EnemyGround, Vector3.zero);

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            try
            {
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
                var weapon = MeleeEnemyRangeFixture.FindEnemyWeapon(scene);
                if (weapon != null)
                {
                    var enemy = SceneBuildHelpers.CreateEnemy(
                        scene,
                        root.transform,
                        EnemyName,
                        EnemyId,
                        EnemyGround + new Vector3(0f, NavFloorTop, 0f),
                        Facing,
                        new List<Vector3>(),
                        EnemyBodySize,
                        weapon);
                    enemy.ApplySpec(Spec());

                    var shield = enemy.gameObject.AddComponent<EnemyShield>();
                    shield.Plate = BulletShield.CreatePlaceholder(enemy.transform);
                    enemy.gameObject.SetActive(false);
                    activated.Add(enemy.gameObject);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(ShieldEnemyRangeFixture)}] the range has no weapon for the shield enemy; placing the floor only");
                }

                root.AddComponent<NavMeshBakeActivator>().Configure(navFloor.transform, activated, SceneBuildHelpers.HumanoidAgentTypeId);
                return root;
            }
            catch
            {
                Object.DestroyImmediate(root);
                throw;
            }
        }
    }
}
