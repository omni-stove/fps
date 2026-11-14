using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // 突進兵を試す仮の敵と、低い壁で囲った囲い。避けた突進が壁にぶつかって止まるのを見られるよう、四方を乗り越えられる高さの壁で囲う。
    // 壁は足場と一緒に焼き (NavMeshBakeActivator)、歩くときは壁を避け、突進のときだけ壁にぶつかるようにする。
    // 手前の真ん中の下 (出現位置の右下) に置き、視界を出現位置・開けた場所・殴る的に届かない長さにする。
    public sealed class ChargerRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "ChargerPen";
        public const string EnemyName = "Charger";
        public const string EnemyId = "Charger";
        public const string NavFloorName = "NavFloor";
        public const int FixtureOrder = 540;

        public const float PenWidth = 10f;
        public const float PenDepth = 8f;
        public const float NavFloorThickness = 0.1f;

        // 床の Plane (y=0) と面を重ねると、敵の足がどちらの面に乗るかが定まらないので、わずかに上げる。
        public const float NavFloorTop = 0.02f;

        // プレイヤーが跳び越えて入れ (TpsRangeSceneBuilder.VaultWallHeight と同じ)、突進の線 (胴の高さ) は止める高さ。
        public const float WallHeight = TpsRangeSceneBuilder.VaultWallHeight;
        public const float WallThickness = 0.3f;

        public const float SightRange = 8f;
        public const float AttackRange = 6f;
        public const float LoseSightSeconds = 2f;
        public const float SearchSeconds = 4f;
        public const float ReactionSeconds = 0.5f;
        public const int MaxHealth = 150;

        public static readonly Vector3 PenCenter = new Vector3(10f, 0f, -20f);

        // 奥 (-Z) の壁の手前に置き、+Z (入ってくる側) を向かせる。入ってきた相手へ向けて囲いの長い向きに走らせるため。
        public static readonly Vector3 EnemyGround = PenCenter + new Vector3(0f, 0f, -2.5f);

        public static readonly EnemyBody EnemyBodySize = new EnemyBody(2f, 0.5f, 3.5f, MaxHealth);

        public static readonly string[] WallNames = { "Wall_North", "Wall_South", "Wall_East", "Wall_West" };

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(
            PenCenter + new Vector3(0f, EnemyBodySize.Height / 2f, 0f),
            new Vector3(PenWidth, EnemyBodySize.Height, PenDepth));

        // 構えの距離 (TriggerDistance) を視界より短くし、囲いの外から見つけても入ってくるまでは構えないようにする。
        public static EnemySpec Spec() => new EnemySpec(
            SightRange,
            AttackRange,
            LoseSightSeconds,
            SearchSeconds,
            ReactionSeconds,
            archetype: EnemyArchetype.Charger,
            charger: ChargerSpec.Default);

        public static Quaternion Facing => Quaternion.LookRotation(Vector3.forward, Vector3.up);

        public static (Vector3 Center, Vector3 Size)[] Walls()
        {
            var y = NavFloorTop + (WallHeight / 2f);
            var halfX = (PenWidth / 2f) - (WallThickness / 2f);
            var halfZ = (PenDepth / 2f) - (WallThickness / 2f);
            var alongX = new Vector3(PenWidth, WallHeight, WallThickness);
            var alongZ = new Vector3(WallThickness, WallHeight, PenDepth - (WallThickness * 2f));
            return new[]
            {
                (PenCenter + new Vector3(0f, y, halfZ), alongX),
                (PenCenter + new Vector3(0f, y, -halfZ), alongX),
                (PenCenter + new Vector3(halfX, y, 0f), alongZ),
                (PenCenter + new Vector3(-halfX, y, 0f), alongZ),
            };
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            try
            {
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = PenCenter;

                var navFloor = new GameObject(NavFloorName);
                navFloor.transform.SetParent(root.transform, false);
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = "Slab";
                slab.transform.SetParent(navFloor.transform, false);
                slab.transform.localPosition = new Vector3(0f, NavFloorTop - (NavFloorThickness / 2f), 0f);
                slab.transform.localScale = new Vector3(PenWidth, NavFloorThickness, PenDepth);

                var walls = Walls();
                for (var i = 0; i < walls.Length; i++)
                {
                    SceneBuildHelpers.CreateStaticBox(scene, navFloor.transform, WallNames[i], walls[i].Center, walls[i].Size);
                }

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

                    var charger = enemy.gameObject.AddComponent<EnemyCharger>();
                    charger.Tell = EnemyCharger.CreateTellPlaceholder(enemy.transform);
                    enemy.gameObject.SetActive(false);
                    activated.Add(enemy.gameObject);
                }
                else
                {
                    Debug.LogWarning($"[{nameof(ChargerRangeFixture)}] the range has no weapon for the charger; placing the pen only");
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
