using System;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public static class CarFixtureBuilder
    {
        public const string DefaultName = "Car";
        public const string VehicleId = "car";
        public const string BodyName = "Body";
        public const string DriverSeatName = "Seat_Driver";
        public const string PassengerSeatName = "Seat_Passenger";
        public const string GunnerSeatName = "Seat_Gunner";
        public const string GunnerVehicleId = "car-gunner";
        public const string GunName = "Gun";
        public const string MuzzleName = "Muzzle";
        public const string BarrelName = "Barrel";

        public const float GunPitchMinDegrees = -10f;
        public const float GunPitchMaxDegrees = 30f;
        public const float GunTurnSpeedDegreesPerSecond = 120f;

        public const float WheelRadius = 0.4f;
        public const float WheelWidth = 0.3f;
        public const float WheelMass = 20f;
        public const float SuspensionDistance = 0.3f;

        public static readonly Vector3 BodySize = new Vector3(1.8f, 0.8f, 4f);
        public static readonly Vector3 BodyCenter = new Vector3(0f, 0.9f, 0f);

        // 重心を低くするのは、曲がるたびに横転しないようにするため。
        public static readonly Vector3 CenterOfMass = new Vector3(0f, 0.4f, 0f);

        public static readonly Vector3 FrontLeftWheel = new Vector3(-0.85f, 0.6f, 1.3f);
        public static readonly Vector3 FrontRightWheel = new Vector3(0.85f, 0.6f, 1.3f);
        public static readonly Vector3 RearLeftWheel = new Vector3(-0.85f, 0.6f, -1.3f);
        public static readonly Vector3 RearRightWheel = new Vector3(0.85f, 0.6f, -1.3f);

        public static readonly Vector3 DriverSeatLocal = new Vector3(-0.4f, 0.7f, 0.2f);
        public static readonly Vector3 PassengerSeatLocal = new Vector3(0.4f, 0.7f, 0.2f);
        public static readonly Vector3 DriverEnterLocal = new Vector3(-1.4f, 0f, 0.2f);
        public static readonly Vector3 PassengerEnterLocal = new Vector3(1.4f, 0f, 0.2f);
        public static readonly Vector3 LeftExitLocal = new Vector3(-2f, 0f, 0.2f);
        public static readonly Vector3 RightExitLocal = new Vector3(2f, 0f, 0.2f);
        public static readonly Vector3 RearExitLocal = new Vector3(0f, 0f, -3f);

        public static readonly Vector3 GunnerSeatLocal = new Vector3(0f, BodyCenter.y + (BodySize.y / 2f), -1.2f);
        public static readonly Vector3 GunnerEnterLocal = new Vector3(0f, 0f, -2.6f);
        public static readonly Vector3 GunLocal = new Vector3(0f, GunnerSeatLocal.y + 1f, -0.6f);
        public static readonly Vector3 MuzzleLocal = new Vector3(0f, 0f, 1f);

        public const float EnterSeconds = 1f;

        public const float ExitSeconds = 0.6f;

        public static VehicleSpec Spec(string gunnerWeaponId = null)
        {
            if (gunnerWeaponId == null)
            {
                return new VehicleSpec(
                    VehicleId,
                    VehicleSpec.DefaultMaxHealth,
                    new[]
                    {
                        new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Seated, VehicleCameraView.Chase),
                        new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Chase),
                    },
                    enterSeconds: EnterSeconds,
                    exitSeconds: ExitSeconds);
            }

            return new VehicleSpec(
                GunnerVehicleId,
                VehicleSpec.DefaultMaxHealth,
                new[]
                {
                    new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Seated, VehicleCameraView.Chase),
                    new VehicleSeatSpec(VehicleSeatRole.Passenger, VehicleSeatPose.Seated, VehicleCameraView.Chase),
                    new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Standing, VehicleCameraView.Chase),
                },
                new[]
                {
                    new VehicleWeaponSpec(
                        gunnerWeaponId,
                        2,
                        VehicleWeaponSpec.DefaultYawMinDegrees,
                        VehicleWeaponSpec.DefaultYawMaxDegrees,
                        GunPitchMinDegrees,
                        GunPitchMaxDegrees,
                        GunTurnSpeedDegreesPerSecond),
                },
                enterSeconds: EnterSeconds,
                exitSeconds: ExitSeconds);
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

                // WheelCollider は親の Rigidbody に付くので、車輪より先に作る。
                var body = root.AddComponent<Rigidbody>();
                body.mass = CarSpec.DefaultMass;
                body.automaticCenterOfMass = false;
                body.centerOfMass = CenterOfMass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                var shell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shell.name = BodyName;
                shell.transform.SetParent(root.transform, false);
                shell.transform.localPosition = BodyCenter;
                shell.transform.localScale = BodySize;

                var wheels = new[]
                {
                    CreateWheel(root.transform, "Wheel_FL", FrontLeftWheel, steers: true, drives: false),
                    CreateWheel(root.transform, "Wheel_FR", FrontRightWheel, steers: true, drives: false),
                    CreateWheel(root.transform, "Wheel_RL", RearLeftWheel, steers: false, drives: true),
                    CreateWheel(root.transform, "Wheel_RR", RearRightWheel, steers: false, drives: true),
                };

                var leftExit = CreatePoint(root.transform, "Exit_Left", LeftExitLocal);
                var rightExit = CreatePoint(root.transform, "Exit_Right", RightExitLocal);
                var rearExit = CreatePoint(root.transform, "Exit_Rear", RearExitLocal);

                // 子の並び順が座席の番号になるので、運転席・助手席の順に作る。
                var driver = CreateSeat(root.transform, DriverSeatName, DriverSeatLocal);
                driver.SetEnterPoints(CreatePoint(root.transform, "Enter_Driver", DriverEnterLocal));
                driver.SetExitPoints(leftExit, rightExit);

                var passenger = CreateSeat(root.transform, PassengerSeatName, PassengerSeatLocal);
                passenger.SetEnterPoints(CreatePoint(root.transform, "Enter_Passenger", PassengerEnterLocal));
                passenger.SetExitPoints(rightExit, leftExit);

                if (gunnerWeapon != null)
                {
                    var gunner = CreateSeat(root.transform, GunnerSeatName, GunnerSeatLocal);
                    gunner.SetEnterPoints(CreatePoint(root.transform, "Enter_Gunner", GunnerEnterLocal));
                    CreateGun(root.transform, gunnerWeapon);
                }

                var vehicle = root.AddComponent<Vehicle>();
                vehicle.SetExitPoints(rearExit);
                vehicle.RefreshSeats();
                vehicle.Configure(Spec(gunnerWeapon != null ? gunnerWeapon.Id : null));

                var motor = root.AddComponent<SimpleCarMotor>();
                motor.SetWheels(wheels);
                motor.Configure(CarSpec.Default);
                root.AddComponent<VehicleMotorLink>();
                root.AddComponent<VehicleImpactDamage>().Configure(VehicleImpactSpec.Default);
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
            var center = origin + (rotation * BodyCenter);
            var corners = new Bounds(center, Vector3.zero);
            for (var i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? -BodySize.x / 2f : BodySize.x / 2f,
                    (i & 2) == 0 ? -BodySize.y / 2f : BodySize.y / 2f,
                    (i & 4) == 0 ? -BodySize.z / 2f : BodySize.z / 2f);
                corners.Encapsulate(center + (rotation * corner));
            }

            corners.Encapsulate(new Vector3(corners.center.x, origin.y, corners.center.z));
            return corners;
        }

        private static CarWheel CreateWheel(Transform parent, string name, Vector3 localPosition, bool steers, bool drives)
        {
            var colliderObject = new GameObject(name);
            colliderObject.transform.SetParent(parent, false);
            colliderObject.transform.localPosition = localPosition;
            var collider = colliderObject.AddComponent<WheelCollider>();
            collider.radius = WheelRadius;
            collider.mass = WheelMass;
            collider.suspensionDistance = SuspensionDistance;

            // 見た目は WheelCollider と別の GameObject にする。回りと向きを毎フレーム写すので、WheelCollider の子にすると親の動きと二重に掛かるため。
            var visual = new GameObject(name + "_Visual").transform;
            visual.SetParent(parent, false);
            visual.localPosition = localPosition;

            var tire = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tire.name = "Tire";

            // 車輪の見た目が地面や車体とぶつかると WheelCollider の接地を邪魔するので、形だけ残して当たり判定は外す。
            UnityEngine.Object.DestroyImmediate(tire.GetComponent<Collider>());
            tire.transform.SetParent(visual, false);
            tire.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            tire.transform.localScale = new Vector3(WheelRadius * 2f, WheelWidth / 2f, WheelRadius * 2f);
            return new CarWheel(collider, visual, steers, drives);
        }

        // 銃の見た目は車体の箱と同じく試すための目印なので、差し替える参照は持たない。屋根の上の乗り手や弾を遮らないよう当たり判定は外す。
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
