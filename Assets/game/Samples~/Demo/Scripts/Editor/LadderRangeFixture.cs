using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // はしごを使うかはルールの ActionRules.Ladder で決まるので、ルールに関わらず置く。
    public sealed class LadderRangeFixture : ITpsRangeFixture
    {
        public const string FixtureName = "LadderTower";

        public static readonly Vector3 TowerBaseCenter = new Vector3(-17f, 0f, -15f);

        public static readonly Vector3 TowerSize = new Vector3(4f, 3f, 4f);

        public static readonly Vector3 LadderSide = Vector3.right;

        public const float LadderWidth = 0.8f;

        public const float ApproachDepth = 1.5f;

        public int Order => 100;

        public string Name => FixtureName;

        public Bounds Footprint => LadderTowerBuilder.Footprint(TowerBaseCenter, TowerSize, LadderSide, ApproachDepth);

        public static string LadderPath => FixtureName + "/" + LadderTowerBuilder.LadderName;

        public GameObject Place(Scene scene, GameRules rules)
        {
            return LadderTowerBuilder.Create(scene, FixtureName, TowerBaseCenter, TowerSize, LadderSide, LadderWidth);
        }
    }
}
