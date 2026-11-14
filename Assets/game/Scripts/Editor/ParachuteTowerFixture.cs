using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// TPS の射撃場に置く、パラシュートを試す高い台 (<see cref="TowerName"/>)。上から +Z へ歩いて飛び降りると、
    /// 開ける高さ (<see cref="ParachuteMotion.DefaultMinOpenHeight"/>) を十分に超えたところで開け、開かずに落ちれば落下ダメージを受ける高さにする。
    /// 左奥 (x = -19..-15, z = -17..-13) に置き、降りる先の +Z 側は空けておく。
    /// </summary>
    public sealed class ParachuteTowerFixture : ITpsRangeFixture
    {
        public const string TowerName = "ParachuteTower";

        /// <summary>
        /// 台の高さ (m)。<see cref="ParachuteMotion.DefaultMinOpenHeight"/> の 2 倍で、端から落ちて少し下がってからでも開ける。
        /// 既定の重力 (<see cref="PlayerController.DefaultGravity"/>) で開かずに落ちると約 22 m/s になり、<see cref="MotorSpec.DefaultSafeFallSpeed"/> を超える。
        /// </summary>
        public const float TowerHeight = ParachuteMotion.DefaultMinOpenHeight * 2f;

        /// <summary>台の上面の x, z の広さ (m)。プレイヤー (直径 1m) が立って歩き出せる広さ。</summary>
        public const float TowerWidth = 4f;

        /// <summary>台の床面の中心 (地面の高さ)。</summary>
        public static readonly Vector3 TowerBase = new Vector3(-17f, 0f, -15f);

        public int Order => 0;

        public string Name => TowerName;

        public Bounds Footprint => new Bounds(TowerCenter, TowerSize);

        /// <summary>台の箱の中心 (ワールド)。</summary>
        public static Vector3 TowerCenter => TowerBase + (Vector3.up * (TowerHeight / 2f));

        /// <summary>台の箱の大きさ。</summary>
        public static Vector3 TowerSize => new Vector3(TowerWidth, TowerHeight, TowerWidth);

        // ルールが Off でも置くのは、射撃場のデバッグパネルで On にして読み直したときにも試せるようにするため。
        public GameObject Place(Scene scene, GameRules rules)
        {
            return SceneBuildHelpers.CreateStaticBox(scene, null, TowerName, TowerCenter, TowerSize);
        }
    }
}
