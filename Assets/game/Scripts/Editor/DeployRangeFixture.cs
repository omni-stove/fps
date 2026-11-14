using System.Collections.Generic;
using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // 置ける物 (展開式の盾) を試す場所。プレイヤーは各レーンの手前 (PlayerZ) に立って +Z へ置く。
    // 急な坂のレーン 1 本と平らなレーンを並べ、上限を超えて置けるよう平らなレーンは盾の幅より離す。
    // 撃ち手の目印は当たり判定も陣営も持たない (陣営を持たせると、モードの参加者や敵の狙う相手になる)。
    public sealed class DeployRangeFixture : ITpsRangeFixture
    {
        public const string RootName = "DeployYard";
        public const string RampName = "SteepRamp";
        public const string ShooterName = "DeployShooter";
        public const int FixtureOrder = 540;

        public const float PlayerZ = -20f;
        public const float RampLaneX = -13f;
        public const float RampTiltDegrees = 40f;
        public const float ShooterZ = -11f;
        public const float ShooterHeight = 1.3f;

        public static readonly Vector3 Center = new Vector3(-8.5f, 0f, -15.5f);
        public static readonly Vector3 Size = new Vector3(13f, 3f, 11f);

        public static readonly Vector3 RampCenter = new Vector3(RampLaneX, 1f, -15.5f);
        public static readonly Vector3 RampScale = new Vector3(2.5f, 0.3f, 5f);

        // 0 番は撃ち手の正面に置く。
        public static readonly IReadOnlyList<float> FlatLaneXs = new[] { -9.5f, -6.5f, -3.5f };

        public int Order => FixtureOrder;

        public string Name => RootName;

        public Bounds Footprint => new Bounds(Center + new Vector3(0f, Size.y / 2f, 0f), Size);

        public static Vector3 PlayerGround(float laneX) => new Vector3(laneX, 0f, PlayerZ);

        public static Vector3 ShooterPosition => new Vector3(FlatLaneXs[0], ShooterHeight, ShooterZ);

        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            try
            {
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = Center;

                var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ramp.name = RampName;
                ramp.transform.SetParent(root.transform, false);
                ramp.transform.SetPositionAndRotation(RampCenter, Quaternion.Euler(RampTiltDegrees, 0f, 0f));
                ramp.transform.localScale = RampScale;

                var shooter = new GameObject(ShooterName);
                shooter.transform.SetParent(root.transform, false);
                shooter.transform.SetPositionAndRotation(ShooterPosition, Quaternion.Euler(0f, 180f, 0f));
                var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                marker.name = "Marker";
                Object.DestroyImmediate(marker.GetComponent<Collider>());
                marker.transform.SetParent(shooter.transform, false);
                marker.transform.localPosition = new Vector3(0f, -ShooterHeight / 2f, 0f);
                marker.transform.localScale = new Vector3(0.3f, ShooterHeight / 2f, 0.3f);
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
