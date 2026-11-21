using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Editor
{
    // 置く物をコードの表でなくマップごとの JSON (NetMapFeatureBuilder.LayoutPath) に持つのは、ゲームがコードを触らずに置き直せるようにするため。
    public sealed class NetMapFeatureLayout
    {
        public const string FileExtension = ".features.json";

        public const string East = "east";
        public const string West = "west";
        public const string North = "north";
        public const string South = "south";

        public NetMapFeatureLayout(
            IReadOnlyList<NetMapLadderTower> ladderTowers, IReadOnlyList<NetMapWaterPool> waterPools, IReadOnlyList<NetMapVehicleSpot> vehicles)
        {
            LadderTowers = Copy(ladderTowers, nameof(ladderTowers));
            WaterPools = Copy(waterPools, nameof(waterPools));
            Vehicles = Copy(vehicles, nameof(vehicles));
        }

        public static IReadOnlyList<string> Sides { get; } = new[] { East, West, North, South };

        public IReadOnlyList<NetMapLadderTower> LadderTowers { get; }

        public IReadOnlyList<NetMapWaterPool> WaterPools { get; }

        public IReadOnlyList<NetMapVehicleSpot> Vehicles { get; }

        public int Count => LadderTowers.Count + WaterPools.Count + Vehicles.Count;

        public static bool TryGetSideDirection(string side, out Vector3 direction)
        {
            switch (side)
            {
                case East:
                    direction = Vector3.right;
                    return true;
                case West:
                    direction = Vector3.left;
                    return true;
                case North:
                    direction = Vector3.forward;
                    return true;
                case South:
                    direction = Vector3.back;
                    return true;
                default:
                    direction = Vector3.zero;
                    return false;
            }
        }

        // 読めない値を黙って既定に戻すと、置いたつもりの物が別の所に出るので、項目ごとの問題をまとめて断る。
        public static NetMapFeatureLayout Parse(string text, string sourceName)
        {
            var source = string.IsNullOrWhiteSpace(sourceName) ? "map features JSON" : sourceName;
            if (string.IsNullOrWhiteSpace(text))
            {
                throw new InvalidOperationException(source + " is empty");
            }

            LayoutJson json;
            try
            {
                json = JsonUtility.FromJson<LayoutJson>(text);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException(source + " is not valid JSON: " + e.Message, e);
            }

            if (json == null)
            {
                throw new InvalidOperationException(source + " has no object");
            }

            var problems = new List<string>();
            var towers = new List<NetMapLadderTower>();
            foreach (var (tower, index) in Indexed(json.ladderTowers))
            {
                var label = "ladderTowers[" + index + "]";
                if (tower == null)
                {
                    problems.Add(label + " is null");
                    continue;
                }

                var before = problems.Count;
                RequireName(problems, label, tower.name);
                RequirePositive(problems, label, "size", tower.size);
                RequireFinite(problems, label, "baseCenter", tower.baseCenter);
                RequirePositive(problems, label, "ladderWidth", tower.ladderWidth);
                if (!TryGetSideDirection(tower.side, out _))
                {
                    problems.Add(label + " has the side '" + tower.side + "'; use one of " + string.Join(", ", Sides));
                }
                else if (tower.ladderWidth > FaceWidth(tower.size, tower.side))
                {
                    problems.Add(label + " has a ladder (" + tower.ladderWidth + " m) wider than the " + tower.side + " face of the tower");
                }

                if (problems.Count == before)
                {
                    towers.Add(new NetMapLadderTower(tower.name, tower.baseCenter, tower.size, tower.side, tower.ladderWidth));
                }
            }

            var pools = new List<NetMapWaterPool>();
            foreach (var (pool, index) in Indexed(json.waterPools))
            {
                var label = "waterPools[" + index + "]";
                if (pool == null)
                {
                    problems.Add(label + " is null");
                    continue;
                }

                var before = problems.Count;
                RequireName(problems, label, pool.name);
                RequirePositive(problems, label, "size", pool.size);
                RequireFinite(problems, label, "groundCenter", pool.groundCenter);
                RequirePositive(problems, label, "surfaceHeight", pool.surfaceHeight);
                if (pool.surfaceHeight > pool.size.y)
                {
                    problems.Add(label + " has a surfaceHeight (" + pool.surfaceHeight + " m) above the top of its box (" + pool.size.y + " m)");
                }

                if (problems.Count == before)
                {
                    pools.Add(new NetMapWaterPool(pool.name, pool.groundCenter, pool.size, pool.surfaceHeight));
                }
            }

            var vehicles = new List<NetMapVehicleSpot>();
            foreach (var (vehicle, index) in Indexed(json.vehicles))
            {
                var label = "vehicles[" + index + "]";
                if (vehicle == null)
                {
                    problems.Add(label + " is null");
                    continue;
                }

                var before = problems.Count;
                RequireName(problems, label, vehicle.name);
                RequireFinite(problems, label, "position", vehicle.position);
                if (string.IsNullOrWhiteSpace(vehicle.kind))
                {
                    problems.Add(label + " has no kind");
                }

                if (!IsFinite(vehicle.yawDegrees))
                {
                    problems.Add(label + " has a yawDegrees that is not a number");
                }

                if (problems.Count == before)
                {
                    vehicles.Add(new NetMapVehicleSpot(vehicle.name, vehicle.kind, vehicle.position, vehicle.yawDegrees));
                }
            }

            if (problems.Count > 0)
            {
                throw new InvalidOperationException(source + " has invalid entries: " + string.Join("; ", problems));
            }

            return new NetMapFeatureLayout(towers, pools, vehicles);
        }

        public string ToJson()
        {
            var json = new LayoutJson();
            foreach (var tower in LadderTowers)
            {
                json.ladderTowers.Add(new LadderTowerJson
                {
                    name = tower.Name,
                    baseCenter = tower.BaseCenter,
                    size = tower.Size,
                    side = tower.Side,
                    ladderWidth = tower.LadderWidth,
                });
            }

            foreach (var pool in WaterPools)
            {
                json.waterPools.Add(new WaterPoolJson
                {
                    name = pool.Name,
                    groundCenter = pool.GroundCenter,
                    size = pool.Size,
                    surfaceHeight = pool.SurfaceHeight,
                });
            }

            foreach (var vehicle in Vehicles)
            {
                json.vehicles.Add(new VehicleJson
                {
                    name = vehicle.Name,
                    kind = vehicle.Kind,
                    position = vehicle.Position,
                    yawDegrees = vehicle.YawDegrees,
                });
            }

            return JsonUtility.ToJson(json, true) + "\n";
        }

        public static float FaceWidth(Vector3 size, string side)
        {
            return side == East || side == West ? size.z : size.x;
        }

        private static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> items, string parameterName)
        {
            if (items == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            return new List<T>(items).AsReadOnly();
        }

        private static IEnumerable<(T Item, int Index)> Indexed<T>(List<T> items)
        {
            if (items == null)
            {
                yield break;
            }

            for (var i = 0; i < items.Count; i++)
            {
                yield return (items[i], i);
            }
        }

        private static void RequireName(List<string> problems, string label, string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                problems.Add(label + " has no name");
            }
        }

        private static void RequirePositive(List<string> problems, string label, string item, Vector3 value)
        {
            if (!(value.x > 0f && value.y > 0f && value.z > 0f) || !IsFinite(value))
            {
                problems.Add(label + " has a " + item + " " + value + " that is not positive in every axis");
            }
        }

        private static void RequirePositive(List<string> problems, string label, string item, float value)
        {
            if (!(value > 0f) || !IsFinite(value))
            {
                problems.Add(label + " has a " + item + " " + value + " that is not positive");
            }
        }

        private static void RequireFinite(List<string> problems, string label, string item, Vector3 value)
        {
            if (!IsFinite(value))
            {
                problems.Add(label + " has a " + item + " " + value + " that is not a number");
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // JsonUtility は公開した欄の名前をそのまま JSON の項目名にするので、JSON の形だけを持つ型を分ける。
        [Serializable]
        private sealed class LayoutJson
        {
            public List<LadderTowerJson> ladderTowers = new List<LadderTowerJson>();
            public List<WaterPoolJson> waterPools = new List<WaterPoolJson>();
            public List<VehicleJson> vehicles = new List<VehicleJson>();
        }

        [Serializable]
        private sealed class LadderTowerJson
        {
            public string name;
            public Vector3 baseCenter;
            public Vector3 size;
            public string side;
            public float ladderWidth;
        }

        [Serializable]
        private sealed class WaterPoolJson
        {
            public string name;
            public Vector3 groundCenter;
            public Vector3 size;
            public float surfaceHeight;
        }

        [Serializable]
        private sealed class VehicleJson
        {
            public string name;
            public string kind;
            public Vector3 position;
            public float yawDegrees;
        }
    }
}
