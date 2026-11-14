using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場に、印 (<see cref="Fps.Runtime.PingController"/>) を試す場所を置く。左奥 (<see cref="ShowPoint"/> の周り) に、
    /// 立つ位置 (<see cref="StandPoint"/>)、敵を見せる位置 (<see cref="ShowPoint"/>)、敵を隠す壁 (<see cref="Blind"/>) とその裏 (<see cref="HiddenPoint"/>) を
    /// 手前から奥へ一直線に並べる。smoke (<see cref="PingSmokeStep"/>) は標的ダミーを見せる位置に置いて印を付け、壁の裏へ動かして見失わせる。
    /// 位置はどれも床の高さで、子の空の GameObject (<see cref="StandName"/> など) にも置くので、シーンを開いて場所を確かめられる。
    /// </summary>
    public sealed class PingRangeFixture : ITpsRangeFixture
    {
        public const string FixtureName = "PingRange";
        public const string BlindName = "PingBlind";
        public const string StandName = "Stand";
        public const string ShowName = "Show";
        public const string HiddenName = "Hidden";

        /// <summary>
        /// 敵を隠す壁の高さ (m)。標的ダミーの頭 (<see cref="Fps.Runtime.TargetDummy.HeadLocalPosition"/>) と、
        /// 肩越しのカメラ (<see cref="Fps.Runtime.ViewRig.DefaultThirdPersonOffset"/>) より高く、立って覗いても裏が見えない。
        /// </summary>
        public const float BlindHeight = 2.5f;

        /// <summary>敵を隠す壁の幅 (m)。肩越しのカメラが横にずれても裏が見えない幅。</summary>
        public const float BlindWidth = 4f;

        public const float BlindThickness = 0.4f;

        /// <summary>プレイヤーが立つ位置 (床の高さ)。+Z を向くと、見せる位置・壁・裏が正面に並ぶ。</summary>
        public static readonly Vector3 StandPoint = new Vector3(-16f, 0f, 3f);

        /// <summary>敵を見せる位置 (床の高さ)。立つ位置から 5m 先で、間に遮るものが無い。</summary>
        public static readonly Vector3 ShowPoint = new Vector3(-16f, 0f, 8f);

        /// <summary>敵を隠す位置 (床の高さ)。立つ位置から見て壁の真裏。</summary>
        public static readonly Vector3 HiddenPoint = new Vector3(-16f, 0f, 12.5f);

        /// <summary>敵を隠す壁。見せる位置と隠す位置の間に、+Z へ撃つ向きで置く。</summary>
        public static readonly LevelBox Blind = new LevelBox(
            BlindName, new Vector3(-16f, BlindHeight / 2f, 11f), new Vector3(BlindWidth, BlindHeight, BlindThickness));

        /// <summary><see cref="Order"/>。</summary>
        public const int PlaceOrder = 500;

        public int Order => PlaceOrder;

        public string Name => FixtureName;

        // 立つ位置から隠す位置の先 (ダミーの半径) までと、壁の幅を囲む。
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
