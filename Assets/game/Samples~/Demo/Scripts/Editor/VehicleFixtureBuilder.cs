using System;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fps.Editor
{
    /// <summary>
    /// 乗り降りを試す、動かない仮の乗り物 (<see cref="Vehicle"/>) をシーンに置く。見た目は箱 1 つで、座席は 3 つ:
    /// 運転席 (左前・座る・追うカメラ)、助手席 (右前・座る・座席からの視点)、銃座 (後ろの屋根・立つ・追うカメラ)。
    /// 運転席は左、助手席は右から乗り、降りるときはまず乗った側、塞がっていれば反対側に降りる。どの座席も最後に後ろへ降りられる。
    /// 助手席は前の半分 (<see cref="PassengerArcDegrees"/>) へ自分の武器で撃て、銃座には gunnerWeapon を渡すと屋根の銃 (<see cref="GunName"/>) が付く。
    /// 動き方はまだ持たない (後から差し込む)。耐久は <see cref="Vehicle"/> が自分で付ける <see cref="Damageable"/> の既定の値で、部位は持たない。
    /// </summary>
    /// <remarks>
    /// 位置は乗り物の原点 (床の高さ・車体の中心の真下) から見たもの。車体は <see cref="BodySize"/> の箱で、+Z が前。
    /// </remarks>
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

        /// <summary>左に降りる位置 (足元)。運転席の最初の候補。</summary>
        public static readonly Vector3 LeftExitLocal = new Vector3(-2f, 0f, 0.4f);

        /// <summary>右に降りる位置 (足元)。助手席の最初の候補で、運転席の 2 番目の候補。</summary>
        public static readonly Vector3 RightExitLocal = new Vector3(2f, 0f, 0.4f);

        /// <summary>後ろに降りる位置 (足元)。どの座席からでも使う乗り物の候補。</summary>
        public static readonly Vector3 RearExitLocal = new Vector3(0f, 0f, -3f);

        /// <summary>助手席の視点 (座席から見た位置)。</summary>
        public static readonly Vector3 PassengerViewLocal = new Vector3(0f, 0.9f, 0f);

        /// <summary>銃座の銃の回る点。銃座の前、立った腰の高さ。</summary>
        public static readonly Vector3 GunLocal = new Vector3(0f, 2.2f, -0.6f);

        /// <summary>弾の出る点 (銃から見た位置)。</summary>
        public static readonly Vector3 MuzzleLocal = new Vector3(0f, 0f, 1f);

        /// <summary>運転席・助手席・銃座の順の設定。乗り物の Id は <see cref="DefaultName"/>。gunnerWeaponId があれば銃座にその武器を付ける。</summary>
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

        /// <summary>
        /// 仮の乗り物を作って position・rotation に置き、返す。parent が null ならシーンの直下に置く。
        /// gunnerWeapon があれば銃座に銃を付ける。
        /// </summary>
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

        /// <summary>origin・rotation に置いた仮の乗り物の車体の範囲 (床に置くものの重なりを確かめるのに使う)。</summary>
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
