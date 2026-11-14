using Fps.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場に置く、はしごの付いた台 (<see cref="LadderTowerBuilder"/>)。左奥の空いた所に置き、はしごは台の +X 側 (出現位置の側) に立てる。
    /// はしごを使うかはルールの <see cref="ActionRules.Ladder"/> で決まるので、ルールに関わらず置く。手順は <see cref="LadderSmokeStep"/>。
    /// </summary>
    public sealed class LadderRangeFixture : ITpsRangeFixture
    {
        public const string FixtureName = "LadderTower";

        /// <summary>台の底面の中心。</summary>
        public static readonly Vector3 TowerBaseCenter = new Vector3(-17f, 0f, -15f);

        /// <summary>台の大きさ。高さがはしごの高さ。</summary>
        public static readonly Vector3 TowerSize = new Vector3(4f, 3f, 4f);

        /// <summary>はしごを立てる台の側面の向き。</summary>
        public static readonly Vector3 LadderSide = Vector3.right;

        /// <summary>はしごの幅 (m)。</summary>
        public const float LadderWidth = 0.8f;

        /// <summary>はしごの前で人が立つ奥行き (m)。<see cref="Footprint"/> に含める。</summary>
        public const float ApproachDepth = 1.5f;

        public int Order => 100;

        public string Name => FixtureName;

        public Bounds Footprint => LadderTowerBuilder.Footprint(TowerBaseCenter, TowerSize, LadderSide, ApproachDepth);

        /// <summary>シーンの中のはしごの GameObject の階層の名前 (<see cref="GameObject.Find(string)"/> に渡す)。</summary>
        public static string LadderPath => FixtureName + "/" + LadderTowerBuilder.LadderName;

        public GameObject Place(Scene scene, GameRules rules)
        {
            return LadderTowerBuilder.Create(scene, FixtureName, TowerBaseCenter, TowerSize, LadderSide, LadderWidth);
        }
    }
}
