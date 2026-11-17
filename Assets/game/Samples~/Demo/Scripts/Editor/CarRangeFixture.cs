using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 走る場所を取るため、ほかの物の無い射撃場の一番手前 (z = -32 の帯) に置く。
    public sealed class CarRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "CarLane";
        public const string CarName = CarFixtureBuilder.DefaultName;
        public const string TargetId = "CarTarget";
        public const int FixtureOrder = 600;

        public const float LaneHalfWidth = 3f;

        public static readonly Vector3 CarOrigin = new Vector3(-8f, 0f, -32f);
        public static readonly Quaternion CarRotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
        public static readonly Vector3 TargetGround = new Vector3(8f, 0f, -32f);

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint
        {
            get
            {
                var car = CarFixtureBuilder.BodyBounds(CarOrigin, CarRotation);
                var height = Mathf.Max(car.max.y, TargetDummy.HeadLocalPosition.y + TargetDummy.HeadScale.y);
                var minX = car.min.x;
                var maxX = TargetGround.x + 0.5f;
                return new Bounds(
                    new Vector3((minX + maxX) / 2f, height / 2f, CarOrigin.z),
                    new Vector3(maxX - minX, height, LaneHalfWidth * 2f));
            }
        }

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            try
            {
                CarFixtureBuilder.Create(scene, root.transform, CarName, CarOrigin, CarRotation);
                TargetDummy.Create(TargetId, TargetGround, Quaternion.LookRotation(Vector3.left, Vector3.up), root.transform);
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
