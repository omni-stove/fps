using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public sealed class ProneCrawlwayFixture : ITpsRangeFixture
    {
        public const string FixtureName = "ProneCrawlway";
        public const string CeilingName = "Ceiling";
        public const string LeftWallName = "Wall_Left";
        public const string RightWallName = "Wall_Right";

        public static readonly Vector3 Center = new Vector3(-11.5f, 0f, -9f);

        // 伏せとしゃがみの高さの間にして、下では伏せたままで立つこともしゃがむこともできないようにする。
        public const float CeilingHeight = (ProneSpec.DefaultHeight + PlayerController.DefaultCrouchHeight) / 2f;

        public const float CeilingThickness = 0.2f;

        public const float InnerWidth = 2.4f;

        public const float Length = 3f;

        public const float WallThickness = 0.3f;

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
