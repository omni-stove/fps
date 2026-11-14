using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場に置く、伏せを試す低い天井のくぐり道。左右の壁の上に天井の板を渡し、+Z へ伏せてくぐる向きに置く。
    /// 天井の下面は伏せ (<see cref="ProneSpec.DefaultHeight"/>) としゃがみ (<see cref="PlayerController.DefaultCrouchHeight"/>) の間なので、
    /// 下では伏せたままで、立つこともしゃがむこともできない。試す手順は <see cref="ProneSmokeStep"/>。
    /// </summary>
    public sealed class ProneCrawlwayFixture : ITpsRangeFixture
    {
        public const string FixtureName = "ProneCrawlway";
        public const string CeilingName = "Ceiling";
        public const string LeftWallName = "Wall_Left";
        public const string RightWallName = "Wall_Right";

        /// <summary>くぐり道の床の中心。射撃場の左手前 (乗り越えの壁より手前、開けた場所の反対側) の空き地。</summary>
        public static readonly Vector3 Center = new Vector3(-12f, 0f, -12f);

        /// <summary>天井の下面の、床からの高さ (m)。伏せとしゃがみの高さの真ん中。</summary>
        public const float CeilingHeight = (ProneSpec.DefaultHeight + PlayerController.DefaultCrouchHeight) / 2f;

        /// <summary>天井の板の厚さ (m)。</summary>
        public const float CeilingThickness = 0.2f;

        /// <summary>くぐり道の内側の幅 (m、X 方向)。プレイヤーの直径の 2 倍あまり。</summary>
        public const float InnerWidth = 2.4f;

        /// <summary>くぐり道の長さ (m、Z 方向)。</summary>
        public const float Length = 3f;

        /// <summary>左右の壁の厚さ (m)。</summary>
        public const float WallThickness = 0.3f;

        /// <summary>
        /// くぐり道の手前 (-Z 側) の入り口の外に立つ位置 (床の高さ)。天井の端からプレイヤーの半径と 1m 離す。
        /// smoke はここで伏せ、天井の下 (<see cref="Center"/>) へ移して起き上がれないことを確かめる。
        /// </summary>
        public static Vector3 Entrance => Center + new Vector3(0f, 0f, -((Length / 2f) + GameBootstrap.PlayerRadius + 1f));

        public int Order => 0;

        public string Name => FixtureName;

        public Bounds Footprint
        {
            get
            {
                var outerWidth = InnerWidth + (WallThickness * 2f);
                var height = CeilingHeight + CeilingThickness;
                return new Bounds(Center + new Vector3(0f, height / 2f, 0f), new Vector3(outerWidth, height, Length));
            }
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(FixtureName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            var wallOffset = (InnerWidth + WallThickness) / 2f;
            var wallSize = new Vector3(WallThickness, CeilingHeight, Length);
            SceneBuildHelpers.CreateStaticBox(
                scene, root.transform, LeftWallName, Center + new Vector3(-wallOffset, CeilingHeight / 2f, 0f), wallSize);
            SceneBuildHelpers.CreateStaticBox(
                scene, root.transform, RightWallName, Center + new Vector3(wallOffset, CeilingHeight / 2f, 0f), wallSize);
            SceneBuildHelpers.CreateStaticBox(
                scene,
                root.transform,
                CeilingName,
                Center + new Vector3(0f, CeilingHeight + (CeilingThickness / 2f), 0f),
                new Vector3(InnerWidth + (WallThickness * 2f), CeilingThickness, Length));
            return root;
        }
    }
}
