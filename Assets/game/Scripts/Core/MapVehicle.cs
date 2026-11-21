using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーはシーンの乗り物を読めないので、設定 (VehicleSpec) に置き方・座席の位置・車体の形・座席の武器を添えて持つ。
    public sealed class MapVehicle
    {
        public MapVehicle(
            VehicleSpec spec,
            Vector3 position,
            Quaternion rotation,
            IReadOnlyList<MapVehicleSeat> seats,
            IReadOnlyList<MapVehicleHullBox> hull = null,
            IReadOnlyList<Vector3> exitPoints = null,
            IReadOnlyList<WeaponSpec> weapons = null,
            VehicleDriveSpec drive = null,
            MapVehicleCountermeasures countermeasures = null)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            Position = MapVehicleSeat.RequireFinite(position, nameof(position));
            Rotation = Normalize(rotation, nameof(rotation));
            if (seats == null)
            {
                throw new ArgumentNullException(nameof(seats));
            }

            if (seats.Count != spec.Seats.Count)
            {
                throw new ArgumentException(
                    "the vehicle has " + seats.Count + " seat positions but its spec has " + spec.Seats.Count + " seats", nameof(seats));
            }

            Seats = CopyAll(seats, nameof(seats));
            Hull = hull == null ? Array.Empty<MapVehicleHullBox>() : CopyAll(hull, nameof(hull));
            ExitPoints = MapVehicleSeat.Copy(exitPoints, nameof(exitPoints));
            Weapons = CopyWeapons(spec, weapons);
            Drive = drive ?? VehicleDriveSpec.Default;
            Countermeasures = countermeasures;
        }

        public VehicleSpec Spec { get; }

        public Vector3 Position { get; }

        public Quaternion Rotation { get; }

        public IReadOnlyList<MapVehicleSeat> Seats { get; }

        public IReadOnlyList<MapVehicleHullBox> Hull { get; }

        public IReadOnlyList<Vector3> ExitPoints { get; }

        public IReadOnlyList<WeaponSpec> Weapons { get; }

        public VehicleDriveSpec Drive { get; }

        // フレアを積んでいない乗り物は null。
        public MapVehicleCountermeasures Countermeasures { get; }

        public static Vector3 ToWorld(Vector3 local, Vector3 position, Quaternion rotation)
        {
            return position + Vector3.Transform(local, rotation);
        }

        // 座る位置は足元なので、体の原点 (カプセルの寸法の原点) はその姿勢の足元の高さだけ上にある。
        public static Vector3 RiderOrigin(Vector3 seatFeet, StanceGeometry geometry, bool crouched)
        {
            if (geometry == null)
            {
                throw new ArgumentNullException(nameof(geometry));
            }

            return seatFeet - new Vector3(0f, geometry.Shape(crouched).BottomY, 0f);
        }

        public Vector3 SeatFeet(int seat, Vector3 position, Quaternion rotation)
        {
            return ToWorld(Seats[seat].Sit, position, rotation);
        }

        public float DistanceToEnter(int seat, Vector3 position, Quaternion rotation, Vector3 from)
        {
            var points = Seats[seat].EnterPoints;
            if (points.Count == 0)
            {
                return Vector3.Distance(from, SeatFeet(seat, position, rotation));
            }

            var best = float.PositiveInfinity;
            foreach (var point in points)
            {
                best = Math.Min(best, Vector3.Distance(from, ToWorld(point, position, rotation)));
            }

            return best;
        }

        public IReadOnlyList<Vector3> ExitsOf(int seat)
        {
            var own = Seats[seat].ExitPoints;
            return own.Count > 0 ? own : ExitPoints;
        }

        public Vector3 Center(Vector3 position, Quaternion rotation)
        {
            if (Hull.Count == 0)
            {
                return position;
            }

            var sum = Vector3.Zero;
            foreach (var box in Hull)
            {
                sum += box.Center;
            }

            return ToWorld(sum / Hull.Count, position, rotation);
        }

        public float PartMultiplier(int box)
        {
            var partId = Hull[box].PartId;
            var part = partId == null ? null : Spec.FindPart(partId);
            return part?.DamageMultiplier ?? 1f;
        }

        // 中から出る線は当たりにしない (Unity の Raycast と同じ)。座席の武器の銃口が車体の箱の中にあっても自分の車体に当てないため。
        public bool RaycastHull(
            Vector3 position, Quaternion rotation, Vector3 origin, Vector3 unit, float maxDistance, out float distance, out Vector3 normal,
            out int box)
        {
            distance = float.PositiveInfinity;
            normal = default;
            box = -1;
            for (var i = 0; i < Hull.Count; i++)
            {
                var shape = Hull[i];
                var boxRotation = rotation * shape.Rotation;
                var inverse = Quaternion.Inverse(boxRotation);
                var center = ToWorld(shape.Center, position, rotation);
                var localOrigin = Vector3.Transform(origin - center, inverse);
                var localDirection = Vector3.Transform(unit, inverse);
                if (!WorldGeometry.RayBox(localOrigin, localDirection, shape.HalfExtents, out var hit, out var localNormal)
                    || hit > maxDistance || hit >= distance)
                {
                    continue;
                }

                distance = hit;
                normal = Vector3.Transform(localNormal, boxRotation);
                box = i;
            }

            return box >= 0;
        }

        public float DistanceToHull(Vector3 position, Quaternion rotation, Vector3 point)
        {
            if (Hull.Count == 0)
            {
                return Vector3.Distance(point, position);
            }

            var best = float.PositiveInfinity;
            foreach (var shape in Hull)
            {
                var boxRotation = rotation * shape.Rotation;
                var center = ToWorld(shape.Center, position, rotation);
                var local = Vector3.Transform(point - center, Quaternion.Inverse(boxRotation));
                var clamped = Vector3.Clamp(local, -shape.HalfExtents, shape.HalfExtents);
                best = Math.Min(best, Vector3.Distance(local, clamped));
            }

            return best;
        }

        internal static Quaternion Normalize(Quaternion rotation, string paramName)
        {
            var length = rotation.Length();
            if (!(length > 1e-6f) || !float.IsFinite(length))
            {
                throw new ArgumentException(paramName + " must be a finite rotation that is not zero", paramName);
            }

            return Quaternion.Normalize(rotation);
        }

        private static T[] CopyAll<T>(IReadOnlyList<T> items, string paramName)
            where T : class
        {
            var copy = new T[items.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = items[i] ?? throw new ArgumentException(paramName + "[" + i + "] is null", paramName);
            }

            return copy;
        }

        private static WeaponSpec[] CopyWeapons(VehicleSpec spec, IReadOnlyList<WeaponSpec> weapons)
        {
            var count = spec.Weapons.Count;
            if ((weapons?.Count ?? 0) != count)
            {
                throw new ArgumentException(
                    "the spec has " + count + " weapons but " + (weapons?.Count ?? 0) + " weapon specs were given", nameof(weapons));
            }

            var copy = new WeaponSpec[count];
            for (var i = 0; i < count; i++)
            {
                var weapon = weapons[i] ?? throw new ArgumentException("weapons[" + i + "] is null", nameof(weapons));
                if (!string.Equals(weapon.Id, spec.Weapons[i].WeaponId, StringComparison.Ordinal))
                {
                    throw new ArgumentException(
                        "weapons[" + i + "] is " + weapon.Id + " but the spec mounts " + spec.Weapons[i].WeaponId, nameof(weapons));
                }

                copy[i] = weapon;
            }

            return copy;
        }
    }
}
