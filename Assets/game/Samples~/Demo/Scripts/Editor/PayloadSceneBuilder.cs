using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 台車の形は PayloadCart.BodyPrefab を空にして置き、Play 中に仮の箱を作らせる。ゲームはシーンの Inspector で差し替える。
    // 台車の速さや時間は Definition を付けずに入口 (GameContent) か Resources/Tuning/payload.json から読ませる。
    public sealed class PayloadSceneBuilder : ModeSceneBuilder
    {
        public const string SceneName = GameRulesAssetBuilder.PayloadName;

        public const string MenuPath = SceneBuildSteps.MenuPath;

        public const string RouteRootName = "PayloadRoute";
        public const string CartObjectName = "PayloadCart";
        public const string CartZoneName = "CartZone";

        // プレイヤーの陣営 (0 = Alpha) が台車を押す。プレイヤーに台車を押させて試すため。
        public static readonly Side AttackerSide = Side.Alpha;

        // 攻め側はプレイヤーを合わせて 4 人、守り側は 3 人にし、bot だけでも台車が終点まで届くようにする。
        public const int AttackerBots = 3;
        public const int DefenderBots = 3;

        // ControlPointZone の既定と同じ。bot は台車の中心からこの半分の距離を回るので、台車の箱 (半分の対角 1.8m) より外になる。
        public const float CartZoneRadius = ControlPointZone.DefaultRadius;

        // bot が回る点を確かめる間隔 (m)。台車が動く間に、回る点が遮蔽物や壁の中に入らないことを確かめる。
        public const float PatrolCheckStep = 1f;

        // 攻め側の出現点の前から、遮蔽物を避けて S 字に曲がり、守り側の出現点の手前で終わる。
        public static readonly IReadOnlyList<Vector3> RoutePoints = new[]
        {
            new Vector3(0f, 0f, 6f),
            new Vector3(0f, 0f, 12f),
            new Vector3(-8f, 0f, 18f),
            new Vector3(-8f, 0f, 24f),
            new Vector3(0f, 0f, 28f),
            new Vector3(0f, 0f, 34f),
        };

        public static readonly IReadOnlyList<int> CheckpointPoints = new[] { 2, 4 };

        // 既定の遮蔽物は中央を塞ぐので、道筋から台車の範囲より離して置き直す。
        public static readonly IReadOnlyList<ModeObstacle> PayloadObstacles = new[]
        {
            new ModeObstacle("Cover-East", new Vector3(8f, 1.5f, 20f), new Vector3(6f, 3f, 1f)),
            new ModeObstacle("Cover-WestNear", new Vector3(-10f, 0.525f, 10f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-EastNear", new Vector3(10f, 0.525f, 8f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-WestFar", new Vector3(-12f, 0.525f, 30f), new Vector3(4f, 1.05f, 0.5f)),
            new ModeObstacle("Cover-EastFar", new Vector3(10f, 0.525f, 32f), new Vector3(4f, 1.05f, 0.5f)),
        };

        public PayloadSceneBuilder()
            : base(ScenePathValue, RulesPathValue)
        {
        }

        public static string ScenePathValue => BuildSceneList.PayloadScene;

        public static string RulesPathValue => GameRulesAssetBuilder.PayloadPath;

        protected override IReadOnlyList<ModeSideLayout> Sides =>
            AttackerSide == Side.Alpha ? TwoSides(AttackerBots, DefenderBots) : TwoSides(DefenderBots, AttackerBots);

        protected override IReadOnlyList<ModeObstacle> Obstacles => PayloadObstacles;

        // GameRulesAssetBuilder.Build を先に回していなくても組めるよう、ルールが無ければここで作る (あれば調整した値を残す)。
        public static void Build()
        {
            GameRulesAssetBuilder.LoadOrBuildModeRules(GameRulesAssetBuilder.PayloadName);
            new PayloadSceneBuilder().BuildSceneFromMenu();
        }

        public static string RoutePointName(int index)
        {
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must not be negative");
            }

            return "RoutePoint-" + index;
        }

        public static PayloadRoute BuildRoute()
        {
            var points = new System.Numerics.Vector3[RoutePoints.Count];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = RoutePoints[i].ToNumerics();
            }

            return new PayloadRoute(points, CheckpointPoints);
        }

        public static List<Vector3> PatrolPoints(int botCount)
        {
            if (botCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(botCount), botCount, "botCount must not be negative");
            }

            var route = BuildRoute();
            var points = new List<Vector3>();
            for (var distance = 0d; ; distance += PatrolCheckStep)
            {
                var center = route.PositionAt(Math.Min(distance, route.Length)).ToUnity();
                points.Add(center);
                for (var slot = 0; slot < botCount; slot++)
                {
                    points.AddRange(HardpointRunner.RouteAround(center, CartZoneRadius, slot));
                }

                if (distance >= route.Length)
                {
                    return points;
                }
            }
        }

        protected override ModeRunnerBase AddRunner(GameObject host, ModeSceneContext context)
        {
            var runner = host.AddComponent<PayloadRunner>();
            runner.AttackerSideId = AttackerSide.Id;
            return runner;
        }

        // 始まった後は Runner が台車の周りの巡回路に替えるので、ここでは台車の始点へだけ向かわせる。
        protected override IReadOnlyList<Vector3> BotPatrolRoute(IReadOnlyList<ModeSideLayout> sides, int sideIndex, int spawnIndex)
        {
            return new[] { sides[sideIndex].Spawns[spawnIndex].Feet, RoutePoints[0] };
        }

        protected override void AddModeObjects(ModeSceneContext context)
        {
            if (!(context.Runner is PayloadRunner runner))
            {
                throw new InvalidOperationException($"{GetType().Name}.AddRunner must add a {nameof(PayloadRunner)}");
            }

            // 道筋の点の並びが PayloadRoute の決まりに合わなければ、何も置かずにここで止める。
            BuildRoute();

            var patrol = PatrolPoints(context.Bots.Count);
            SceneBuildHelpers.RequireOnNavMesh(patrol, NavMeshCheckRadius, "payload patrol point");
            SceneBuildHelpers.RequireReachable(PlayerFeet, patrol, NavMeshCheckRadius, "payload patrol point");

            var routeRoot = new GameObject(RouteRootName);
            SceneManager.MoveGameObjectToScene(routeRoot, context.Scene);

            // 道筋の点は台車の子にすると台車と一緒に動くので、台車の外に置く。
            var points = new Transform[RoutePoints.Count];
            for (var i = 0; i < points.Length; i++)
            {
                var point = new GameObject(RoutePointName(i));
                point.transform.SetParent(routeRoot.transform, false);
                point.transform.position = RoutePoints[i];
                points[i] = point.transform;
            }

            var cartObject = new GameObject(CartObjectName);
            SceneManager.MoveGameObjectToScene(cartObject, context.Scene);
            cartObject.transform.SetPositionAndRotation(
                RoutePoints[0], Quaternion.LookRotation(RoutePoints[1] - RoutePoints[0], Vector3.up));
            var cart = cartObject.AddComponent<PayloadCart>();
            cart.SetRoute(points, CheckpointPoints);

            var zoneObject = new GameObject(CartZoneName);
            zoneObject.transform.SetParent(cartObject.transform, false);
            var zone = zoneObject.AddComponent<ControlPointZone>();
            zone.Radius = CartZoneRadius;
            zone.Height = ControlPointZone.DefaultHeight;
            cart.SetZone(zone);

            runner.ConfiguredCart = cart;
        }
    }
}
