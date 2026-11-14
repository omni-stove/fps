using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // 自爆兵を試す仮の敵。射撃場の床は実行時に作られ NavMesh が無いので、殴ってくる敵 (MeleeEnemyRangeFixture) と同じく自前の足場を Play 中に焼く。
    // 手前の左 (出現位置の左下) に置き、視界と爆発がほかの物 (殴る的・パラシュートの塔・滑り込みの坂) に届かないようにする。
    public sealed class BomberRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "BomberYard";
        public const string EnemyName = "Bomber";
        public const string EnemyId = "Bomber";
        public const string NavFloorName = "NavFloor";
        public const int FixtureOrder = 550;

        public const float NavFloorSize = 8f;
        public const float NavFloorThickness = 0.1f;

        // 床の Plane (y=0) と面を重ねると、敵の足がどちらの面に乗るかが定まらないので、わずかに上げる。
        public const float NavFloorTop = 0.02f;

        public const float SightRange = 8f;
        public const float AttackRange = 6f;
        public const float LoseSightSeconds = 2f;
        public const float SearchSeconds = 4f;
        public const float ReactionSeconds = 0.5f;

        // 合図の前に撃ち倒すのを試せるよう、数発で倒れる体力にする。
        public const int MaxHealth = 60;

        public static readonly Vector3 YardCenter = new Vector3(-8.5f, 0f, -19.5f);

        public static readonly Vector3 EnemyGround = YardCenter + new Vector3(0f, 0f, -2f);

        public static readonly EnemyBody EnemyBodySize = new EnemyBody(2f, 0.5f, 3.5f, MaxHealth);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(
            YardCenter + new Vector3(0f, EnemyBodySize.Height / 2f, 0f),
            new Vector3(NavFloorSize, EnemyBodySize.Height, NavFloorSize));

        public static EnemySpec Spec() => new EnemySpec(
            SightRange,
            AttackRange,
            LoseSightSeconds,
            SearchSeconds,
            ReactionSeconds,
            archetype: EnemyArchetype.Bomber,
            bomber: BomberSpec.Default);

        public static Quaternion Facing => Quaternion.LookRotation(Vector3.forward, Vector3.up);

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            try
            {
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = YardCenter;

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

                    // 目印はシーンに焼き込まず、実行時に作らせる。焼き込むと、後で EnemyBomber にプレハブを設定しても使われない。
                    enemy.gameObject.AddComponent<EnemyBomber>();
                    enemy.gameObject.SetActive(false);
                    activated.Add(enemy.gameObject);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(BomberRangeFixture)}] the range has no weapon for the bomber; placing the floor only");
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
