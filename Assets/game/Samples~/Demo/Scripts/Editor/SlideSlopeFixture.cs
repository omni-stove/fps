using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public sealed class SlideSlopeFixture : ITpsRangeFixture
    {
        public const int FixtureOrder = 100;
        public const string FixtureName = "SlideSlope";
        public const string RampName = "SlideRamp";

        // CharacterController の登れる角度 (既定 45°) より十分ゆるくして、歩いて上れるようにする。
        public const float SlopeDegrees = 12f;

        public const float RampLength = 14f;

        public const float RampWidth = 4f;
        public const float RampThickness = 0.5f;

        // ダッシュで速さに乗るまでの助走の分だけ、滑り始める位置を上の端から下げる。
        public const float SlopeStartFromTop = 1.5f;

        // 平らな床で滑る分 (SlideMotion の既定で 7m ほど) と、坂から滑り降りた続きが収まる長さにする。
        public const float FlatLaneLength = 15f;

        public static readonly Vector3 RampFoot = new Vector3(-34f, 0f, -8f);

        public static readonly Vector3 FlatStart = RampFoot + new Vector3(0f, 0f, -4f);

        public int Order => FixtureOrder;

        public string Name => FixtureName;

        public static Quaternion RampRotation => Quaternion.Euler(-SlopeDegrees, 0f, 0f);

        public static Vector3 RampCenter => RampFoot - (RampRotation * new Vector3(0f, RampThickness / 2f, -RampLength / 2f));

        public static Vector3 SlopeStart => SurfacePoint(SlopeStartFromTop);

        public static Vector3 Downhill => Vector3.back;

        public Bounds Footprint
        {
            get
            {
                var bounds = RampBounds();
                var laneEnd = RampFoot + new Vector3(0f, 1f, -FlatLaneLength);
                bounds.Encapsulate(new Bounds(laneEnd, new Vector3(RampWidth, 2f, 0f)));
                return bounds;
            }
        }

        public static Vector3 SurfacePoint(float fromTop)
        {
            return RampCenter + (RampRotation * new Vector3(0f, RampThickness / 2f, (RampLength / 2f) - fromTop));
        }

        public static Bounds RampBounds()
        {
            var center = RampCenter;
            var rotation = RampRotation;
            var half = new Vector3(RampWidth, RampThickness, RampLength) / 2f;
            var bounds = new Bounds(center, Vector3.zero);
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -half.x : half.x,
                    (i & 2) == 0 ? -half.y : half.y,
                    (i & 4) == 0 ? -half.z : half.z);
                bounds.Encapsulate(center + (rotation * corner));
            }

            return bounds;
        }

        // 坂はルールで Slide が Off でもただの足場として置いておく (Off のときにしゃがむだけになるのを同じ場所で確かめられる)。
        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(FixtureName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var ramp = SceneBuildHelpers.CreateStaticBox(
                scene, root.transform, RampName, RampCenter, new Vector3(RampWidth, RampThickness, RampLength));
            ramp.transform.rotation = RampRotation;
            return root;
        }
    }
}
