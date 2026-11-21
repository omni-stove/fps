using System;
using System.Numerics;

namespace Shooter.Core
{
    // Unity 抜きのサーバーは乗り物の VehicleCountermeasures を読めないので、撒く数と効き目 (Spec) に撒く所と向きを添えて持つ。
    public sealed class MapVehicleCountermeasures
    {
        public static readonly Vector3 DefaultEjectOffset = new Vector3(0f, -0.5f, -2f);

        public static readonly Vector3 DefaultEjectDirection = new Vector3(0f, -1f, -1f);

        private const float _minLength = 1e-4f;

        public MapVehicleCountermeasures(CountermeasureSpec spec, Vector3 ejectOffset, Vector3 ejectDirection)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
            EjectOffset = MapVehicleSeat.RequireFinite(ejectOffset, nameof(ejectOffset));
            EjectDirection = MapVehicleSeat.RequireFinite(ejectDirection, nameof(ejectDirection));
        }

        public static MapVehicleCountermeasures Default =>
            new MapVehicleCountermeasures(CountermeasureSpec.Default, DefaultEjectOffset, DefaultEjectDirection);

        public CountermeasureSpec Spec { get; }

        // 乗り物の原点から見た撒く所 (m)。
        public Vector3 EjectOffset { get; }

        // 乗り物から見た撒く向き。長さは問わず、0 なら真下 (Unity の VehicleCountermeasures と同じ)。
        public Vector3 EjectDirection { get; }

        public Vector3 EjectPoint(Vector3 position, Quaternion rotation)
        {
            return MapVehicle.ToWorld(EjectOffset, position, rotation);
        }

        public Vector3 EjectVelocity(Quaternion rotation, Vector3 bodyVelocity)
        {
            var direction = EjectDirection.LengthSquared() > _minLength * _minLength
                ? Vector3.Transform(Vector3.Normalize(EjectDirection), rotation)
                : -Vector3.UnitY;
            return bodyVelocity + (direction * Spec.EjectSpeed);
        }
    }
}
