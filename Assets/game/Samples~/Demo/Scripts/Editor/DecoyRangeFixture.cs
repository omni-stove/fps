using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 右手前の隅 (x = 28..36, z = -37..-26) の、ほかの敵の居場所から囮の呼び声 (DecoySpec.DefaultCallRadius) が届かない所に置く。
    // 敵は無効のまま置き、smoke の手順 (DecoySmokeStep) が自分の番に有効にする。ほかの手順の間に撃ち合いを始めないように。
    // 敵が囮のあった場所まで歩いて調べに行けるよう、敵の足元から囮の置き場所までを NavMesh の床で覆う。
    public sealed class DecoyRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "DecoyYard";
        public const string NavFloorName = "NavFloor";
        public const string EnemyName = "DecoyEnemy";
        public const string EnemyId = "decoy-enemy";
        public const int FixtureOrder = 560;

        public const float NavFloorThickness = 0.1f;

        // 床の Plane (y=0) と面を重ねると、敵の足がどちらの面に乗るかが定まらないので、わずかに上げる。
        public const float NavFloorTop = 0.02f;

        public static readonly Vector3 Center = new Vector3(32f, 0f, -31.5f);
        public static readonly Vector3 Size = new Vector3(8f, 3f, 11f);

        public static readonly Vector3 PlayerGround = new Vector3(32f, 0f, -36.5f);

        public static readonly Vector3 NavFloorCenter = new Vector3(32f, 0f, -30f);
        public static readonly Vector2 NavFloorSize = new Vector2(5f, 8f);

        public static readonly Vector3 EnemyGround = new Vector3(32f, 0f, -27f);

        public static readonly EnemyBody EnemyBodySize = new EnemyBody(2f, 0.5f, 3.5f, Damageable.DefaultMaxHealth);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(Center + new Vector3(0f, Size.y / 2f, 0f), Size);

        public static Vector3 DecoyGround => PlayerGround + new Vector3(0f, 0f, DeploySpec.DefaultMaxRange);

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
                    var enemy = SceneBuildHelpers.CreateEnemy(
                        scene,
                        root.transform,
                        EnemyName,
                        EnemyId,
                        EnemyGround + new Vector3(0f, NavFloorTop, 0f),
                        Quaternion.LookRotation(Vector3.back, Vector3.up),
                        new List<Vector3>(),
                        EnemyBodySize,
                        weapon);
                    enemy.gameObject.SetActive(false);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(DecoyRangeFixture)}] the range has no weapon for the decoy's enemy; placing the floor only");
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
    }
}
