using System;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public static class VehicleFixtureBuilder
    {
        public const string DefaultName = "ParkedVehicle";
        public const string BodyName = "Body";
        public const string DriverSeatName = "Seat_Driver";
        public const string PassengerSeatName = "Seat_Passenger";
        public const string GunnerSeatName = "Seat_Gunner";
        public const string PassengerViewName = "View";
        public const string GunName = "Gun";
        public const string MuzzleName = "Muzzle";
        public const string BarrelName = "Barrel";

        public const float PassengerArcDegrees = 180f;
        public const float GunPitchMinDegrees = -10f;
        public const float GunPitchMaxDegrees = 30f;
        public const float GunTurnSpeedDegreesPerSecond = 120f;

        public static readonly Vector3 BodySize = new Vector3(2f, 1.2f, 4f);

        public static readonly Vector3 DriverSeatLocal = new Vector3(-0.5f, 0.4f, 0.4f);
        public static readonly Vector3 PassengerSeatLocal = new Vector3(0.5f, 0.4f, 0.4f);
        public static readonly Vector3 GunnerSeatLocal = new Vector3(0f, 1.2f, -1.2f);

        public static readonly Vector3 DriverEnterLocal = new Vector3(-1.3f, 0f, 0.4f);
        public static readonly Vector3 PassengerEnterLocal = new Vector3(1.3f, 0f, 0.4f);
        public static readonly Vector3 GunnerEnterLocal = new Vector3(0f, 0f, -2.3f);

        public static readonly Vector3 LeftExitLocal = new Vector3(-2f, 0f, 0.4f);

        public static readonly Vector3 RightExitLocal = new Vector3(2f, 0f, 0.4f);

        public static readonly Vector3 RearExitLocal = new Vector3(0f, 0f, -3f);

        public static readonly Vector3 PassengerViewLocal = new Vector3(0f, 0.9f, 0f);

        public static readonly Vector3 GunLocal = new Vector3(0f, 2.2f, -0.6f);

        public static readonly Vector3 MuzzleLocal = new Vector3(0f, 0f, 1f);

        public static VehicleSpec Spec(string gunnerWeaponId = null)
        {
            return new VehicleSpec(
                DefaultName,
                VehicleSpec.DefaultMaxHealth,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Seated, VehicleCameraView.Chase),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Seat, PassengerArcDegrees),
                    new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Standing, VehicleCameraView.Chase),
                },
                gunnerWeaponId != null
                    ? new[]
                    {
                        new VehicleWeaponSpec(
                            gunnerWeaponId,
                            2,
                            VehicleWeaponSpec.DefaultYawMinDegrees,
                            VehicleWeaponSpec.DefaultYawMaxDegrees,
                            GunPitchMinDegrees,
                            GunPitchMaxDegrees,
                            GunTurnSpeedDegreesPerSecond),
                    }
                    : null);
        }

        public static Vehicle Create(
            Scene scene, Transform parent, string name, Vector3 position, Quaternion rotation, WeaponDefinition gunnerWeapon = null)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a loaded scene", nameof(scene));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name must not be empty", nameof(name));
            }

            var root = new GameObject(name);
            try
            {
                if (parent != null)
                {
                    root.transform.SetParent(parent, false);
                }
                else
                {
                    SceneManager.MoveGameObjectToScene(root, scene);
                }

                root.transform.SetPositionAndRotation(position, rotation);

                var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                body.name = BodyName;
                body.transform.SetParent(root.transform, false);
                body.transform.localPosition = new Vector3(0f, BodySize.y / 2f, 0f);
                body.transform.localScale = BodySize;

                var leftExit = CreatePoint(root.transform, "Exit_Left", LeftExitLocal);
                var rightExit = CreatePoint(root.transform, "Exit_Right", RightExitLocal);
                var rearExit = CreatePoint(root.transform, "Exit_Rear", RearExitLocal);

                // 子の並び順が座席の番号になるので、運転席・助手席・銃座の順に作る。
                var driver = CreateSeat(root.transform, DriverSeatName, DriverSeatLocal);
                driver.SetEnterPoints(CreatePoint(root.transform, "Enter_Driver", DriverEnterLocal));
                driver.SetExitPoints(leftExit, rightExit);

                var passenger = CreateSeat(root.transform, PassengerSeatName, PassengerSeatLocal);
                passenger.SetEnterPoints(CreatePoint(root.transform, "Enter_Passenger", PassengerEnterLocal));
                passenger.SetExitPoints(rightExit, leftExit);
                passenger.SetViewPoint(CreatePoint(passenger.transform, PassengerViewName, PassengerViewLocal));

                var gunner = CreateSeat(root.transform, GunnerSeatName, GunnerSeatLocal);
                gunner.SetEnterPoints(CreatePoint(root.transform, "Enter_Gunner", GunnerEnterLocal));

                if (gunnerWeapon != null)
                {
                    CreateGun(root.transform, gunnerWeapon);
                }

                var vehicle = root.AddComponent<Vehicle>();
                vehicle.SetExitPoints(rearExit);
                vehicle.RefreshSeats();
                vehicle.Configure(Spec(gunnerWeapon != null ? gunnerWeapon.Id : null));
                return vehicle;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        public static Bounds BodyBounds(Vector3 origin, Quaternion rotation)
        {
            var center = origin + (rotation * new Vector3(0f, BodySize.y / 2f, 0f));
            var corners = new Bounds(center, Vector3.zero);
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -BodySize.x / 2f : BodySize.x / 2f,
                    (i & 2) == 0 ? -BodySize.y / 2f : BodySize.y / 2f,
                    (i & 4) == 0 ? -BodySize.z / 2f : BodySize.z / 2f);
                corners.Encapsulate(center + (rotation * corner));
            }

            return corners;
        }

        // 銃の見た目は仮の乗り物の箱と同じく試すための目印なので、差し替える参照は持たない。弾を遮らないよう当たり判定は外す。
        private static VehicleWeaponMount CreateGun(Transform parent, WeaponDefinition weapon)
        {
            var gun = new GameObject(GunName).AddComponent<VehicleWeaponMount>();
            gun.transform.SetParent(parent, false);
            gun.transform.localPosition = GunLocal;
            gun.transform.localRotation = Quaternion.identity;
            gun.SetWeapon(weapon);
            gun.SetMuzzle(CreatePoint(gun.transform, MuzzleName, MuzzleLocal));

            var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            barrel.name = BarrelName;
            UnityEngine.Object.DestroyImmediate(barrel.GetComponent<Collider>());
            barrel.transform.SetParent(gun.transform, false);
            barrel.transform.localPosition = new Vector3(0f, 0f, MuzzleLocal.z / 2f);
            barrel.transform.localScale = new Vector3(0.12f, 0.12f, MuzzleLocal.z);
            return gun;
        }

        private static VehicleSeat CreateSeat(Transform parent, string name, Vector3 localPosition)
        {
            var seat = new GameObject(name).AddComponent<VehicleSeat>();
            seat.transform.SetParent(parent, false);
            seat.transform.localPosition = localPosition;
            seat.transform.localRotation = Quaternion.identity;
            return seat;
        }

        private static Transform CreatePoint(Transform parent, string name, Vector3 localPosition)
        {
            var point = new GameObject(name).transform;
            point.SetParent(parent, false);
            point.localPosition = localPosition;
            point.localRotation = Quaternion.identity;
            return point;
        }
    }
}
