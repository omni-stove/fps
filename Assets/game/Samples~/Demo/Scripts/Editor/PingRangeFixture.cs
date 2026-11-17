using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public sealed class PingRangeFixture : ITpsRangeFixture
    {
        public const string FixtureName = "PingRange";
        public const string BlindName = "PingBlind";
        public const string StandName = "Stand";
        public const string ShowName = "Show";
        public const string HiddenName = "Hidden";

        // 標的ダミーの頭 (TargetDummy.HeadLocalPosition) と肩越しのカメラ (ViewRig.DefaultThirdPersonOffset) より高くして、立って覗いても裏が見えないようにする。
        public const float BlindHeight = 2.5f;

        // 肩越しのカメラが横にずれても裏が見えない幅にする。
        public const float BlindWidth = 4f;

        public const float BlindThickness = 0.4f;

        public static readonly Vector3 StandPoint = new Vector3(-16f, 0f, 3f);

        public static readonly Vector3 ShowPoint = new Vector3(-16f, 0f, 8f);

        public static readonly Vector3 HiddenPoint = new Vector3(-16f, 0f, 12.5f);

        public static readonly LevelBox Blind = new LevelBox(
            BlindName, new Vector3(-16f, BlindHeight / 2f, 11f), new Vector3(BlindWidth, BlindHeight, BlindThickness));

        public const int PlaceOrder = 500;

        public int Order => PlaceOrder;

        public string Name => FixtureName;

        public Bounds Footprint => new Bounds(
            new Vector3(-16f, BlindHeight / 2f, 7.875f), new Vector3(BlindWidth + 0.5f, BlindHeight, 10.25f));

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(FixtureName);
            SceneManager.MoveGameObjectToScene(root, scene);
            root.transform.SetPositionAndRotation(ShowPoint, Quaternion.identity);
            SceneBuildHelpers.CreateStaticBox(scene, root.transform, Blind.Name, Blind.Center, Blind.Size);
            AddPoint(root.transform, StandName, StandPoint);
            AddPoint(root.transform, ShowName, ShowPoint);
            AddPoint(root.transform, HiddenName, HiddenPoint);
            return root;
        }

        private static void AddPoint(Transform parent, string name, Vector3 position)
        {
            var point = new GameObject(name);
            point.transform.SetParent(parent, false);
            point.transform.SetPositionAndRotation(position, Quaternion.identity);
        }
    }
}
