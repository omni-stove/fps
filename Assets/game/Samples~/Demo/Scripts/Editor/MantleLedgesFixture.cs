using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場に置く、崖のぼりを試す段の組 (<see cref="RootName"/>)。左手前 (x = -22..-14, z = -10..-8) に、+Z へ登る向きで 3 つ並べる。
    /// <list type="bullet">
    /// <item><see cref="HighLedgeName"/>: 乗り越えより高くジャンプでも届かない、奥行きのある段。よじ登って上に立てる。</item>
    /// <item><see cref="NarrowLedgeName"/>: 同じ高さだが薄く、上に立てないので登らない。</item>
    /// <item><see cref="CoveredLedgeName"/>: 同じ高さで奥行きもあるが、縁の上に天井 (<see cref="CeilingName"/>) があり頭上が塞がっているので登らない。</item>
    /// </list>
    /// 手順は <see cref="MantleSmokeStep"/>。
    /// </summary>
    public sealed class MantleLedgesFixture : ITpsRangeFixture
    {
        public const string RootName = "MantleLedges";
        public const string HighLedgeName = "MantleLedge_High";
        public const string NarrowLedgeName = "MantleLedge_Narrow";
        public const string CoveredLedgeName = "MantleLedge_Covered";
        public const string CeilingName = "MantleLedge_Ceiling";

        /// <summary>
        /// 段の高さ (m)。<see cref="MotorSpec.DefaultVaultMaxHeight"/> とプレイヤーのジャンプ (1.2 m) より高く、
        /// <see cref="MotorSpec.DefaultMantleMaxHeight"/> より低い。
        /// </summary>
        public const float LedgeHeight = 1.8f;

        /// <summary>登れる段と天井のある段の奥行き (m)。立ち 2 m・半径 0.5 m の体が縁の上に立てる。</summary>
        public const float DeepLedgeDepth = 2f;

        /// <summary>薄い段の奥行き (m)。体の直径より薄いので上に立てない。</summary>
        public const float NarrowLedgeDepth = 0.4f;

        /// <summary>天井のある段の、縁の上から天井の下面までの高さ (m)。立ち 2 m の体が入らない。</summary>
        public const float CeilingClearance = 1.2f;

        /// <summary>天井の厚さ (m)。</summary>
        public const float CeilingThickness = 0.2f;

        /// <summary>段の幅 (x 方向、m)。</summary>
        public const float LedgeWidth = 2f;

        /// <summary>段の手前の面の z (m)。プレイヤーは z がこれより小さい側から +Z へ登る。</summary>
        public const float FrontZ = -10f;

        /// <summary>登れる段・薄い段・天井のある段の中心の x (m)。</summary>
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

        /// <summary>段ごとの名前・中心・大きさ (ワールド座標)。天井も含む。</summary>
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
