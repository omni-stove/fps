using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public sealed class MantleLedgesFixture : ITpsRangeFixture
    {
        public const string RootName = "MantleLedges";
        public const string HighLedgeName = "MantleLedge_High";
        public const string NarrowLedgeName = "MantleLedge_Narrow";
        public const string CoveredLedgeName = "MantleLedge_Covered";
        public const string CeilingName = "MantleLedge_Ceiling";

        // 乗り越えでもジャンプでもなく崖のぼりでだけ上がれるよう、MotorSpec.DefaultVaultMaxHeight とプレイヤーのジャンプ (1.2 m) より高く、
        // MotorSpec.DefaultMantleMaxHeight より低くする。
        public const float LedgeHeight = 1.8f;

        // 立ち 2 m・半径 0.5 m の体が縁の上に立てる奥行きにする。
        public const float DeepLedgeDepth = 2f;

        // 体の直径より薄くして、上に立てないので登らないようにする。
        public const float NarrowLedgeDepth = 0.4f;

        // 立ち 2 m の体が入らない高さにして、頭上が塞がっているので登らないようにする。
        public const float CeilingClearance = 1.2f;

        public const float CeilingThickness = 0.2f;

        public const float LedgeWidth = 2f;

        public const float FrontZ = -10f;

        public const float HighLedgeX = -21f;
        public const float NarrowLedgeX = -18f;
        public const float CoveredLedgeX = -15f;

        public int Order => 100;

        public string Name => RootName;

        public Bounds Footprint
        {
            get
            {
                var top = LedgeHeight + CeilingClearance + CeilingThickness;
                var minX = HighLedgeX - (LedgeWidth / 2f);
                var maxX = CoveredLedgeX + (LedgeWidth / 2f);
                var center = new Vector3((minX + maxX) / 2f, top / 2f, FrontZ + (DeepLedgeDepth / 2f));
                return new Bounds(center, new Vector3(maxX - minX, top, DeepLedgeDepth));
            }
        }

        public static IReadOnlyList<LevelBox> Boxes()
        {
            return new[]
            {
                Ledge(HighLedgeName, HighLedgeX, DeepLedgeDepth),
                Ledge(NarrowLedgeName, NarrowLedgeX, NarrowLedgeDepth),
                Ledge(CoveredLedgeName, CoveredLedgeX, DeepLedgeDepth),
                new LevelBox(
                    CeilingName,
                    new Vector3(CoveredLedgeX, LedgeHeight + CeilingClearance + (CeilingThickness / 2f), FrontZ + (DeepLedgeDepth / 2f)),
                    new Vector3(LedgeWidth, CeilingThickness, DeepLedgeDepth)),
            };
        }

        // 天井は段とは別のコライダーにする。上端を測る Ray は当たった段にだけ下ろすので、天井は頭上の塞がりとしてだけ効く。
        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            foreach (var box in Boxes())
            {
                SceneBuildHelpers.CreateStaticBox(scene, root.transform, box.Name, box.Center, box.Size);
            }

            return root;
        }

        private static LevelBox Ledge(string name, float x, float depth)
        {
            return new LevelBox(
                name, new Vector3(x, LedgeHeight / 2f, FrontZ + (depth / 2f)), new Vector3(LedgeWidth, LedgeHeight, depth));
        }
    }
}
