using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場にスライディング (<see cref="SlideMovement"/>) を試す場所を置く。左奥に、+Z の端が高く -Z の端が床に着く坂 (<see cref="RampName"/>) と、
    /// その足元から -Z へ続く平らな走路を用意する。坂の上から -Z へダッシュして滑ると、平らな床より長く滑る。
    /// 平らな床で滑る始点は <see cref="FlatStart"/>、坂で滑る始点は <see cref="SlopeStart"/> (どちらも -Z へ向いて立つ足元)。
    /// </summary>
    public sealed class SlideSlopeFixture : ITpsRangeFixture
    {
        public const int FixtureOrder = 100;
        public const string FixtureName = "SlideSlope";
        public const string RampName = "SlideRamp";

        /// <summary>坂の傾き (度)。CharacterController の登れる角度 (既定 45°) より十分ゆるく、歩いて上れる。</summary>
        public const float SlopeDegrees = 12f;

        /// <summary>坂の面に沿った長さ (m)。</summary>
        public const float RampLength = 14f;

        public const float RampWidth = 4f;
        public const float RampThickness = 0.5f;

        /// <summary>坂の上で滑り始める位置の、上の端から面に沿って下った距離 (m)。ダッシュで速さに乗るまでの助走を含む。</summary>
        public const float SlopeStartFromTop = 1.5f;

        /// <summary>坂の足元から -Z へ続く平らな走路の長さ (m)。平らな床で滑る分 (<see cref="SlideMotion"/> の既定で 7m ほど) と、坂から滑り降りた続きが収まる。</summary>
        public const float FlatLaneLength = 15f;

        /// <summary>坂の上の面が床に着く辺の中心。坂はここから +Z へ上る。</summary>
        public static readonly Vector3 RampFoot = new Vector3(-18f, 0f, -8f);

        /// <summary>平らな床で滑り始める足元。坂の足元から走路へ 4m 入った所。</summary>
        public static readonly Vector3 FlatStart = RampFoot + new Vector3(0f, 0f, -4f);

        public int Order => FixtureOrder;

        public string Name => FixtureName;

        /// <summary>坂の回転。+Z の端を持ち上げる。</summary>
        public static Quaternion RampRotation => Quaternion.Euler(-SlopeDegrees, 0f, 0f);

        /// <summary>坂の箱の中心。上の面の低い辺が <see cref="RampFoot"/> に来るように置く (箱の下側は床に埋まる)。</summary>
        public static Vector3 RampCenter => RampFoot - (RampRotation * new Vector3(0f, RampThickness / 2f, -RampLength / 2f));

        /// <summary>坂の上で滑り始める足元 (坂の面の上)。</summary>
        public static Vector3 SlopeStart => SurfacePoint(SlopeStartFromTop);

        /// <summary>坂を下る向き (ワールド、水平の単位ベクトル)。</summary>
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

        /// <summary>坂の上の面のうち、上の端から面に沿って fromTop (m) 下った所の中心。</summary>
        public static Vector3 SurfacePoint(float fromTop)
        {
            return RampCenter + (RampRotation * new Vector3(0f, RampThickness / 2f, (RampLength / 2f) - fromTop));
        }

        /// <summary>傾けた坂の箱を囲む範囲 (ワールド)。</summary>
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
