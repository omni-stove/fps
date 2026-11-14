using System;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場に、爆発する置き物 (<see cref="ExplosiveProp"/>) を x 方向に並べる。左から標的ダミー、樽 2 つ、ガソリン缶の順で、
    /// 樽 A を撃つと隣の標的ダミーが倒れ、樽 B が連鎖して遅れて爆発し、ガソリン缶が燃えてからさらに遅れて爆発する間隔にする。
    /// smoke の手順は <see cref="ExplosivePropSmokeStep"/>。
    /// </summary>
    /// <remarks>
    /// 置き場所は左奥 (<see cref="Center"/>)。乗り越えの壁・開けた場所・小屋・GameBootstrap の標的ダミーの列から離す。
    /// 標的ダミーは近くの敵の代わりで、射撃場には NavMesh が無く <see cref="EnemyAgent"/> を置けないため。
    /// </remarks>
    public sealed class ExplosivePropRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "ExplosiveProps";
        public const string FirstBarrelName = "Barrel_A";
        public const string SecondBarrelName = "Barrel_B";
        public const string GasCanName = "GasCan";

        /// <summary>樽 A の爆発で倒れる標的ダミーの名前と Id。</summary>
        public const string DummyId = "ExplosiveDummy";

        /// <summary>並べる間隔 (m)。隣の物の中心どうしの x の差。</summary>
        public const float Spacing = 1.6f;

        /// <summary>樽 (円柱) の半径と高さ (m)。</summary>
        public const float BarrelRadius = 0.3f;
        public const float BarrelHeight = 0.9f;

        /// <summary>ガソリン缶 (箱) の大きさ (m)。</summary>
        public static readonly Vector3 GasCanSize = new Vector3(0.35f, 0.45f, 0.25f);

        /// <summary>樽 A の足元の位置。ほかの物はここから x 方向に <see cref="Spacing"/> ずつ並べる。</summary>
        public static readonly Vector3 Center = new Vector3(-16f, 0f, 14f);

        /// <summary>樽の値。キットの既定値のまま。</summary>
        public static readonly ExplosivePropSpec BarrelSpec = ExplosivePropSpec.Default;

        /// <summary>ガソリン缶の値。弱い 1 発で燃え始め、2 秒燃えてから樽より小さく爆発する。</summary>
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

        /// <summary>標的ダミーの足元の位置。樽 A の左。</summary>
        public static Vector3 DummyPosition => Center + new Vector3(-Spacing, 0f, 0f);

        /// <summary>樽 A の足元の位置。</summary>
        public static Vector3 FirstBarrelPosition => Center;

        /// <summary>樽 B の足元の位置。樽 A の右。</summary>
        public static Vector3 SecondBarrelPosition => Center + new Vector3(Spacing, 0f, 0f);

        /// <summary>ガソリン缶の足元の位置。樽 B の右。</summary>
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
