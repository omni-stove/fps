using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    // 車の動き方・入力の受け渡し・ぶつかったダメージのテストで同じ車を組むためのもの。座席は運転席・助手席の順で、前輪で曲がり後輪で走る。
    internal static class TestCars
    {
        public const string VehicleId = "car";

        public static readonly CarSpec Spec = new CarSpec(
            mass: 1000f,
            motorTorque: 2000f,
            brakeTorque: 3000f,
            idleBrakeTorque: 100f,
            maxSteerAngle: 30f,
            maxForwardSpeed: 20f,
            maxReverseSpeed: 5f,
            flipAngle: 60f,
            rightingMaxSpeed: 2f,
            rightingLift: 1f);

        public static Vehicle Create(List<Object> created)
        {
            var root = new GameObject("Car");
            created.Add(root);
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(1.8f, 0.8f, 4f);

            root.AddComponent<Rigidbody>();
            var wheels = new[]
            {
                Wheel(root.transform, "FL", new Vector3(-0.85f, 0.6f, 1.3f), steers: true, drives: false),
                Wheel(root.transform, "FR", new Vector3(0.85f, 0.6f, 1.3f), steers: true, drives: false),
                Wheel(root.transform, "RL", new Vector3(-0.85f, 0.6f, -1.3f), steers: false, drives: true),
                Wheel(root.transform, "RR", new Vector3(0.85f, 0.6f, -1.3f), steers: false, drives: true),
            };

            Seat(root.transform, "Driver", new Vector3(-0.4f, 0.7f, 0.2f));
            Seat(root.transform, "Passenger", new Vector3(0.4f, 0.7f, 0.2f));

            var vehicle = root.AddComponent<Vehicle>();
            vehicle.Configure(new VehicleSpec(
                VehicleId,
                500,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Driver), new VehicleSeatSpec(VehicleSeatRole.Passenger) }));

            var motor = root.AddComponent<SimpleCarMotor>();
            motor.SetWheels(wheels);
            motor.Configure(Spec);
            return vehicle;
        }

        public static VehicleTestRider Board(Vehicle vehicle, string name, int seatIndex, List<Object> created)
        {
            var rider = new GameObject(name).AddComponent<VehicleTestRider>();
            created.Add(rider.gameObject);
            vehicle.TryEnter(rider, seatIndex, requireInRange: false);
            return rider;
        }

        private static CarWheel Wheel(Transform parent, string name, Vector3 localPosition, bool steers, bool drives)
        {
            var wheel = new GameObject(name);
            wheel.transform.SetParent(parent, false);
            wheel.transform.localPosition = localPosition;
            return new CarWheel(wheel.AddComponent<WheelCollider>(), null, steers, drives);
        }

        private static void Seat(Transform parent, string name, Vector3 localPosition)
        {
            var seat = new GameObject(name).AddComponent<VehicleSeat>();
            seat.transform.SetParent(parent, false);
            seat.transform.localPosition = localPosition;
        }
    }
}
