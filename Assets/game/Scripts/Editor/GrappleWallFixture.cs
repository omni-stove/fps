using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    // グラップルを試す、-X を向いた高い壁 2 枚。右の中ほど (x = 11..23, z = 3..10) の、開けた場所・小屋・標的の列・高い柱から離れた所に置く。
    // 片方は掴めて縁の上に立てる奥行きがあり、もう片方は同じ形で掴めないレイヤーに置く。手順は GrappleSmokeStep。
    public sealed class GrappleWallFixture : ITpsRangeFixture
    {
        public const string RootName = "GrappleWalls";
        public const string GrabbableWallName = "GrappleWall";
        public const string NotGrabbableWallName = "GrappleWall_NotGrabbable";

        // キットはゲームのプロジェクトにレイヤーを足せないので、組み込みのレイヤーのうち Ray にも体にも当たる TransparentFX を
        // 「掴めない面」に使う。射撃場のグラップル (AbilityAssetBuilder.GrappleGrabbableLayers) は Default だけを掴む。
        public const int NotGrabbableLayer = 1;

        // ジャンプ (1.2 m) でも崖のぼり (MotorSpec.DefaultMantleMaxHeight) でも届かず、グラップルでないと上がれない高さ。
        public const float WallHeight = 5f;

        // 上がった後に縁の上に立てるよう、VaultProbe.MantleFootingDepth より厚くする。
        public const float WallThickness = 2f;

        public const float WallWidth = 3f;
        public const float FaceX = 21f;
        public const float GrabbableWallZ = 4.5f;
        public const float NotGrabbableWallZ = 8.5f;

        // 目から縁までが GrappleSpec.DefaultMaxRange に十分収まり、GrappleSpec.DefaultMinRange より遠い。
        public const float StandX = 12f;

        public int Order => 100;

        public string Name => RootName;

        public Bounds Footprint
        {
            get
            {
                var minX = StandX - 1f;
                var maxX = FaceX + WallThickness;
                var minZ = GrabbableWallZ - (WallWidth / 2f);
                var maxZ = NotGrabbableWallZ + (WallWidth / 2f);
                return new Bounds(
                    new Vector3((minX + maxX) / 2f, WallHeight / 2f, (minZ + maxZ) / 2f),
                    new Vector3(maxX - minX, WallHeight, maxZ - minZ));
            }
        }

        public static Vector3 StandPoint => new Vector3(StandX, 0f, GrabbableWallZ);

        public static Vector3 NotGrabbableStandPoint => new Vector3(StandX, 0f, NotGrabbableWallZ);

        public static LevelBox GrabbableWall => Wall(GrabbableWallName, GrabbableWallZ);

        public static LevelBox NotGrabbableWall => Wall(NotGrabbableWallName, NotGrabbableWallZ);

        // ルールで能力が Off でも置くのは、射撃場のデバッグパネルで On にして読み直したときにも試せるようにするため。
        public GameObject Place(Scene scene, GameRules rules)
        {
            var root = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(root, scene);
            var grabbable = GrabbableWall;
            SceneBuildHelpers.CreateStaticBox(scene, root.transform, grabbable.Name, grabbable.Center, grabbable.Size);
            var notGrabbable = NotGrabbableWall;
            var blocked = SceneBuildHelpers.CreateStaticBox(scene, root.transform, notGrabbable.Name, notGrabbable.Center, notGrabbable.Size);
            blocked.layer = NotGrabbableLayer;
            return root;
        }

        private static LevelBox Wall(string name, float z)
        {
            return new LevelBox(
                name,
                new Vector3(FaceX + (WallThickness / 2f), WallHeight / 2f, z),
                new Vector3(WallThickness, WallHeight, WallWidth));
        }
    }
}
