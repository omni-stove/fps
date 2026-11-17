using Shooter.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 降りる先の +Z 側 (開いて流される 20m ほど) は空けておく。
    public sealed class ParachuteTowerFixture : ITpsRangeFixture
    {
        public const string TowerName = "ParachuteTower";

        // 開ける高さの 2 倍にして、端から落ちて少し下がってからでも開けるようにする。
        // この高さなら既定の重力 (PlayerController.DefaultGravity) で開かずに落ちると約 22 m/s になり、MotorSpec.DefaultSafeFallSpeed を超えて落下ダメージを受ける。
        public const float TowerHeight = ParachuteMotion.DefaultMinOpenHeight * 2f;

        // プレイヤー (直径 1m) が立って歩き出せる広さにする。
        public const float TowerWidth = 4f;

        public static readonly Vector3 TowerBase = new Vector3(-28f, 0f, -12f);

        public int Order => 0;

        public string Name => TowerName;

        public Bounds Footprint => new Bounds(TowerCenter, TowerSize);

        public static Vector3 TowerCenter => TowerBase + (Vector3.up * (TowerHeight / 2f));

        public static Vector3 TowerSize => new Vector3(TowerWidth, TowerHeight, TowerWidth);

        // ルールが Off でも置くのは、射撃場のデバッグパネルで On にして読み直したときにも試せるようにするため。
        public GameObject Place(Scene scene, GameRules rules)
        {
            return SceneBuildHelpers.CreateStaticBox(scene, null, TowerName, TowerCenter, TowerSize);
        }
    }
}
