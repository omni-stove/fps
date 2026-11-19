using System;
using System.Collections.Generic;
using Shooter.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public sealed class HardpointSceneBuilder : ModeSceneBuilder
    {
        public static string ScenePathValue => BuildSceneList.HardpointScene;
        public static string RulesPathValue => GameRulesAssetBuilder.HardpointPath;

        public static string DefinitionPath => ModeDefinitionAssets.PathOf(GameRulesAssetBuilder.HardpointName);
        public const string MenuPath = SceneBuildSteps.MenuPath;

        public const string PointsRootName = "ControlPoints";

        // 拠点の範囲が遮蔽物と出現点に掛からない大きさにする。
        public const float PointRadius = 4f;

        // 並びが有効になる順。手前と奥はどちらの陣営にも偏らないよう、どちらも自陣営の出現点の列から 12m、遮蔽物から半径より離す。
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

        public static void Build()
        {
            new HardpointSceneBuilder().BuildSceneFromMenu();
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            // NewScene(Single) は参照されていないアセットをアンロードするので、シーンを作った後のここで読む。
            var tuning = LoadOrCreateDefinition(DefinitionPath);
            var runner = host.AddComponent<HardpointRunner>();
            runner.Definition = tuning;
            return runner;
        }

        public static HardpointDefinition LoadOrCreateDefinition(string path)
        {
            var definition = ModeDefinitionAssets.LoadOrCreate<HardpointDefinition>(path, _ => { });
            definition.Validate();
            return definition;
        }

        // 始まった後は Runner が有効な拠点の巡回路に替えるので、ここでは最初に有効になる拠点 (番号 0) へだけ向かわせる。
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

    public readonly struct HardpointPlacement
    {
        public string Name { get; }
        public Vector3 Center { get; }

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
