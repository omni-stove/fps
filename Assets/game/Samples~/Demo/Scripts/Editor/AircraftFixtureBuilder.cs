using System;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    public static class AircraftFixtureBuilder
    {
        public const string DefaultName = "Aircraft";
        public const string VehicleId = "aircraft";
        public const string FuselageName = "Fuselage";
        public const string WingName = "Wing";
        public const string TailplaneName = "Tailplane";
        public const string FinName = "Fin";
        public const string PilotSeatName = "Seat_Pilot";

        public const float ChaseDistance = 14f;
        public const float ChaseHeight = 4f;

        public const float WheelRadius = 0.4f;
        public const float WheelMass = 40f;
        public const float SuspensionDistance = 0.3f;

        // 既定のばねは車の重さ向けで、飛行機の重さでは縮み切って胴が地面に着くので、重さに合わせて固くする。
        public const float SuspensionSpring = 200000f;
        public const float SuspensionDamper = 20000f;

        public static readonly Vector3 FuselageSize = new Vector3(1.2f, 1.2f, 8f);
        public static readonly Vector3 FuselageCenter = new Vector3(0f, 1.3f, 0f);
        public static readonly Vector3 WingSize = new Vector3(9f, 0.2f, 2f);
        public static readonly Vector3 WingCenter = new Vector3(0f, 1.3f, 0.3f);
        public static readonly Vector3 TailplaneSize = new Vector3(3f, 0.15f, 1f);
        public static readonly Vector3 TailplaneCenter = new Vector3(0f, 1.6f, -3.5f);
        public static readonly Vector3 FinSize = new Vector3(0.2f, 1.4f, 1.2f);
        public static readonly Vector3 FinCenter = new Vector3(0f, 2.5f, -3.4f);

        // 重心を翼の位置に置くのは、機首と尾のどちらかへ傾いて、舵を当てなくても回り出さないようにするため。
        public static readonly Vector3 CenterOfMass = new Vector3(0f, 1.2f, 0.3f);

        public static readonly Vector3 NoseWheel = new Vector3(0f, 0.6f, 3f);
        public static readonly Vector3 LeftMainWheel = new Vector3(-1.2f, 0.6f, -0.5f);
        public static readonly Vector3 RightMainWheel = new Vector3(1.2f, 0.6f, -0.5f);

        public static readonly Vector3 PilotSeatLocal = new Vector3(0f, 1.6f, 1.6f);
        public static readonly Vector3 PilotEnterLocal = new Vector3(-1.5f, 0f, 3f);
        public static readonly Vector3 LeftExitLocal = new Vector3(-2f, 0f, 3.2f);
        public static readonly Vector3 RightExitLocal = new Vector3(2f, 0f, 3.2f);
        public static readonly Vector3 RearExitLocal = new Vector3(0f, 0f, -6f);

        public static VehicleSpec Spec()
        {
            return new VehicleSpec(
                VehicleId,
                VehicleSpec.DefaultMaxHealth,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Driver, VehicleSeatPose.Seated, VehicleCameraView.Chase) },
                chaseDistance: ChaseDistance,
                chaseHeight: ChaseHeight);
        }

        public static Vehicle Create(Scene scene, Transform parent, string name, Vector3 position, Quaternion rotation)
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
                body.mass = AircraftSpec.DefaultMass;
                body.automaticCenterOfMass = false;
                body.centerOfMass = CenterOfMass;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                CreateBox(root.transform, FuselageName, FuselageCenter, FuselageSize);
                CreateBox(root.transform, WingName, WingCenter, WingSize);
                CreateBox(root.transform, TailplaneName, TailplaneCenter, TailplaneSize);
                CreateBox(root.transform, FinName, FinCenter, FinSize);
                CreateWheel(root.transform, "Wheel_Nose", NoseWheel);
                CreateWheel(root.transform, "Wheel_Left", LeftMainWheel);
                CreateWheel(root.transform, "Wheel_Right", RightMainWheel);

                var leftExit = CreatePoint(root.transform, "Exit_Left", LeftExitLocal);
                var rightExit = CreatePoint(root.transform, "Exit_Right", RightExitLocal);
                var rearExit = CreatePoint(root.transform, "Exit_Rear", RearExitLocal);

                var pilot = new GameObject(PilotSeatName).AddComponent<VehicleSeat>();
                pilot.transform.SetParent(root.transform, false);
                pilot.transform.localPosition = PilotSeatLocal;
                pilot.transform.localRotation = Quaternion.identity;
                pilot.SetEnterPoints(CreatePoint(root.transform, "Enter_Pilot", PilotEnterLocal));
                pilot.SetExitPoints(leftExit, rightExit);

                var vehicle = root.AddComponent<Vehicle>();
                vehicle.SetExitPoints(rearExit);
                vehicle.RefreshSeats();
                vehicle.Configure(Spec());

                root.AddComponent<SimpleAircraftMotor>();
                root.AddComponent<AircraftControlScheme>();
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

        // 胴・翼・尾の箱は試すための目印なので、差し替える参照は持たない。当たり判定は機体の当たりとして残す。
        private static void CreateBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
        }

        // 車輪は回さず転がすだけなので、見た目は付けない。押す力は SimpleAircraftMotor が機体へ掛ける。
        private static WheelCollider CreateWheel(Transform parent, string name, Vector3 localPosition)
        {
            var wheel = new GameObject(name);
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = localPosition;
            var collider = wheel.AddComponent<WheelCollider>();
            collider.radius = WheelRadius;
            collider.mass = WheelMass;
            collider.suspensionDistance = SuspensionDistance;
            collider.suspensionSpring = new JointSpring { spring = SuspensionSpring, damper = SuspensionDamper, targetPosition = 0.5f };
            return collider;
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
