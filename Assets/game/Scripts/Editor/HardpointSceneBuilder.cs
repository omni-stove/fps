using System;
using System.Collections.Generic;
using Fps.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// ハードポイントのシーン Hardpoint を組む。<see cref="ModeSceneBuilder"/> の既定の地形・出現点・bot (プレイヤーの陣営 0 に bot 2 体、陣営 1 に 3 体) に、
    /// 拠点 (<see cref="ControlPointZone"/>) を <see cref="Points"/> の 4 つ置き、<see cref="HardpointRunner"/> に配線する。
    /// ルールは <see cref="GameRulesAssetBuilder.HardpointPath"/>。拠点の中心が NavMesh に乗っていて、プレイヤーの出現点から歩けることを確かめ、
    /// 満たさなければシーンを保存せずに例外を投げる。Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.HardpointSceneBuilder.Build</code>
    /// </summary>
    public sealed class HardpointSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.HardpointScene;
        public static string RulesPathValue => GameRulesAssetBuilder.HardpointPath;
        public const string MenuPath = "Fps/Build Hardpoint Scene";

        public const string PointsRootName = "ControlPoints";

        /// <summary>拠点の範囲の半径 (m)。遮蔽物と出現点に掛からない大きさにする。</summary>
        public const float PointRadius = 4f;

        /// <summary>
        /// 拠点の名前と足元の中心。並びが番号 (有効になる順) で、西の中央 → 奥 (陣営 1 寄り) → 東の中央 → 手前 (陣営 0 寄り) と回る。
        /// 手前と奥はどちらも自陣営の出現点の列から 12m、遮蔽物から半径より離す。
        /// </summary>
        public static readonly IReadOnlyList<HardpointPlacement> Points = new[]
        {
            new HardpointPlacement("ControlPoint-A", new Vector3(-12f, 0f, 20f)),
            new HardpointPlacement("ControlPoint-B", new Vector3(0f, 0f, 28f)),
            new HardpointPlacement("ControlPoint-C", new Vector3(12f, 0f, 20f)),
            new HardpointPlacement("ControlPoint-D", new Vector3(0f, 0f, 12f)),
        };

        public HardpointSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        [MenuItem(MenuPath)]
        public static void Build()
        {
            new HardpointSceneBuilder().BuildSceneFromMenu();
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            var runner = host.AddComponent<HardpointRunner>();
            runner.RotateSeconds = HardpointRunner.DefaultRotateSeconds;
            runner.PointsPerSecond = HardpointRunner.DefaultPointsPerSecond;
            runner.ScoreLimit = HardpointRunner.DefaultScoreLimit;
            runner.TimeLimitSeconds = HardpointRunner.DefaultTimeLimitSeconds;
            runner.RespawnSeconds = HardpointRunner.DefaultRespawnSeconds;
            return runner;
        }

        /// <summary>bot は最初に有効になる拠点 (番号 0) へ向かう。始まった後は Runner が有効な拠点の巡回路に替える。</summary>
        protected override IReadOnlyList<Vector3> BotPatrolRoute(IReadOnlyList<ModeSideLayout> sides, int sideIndex, int spawnIndex)
        {
            return new[] { sides[sideIndex].Spawns[spawnIndex].Feet, Points[0].Center };
        }

        protected override void AddModeObjects(ModeSceneContext context)
        {
            var points = new List<Vector3>();
            foreach (var point in Points)
            {
                points.Add(point.Center);
                for (var slot = 0; slot < context.Bots.Count; slot++)
                {
                    points.AddRange(HardpointRunner.RouteAround(point.Center, PointRadius, slot));
                }
            }

            SceneBuildHelpers.RequireOnNavMesh(points, NavMeshCheckRadius, "hardpoint");
            SceneBuildHelpers.RequireReachable(PlayerFeet, points, NavMeshCheckRadius, "hardpoint");

            var root = new GameObject(PointsRootName);
            SceneManager.MoveGameObjectToScene(root, context.Scene);

            var zones = new ControlPointZone[Points.Count];
            for (var i = 0; i < zones.Length; i++)
            {
                var go = new GameObject(Points[i].Name);
                go.transform.SetParent(root.transform, false);
                go.transform.position = Points[i].Center;

                var zone = go.AddComponent<ControlPointZone>();
                zone.Index = i;
                zone.Radius = PointRadius;
                zone.Height = ControlPointZone.DefaultHeight;
                zones[i] = zone;
            }

            var runner = context.Runner as HardpointRunner
                ?? throw new InvalidOperationException($"{nameof(HardpointSceneBuilder)} expects a {nameof(HardpointRunner)}");
            runner.SetZones(zones);
        }
    }

    /// <summary>ハードポイントの拠点 1 つの名前と足元の中心 (<see cref="HardpointSceneBuilder.Points"/>)。</summary>
    public readonly struct HardpointPlacement
    {
        public string Name { get; }
        public Vector3 Center { get; }

        /// <exception cref="ArgumentException">name が空。</exception>
        public HardpointPlacement(string name, Vector3 center)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("a hardpoint needs a name", nameof(name));
            }

            Name = name;
            Center = center;
        }
    }
}
