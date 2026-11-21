using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using NumericsQuaternion = System.Numerics.Quaternion;
using NumericsVector3 = System.Numerics.Vector3;

namespace Shooter.Editor
{
    public static class MapVehicleExporter
    {
        public const float MinHalfExtent = 0.01f;

        // サーバーとクライアント (NetV2Vehicles) は同じ並びの番号で乗り物を指すので、数え方をそろえる。
        public static NetMapVehicles Collect(IReadOnlyList<GameObject> roots)
        {
            if (roots == null)
            {
                throw new ArgumentNullException(nameof(roots));
            }

            var vehicles = new List<MapVehicle>();
            var invalid = new List<string>();
            foreach (var root in roots)
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var vehicle in root.GetComponentsInChildren<Vehicle>(false))
                {
                    if (!IsExported(vehicle))
                    {
                        continue;
                    }

                    try
                    {
                        vehicles.Add(ToMapVehicle(vehicle));
                    }
                    catch (Exception e) when (e is ArgumentException || e is InvalidOperationException)
                    {
                        invalid.Add(vehicle.name + " (" + e.Message + ")");
                    }
                }
            }

            // 書き出さないと、クライアントの並びとサーバーの並びの番号がずれて別の乗り物を指してしまう
            if (invalid.Count > 0)
            {
                throw new InvalidOperationException(MapCollisionExporter.LogTag + " these vehicles cannot be exported: " + string.Join(", ", invalid));
            }

            return new NetMapVehicles(vehicles);
        }

        public static bool IsExported(Vehicle vehicle)
        {
            return vehicle != null && vehicle.enabled && vehicle.gameObject.activeInHierarchy;
        }

        public static MapVehicle ToMapVehicle(Vehicle vehicle)
        {
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            var root = vehicle.transform;
            var spec = vehicle.Spec;
            var seats = new List<MapVehicleSeat>();
            foreach (var seat in vehicle.Seats)
            {
                var yaw = Mathf.DeltaAngle(root.eulerAngles.y, seat.SitYawDegrees);
                seats.Add(new MapVehicleSeat(Local(root, seat.SitPosition), yaw, Points(root, seat.EnterPoints), Points(root, seat.ExitPoints)));
            }

            var weapons = new List<WeaponSpec>();
            foreach (var mount in vehicle.WeaponMounts)
            {
                weapons.Add(mount.WeaponSpec ?? throw new InvalidOperationException("weapon mount " + mount.name + " has no weapon"));
            }

            var drive = vehicle.TryGetComponent<IVehicleTopSpeed>(out var top) ? VehicleDriveSpec.FromTopSpeed(top.TopSpeed) : VehicleDriveSpec.Default;
            return new MapVehicle(
                spec,
                ToNumerics(root.position),
                ToNumerics(root.rotation),
                seats,
                Hull(vehicle),
                Points(root, vehicle.ExitPoints),
                weapons,
                drive,
                Countermeasures(vehicle));
        }

        // 切ってある VehicleCountermeasures はオフラインでも撒かないので、書き出さずにサーバーでも積んでいないことにする。
        // 設定が使えない値なら ToSpec が投げ、Collect がこの乗り物を書き出せない一覧に載せる
        private static MapVehicleCountermeasures Countermeasures(Vehicle vehicle)
        {
            var flares = vehicle.GetComponent<VehicleCountermeasures>();
            if (flares == null || !flares.enabled)
            {
                return null;
            }

            var settings = flares.Settings;
            return new MapVehicleCountermeasures(
                settings.ToSpec(), ToNumerics(settings.EjectLocalOffset), ToNumerics(settings.EjectLocalDirection));
        }

        // 箱でない当たり判定は、乗り物から見た向きの外接箱で近づける。サーバーの車体は箱の集まりだけで当てるため。
        private static List<MapVehicleHullBox> Hull(Vehicle vehicle)
        {
            var root = vehicle.transform;
            var inverse = Quaternion.Inverse(root.rotation);
            var hull = new List<MapVehicleHullBox>();
            foreach (var collider in vehicle.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger || collider is WheelCollider
                    || collider.GetComponentInParent<Vehicle>(true) != vehicle)
                {
                    continue;
                }

                var part = collider.GetComponentInParent<VehiclePart>(true);
                var partId = part != null && part.GetComponentInParent<Vehicle>(true) == vehicle ? part.PartId : null;
                if (collider is BoxCollider box)
                {
                    var transform = box.transform;
                    var center = transform.TransformPoint(box.center);
                    var half = Thick(Abs(Vector3.Scale(box.size, transform.lossyScale)) * 0.5f);
                    hull.Add(new MapVehicleHullBox(
                        Local(root, center), ToNumerics(half), ToNumerics(inverse * transform.rotation), partId));
                    continue;
                }

                var bounds = collider.bounds;
                hull.Add(new MapVehicleHullBox(Local(root, bounds.center), ToNumerics(Thick(bounds.extents)), ToNumerics(inverse), partId));
            }

            return hull;
        }

        private static NumericsVector3[] Points(Transform root, IReadOnlyList<Transform> points)
        {
            var result = new List<NumericsVector3>();
            if (points == null)
            {
                return result.ToArray();
            }

            foreach (var point in points)
            {
                if (point != null)
                {
                    result.Add(Local(root, point.position));
                }
            }

            return result.ToArray();
        }

        // サーバーは乗り物の縮尺を掛けないので、位置と大きさはどれもワールドの長さで書く
        private static NumericsVector3 Local(Transform root, Vector3 world)
        {
            return ToNumerics(Quaternion.Inverse(root.rotation) * (world - root.position));
        }

        // 厚さの無い板の当たり判定も、サーバーの箱は厚さを要るので薄い箱にする
        private static Vector3 Thick(Vector3 half)
        {
            return Vector3.Max(half, Vector3.one * MinHalfExtent);
        }

        private static Vector3 Abs(Vector3 v)
        {
            return new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        }

        private static NumericsVector3 ToNumerics(Vector3 v)
        {
            return new NumericsVector3(v.x, v.y, v.z);
        }

        private static NumericsQuaternion ToNumerics(Quaternion q)
        {
            return new NumericsQuaternion(q.x, q.y, q.z, q.w);
        }
    }
}
