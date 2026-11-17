using System.Numerics;

namespace Fps.Core.Tests
{
    // 運転席・助手席・銃座の 3 座席で、車体が長さ 4m の箱 1 つと、前に付いたエンジン (倍率 2) の箱を持つ車
    internal static class TestVehicles
    {
        public const string JeepId = "jeep";
        public const string GunId = "c3d1a0e5-6f2b-4b7d-9e1a-5a8c4d2f7b90";

        public static WeaponSpec Gun() => new WeaponSpec(GunId, "weapon.mounted", WeaponCategory.AssaultRifle, impact: new ImpactSpec(30));

        public static VehicleSpec Spec(int maxHealth = 1000)
        {
            return new VehicleSpec(
                JeepId,
                maxHealth,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger, ownWeaponArcDegrees: 180f),
                    new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Standing),
                },
                new[] { new VehicleWeaponSpec(GunId, 2) },
                new[] { new VehiclePartSpec("engine", 2f) });
        }

        public static MapVehicle Jeep(Vector3 position, Quaternion rotation, int maxHealth = 1000, VehicleDriveSpec drive = null)
        {
            return new MapVehicle(
                Spec(maxHealth),
                position,
                rotation,
                new[]
                {
                    new MapVehicleSeat(new Vector3(-0.5f, 0.5f, 0.3f), 0f, new[] { new Vector3(-1.5f, 0f, 0.3f) }, new[] { new Vector3(-2f, 0f, 0.3f) }),
                    new MapVehicleSeat(new Vector3(0.5f, 0.5f, 0.3f), 90f, new[] { new Vector3(1.5f, 0f, 0.3f) }),
                    new MapVehicleSeat(new Vector3(0f, 1.2f, -1.2f)),
                },
                new[]
                {
                    new MapVehicleHullBox(new Vector3(0f, 0.5f, 0f), new Vector3(1f, 0.3f, 2f), Quaternion.Identity),
                    new MapVehicleHullBox(new Vector3(0f, 1f, 1.5f), new Vector3(0.5f, 0.2f, 0.5f), Quaternion.Identity, "engine"),
                },
                new[] { new Vector3(3f, 0f, 0f) },
                new[] { Gun() },
                drive);
        }
    }
}
