using System;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 爆発の半径にほかの置く物が入らないよう、乗り越えの壁・開けた場所・小屋・GameBootstrap の標的ダミーの列から離して置く。
    // 近くの敵の代わりに標的ダミーを置くのは、射撃場には NavMesh が無く EnemyAgent を置けないため。
    public sealed class ExplosivePropRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "ExplosiveProps";
        public const string FirstBarrelName = "Barrel_A";
        public const string SecondBarrelName = "Barrel_B";
        public const string GasCanName = "GasCan";

        public const string DummyId = "ExplosiveDummy";

        // 樽 A を撃つと隣の標的ダミーが倒れ、樽 B が連鎖して遅れて爆発し、ガソリン缶が燃えてからさらに遅れて爆発する間隔にする。
        public const float Spacing = 1.6f;

        public const float BarrelRadius = 0.3f;
        public const float BarrelHeight = 0.9f;

        public static readonly Vector3 GasCanSize = new Vector3(0.35f, 0.45f, 0.25f);

        public static readonly Vector3 Center = new Vector3(-28f, 0f, 28f);

        public static readonly ExplosivePropSpec BarrelSpec = ExplosivePropSpec.Default;

        public static readonly ExplosivePropSpec GasCanSpec = new ExplosivePropSpec(
            maxHealth: 10,
            maxDamage: 120,
            damageRadius: 4f,
            fullDamageRadius: 1f,
            structureDamageAtCenter: 120,
            burnSeconds: 2f);

        public int Order => 0;

        public string Name => RootName;

        public Bounds Footprint
        {
            get
            {
                var min = new Vector3(DummyPosition.x - 0.5f, 0f, Center.z - 0.5f);
                var max = new Vector3(GasCanPosition.x + (GasCanSize.x / 2f), TargetDummy.HeadLocalPosition.y + 0.25f, Center.z + 0.5f);
                var bounds = new Bounds();
                bounds.SetMinMax(min, max);
                return bounds;
            }
        }

        public static Vector3 DummyPosition => Center + new Vector3(-Spacing, 0f, 0f);

        public static Vector3 FirstBarrelPosition => Center;

        public static Vector3 SecondBarrelPosition => Center + new Vector3(Spacing, 0f, 0f);

        public static Vector3 GasCanPosition => Center + new Vector3(Spacing * 2f, 0f, 0f);

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            try
            {
                SceneManager.MoveGameObjectToScene(root, scene);

                // 射撃場の出現位置 (-Z 側) から見えるよう、プレイヤーの方を向ける。
                TargetDummy.Create(DummyId, DummyPosition, Quaternion.LookRotation(Vector3.back, Vector3.up), root.transform);

                CreateProp(
                    root.transform,
                    PrimitiveType.Cylinder,
                    FirstBarrelName,
                    FirstBarrelPosition,
                    new Vector3(BarrelRadius * 2f, BarrelHeight / 2f, BarrelRadius * 2f),
                    BarrelHeight,
                    BarrelSpec);
                CreateProp(
                    root.transform,
                    PrimitiveType.Cylinder,
                    SecondBarrelName,
                    SecondBarrelPosition,
                    new Vector3(BarrelRadius * 2f, BarrelHeight / 2f, BarrelRadius * 2f),
                    BarrelHeight,
                    BarrelSpec);
                CreateProp(root.transform, PrimitiveType.Cube, GasCanName, GasCanPosition, GasCanSize, GasCanSize.y, GasCanSpec);
                return root;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        private static ExplosiveProp CreateProp(
            Transform parent, PrimitiveType shape, string name, Vector3 feet, Vector3 scale, float height, ExplosivePropSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var prop = GameObject.CreatePrimitive(shape);
            prop.name = name;
            prop.transform.SetParent(parent, false);
            prop.transform.SetPositionAndRotation(feet + (Vector3.up * (height / 2f)), Quaternion.identity);
            prop.transform.localScale = scale;

            var explosive = prop.AddComponent<ExplosiveProp>();
            explosive.Id = name;
            explosive.Configure(spec);
            return explosive;
        }
    }
}
