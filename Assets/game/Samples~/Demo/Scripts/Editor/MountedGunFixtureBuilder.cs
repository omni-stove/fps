using System;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.Editor
{
    // 盾はシーンに残るよう、作るときに MountedGun に付けさせる (Play で付けると保存したシーンに盾が無く、置き方を確かめられない)。
    public static class MountedGunFixtureBuilder
    {
        public const string DefaultName = "MountedGun";
        public const string PostName = "Post";
        public const string SeatName = "Seat_Gunner";
        public const string GunName = "Gun";
        public const string MuzzleName = "Muzzle";
        public const string BarrelName = "Barrel";
        public const string EnterName = "Enter";
        public const string ExitName = "Exit";

        // 前の半分より狭くし、横から回り込めば盾の無い所を撃てるようにする。
        public const float YawLimitDegrees = 60f;
        public const float PitchMinDegrees = -10f;
        public const float PitchMaxDegrees = 25f;
        public const float TurnSpeedDegreesPerSecond = 120f;

        public const float ChaseDistance = 3f;
        public const float ChaseHeight = 1.6f;

        public static readonly Vector3 PostSize = new Vector3(0.3f, 0.95f, 0.3f);

        public static readonly Vector3 GunLocal = new Vector3(0f, 1.1f, 0f);
        public static readonly Vector3 MuzzleLocal = new Vector3(0f, 0f, 1.2f);
        public static readonly Vector3 SeatLocal = new Vector3(0f, 0f, -0.9f);
        public static readonly Vector3 EnterLocal = new Vector3(0f, 0f, -1.6f);
        public static readonly Vector3 ExitLocal = new Vector3(0f, 0f, -1.9f);

        public static VehicleSpec Spec(string id, string weaponId)
        {
            return new VehicleSpec(
                id,
                VehicleSpec.DefaultMaxHealth,
                new[] { new VehicleSeatSpec(VehicleSeatRole.Gunner, VehicleSeatPose.Seated, VehicleCameraView.Chase) },
                new[]
                {
                    new VehicleWeaponSpec(
                        weaponId, 0, -YawLimitDegrees, YawLimitDegrees, PitchMinDegrees, PitchMaxDegrees, TurnSpeedDegreesPerSecond),
                },
                null,
                VehicleSpec.DefaultEnterRange,
                ChaseDistance,
                ChaseHeight);
        }

        public static MountedGun Create(
            Scene scene, Transform parent, string name, Vector3 position, Quaternion rotation, WeaponDefinition weapon)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new ArgumentException("scene must be a loaded scene", nameof(scene));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("name must not be empty", nameof(name));
            }

            if (weapon == null)
            {
                throw new ArgumentNullException(nameof(weapon));
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

                // 台と銃は試すための目印なので、差し替える参照は持たない (盾の見た目は MountedGun の参照で差し替える)。
                var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = PostName;
                post.transform.SetParent(root.transform, false);
                post.transform.localPosition = new Vector3(0f, PostSize.y / 2f, 0f);
                post.transform.localScale = PostSize;

                var seat = new GameObject(SeatName).AddComponent<VehicleSeat>();
                seat.transform.SetParent(root.transform, false);
                seat.transform.localPosition = SeatLocal;
                seat.transform.localRotation = Quaternion.identity;
                seat.SetEnterPoints(CreatePoint(root.transform, EnterName, EnterLocal));
                seat.SetExitPoints(CreatePoint(root.transform, ExitName, ExitLocal));

                var gun = new GameObject(GunName).AddComponent<VehicleWeaponMount>();
                gun.transform.SetParent(root.transform, false);
                gun.transform.localPosition = GunLocal;
                gun.transform.localRotation = Quaternion.identity;
                gun.SetWeapon(weapon);
                gun.SetMuzzle(CreatePoint(gun.transform, MuzzleName, MuzzleLocal));

                // 銃身に当たり判定があると、盾の脇を抜けた弾が銃身で止まり、盾が前だけを守るのか確かめにくい。
                var barrel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                barrel.name = BarrelName;
                UnityEngine.Object.DestroyImmediate(barrel.GetComponent<Collider>());
                barrel.transform.SetParent(gun.transform, false);
                barrel.transform.localPosition = new Vector3(0f, 0f, MuzzleLocal.z / 2f);
                barrel.transform.localScale = new Vector3(0.1f, 0.1f, MuzzleLocal.z);

                var vehicle = root.AddComponent<Vehicle>();
                vehicle.RefreshSeats();
                vehicle.Configure(Spec(name, weapon.Id));

                var mounted = root.AddComponent<MountedGun>();
                if (!mounted.TryInitialize())
                {
                    throw new InvalidOperationException($"'{name}' is not a mounted gun: {mounted.Problem}");
                }

                return mounted;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        public static Bounds FootprintOf(Vector3 origin, Quaternion rotation)
        {
            var top = GunLocal.y + MountedGun.PlaceholderShieldLocalScale.y;
            var halfWidth = Mathf.Max(MountedGun.PlaceholderShieldLocalScale.x / 2f, 0.5f);
            var bounds = new Bounds(origin, Vector3.zero);
            foreach (var x in new[] { -halfWidth, halfWidth })
            {
                foreach (var z in new[] { ExitLocal.z - 0.5f, MuzzleLocal.z })
                {
                    bounds.Encapsulate(origin + (rotation * new Vector3(x, 0f, z)));
                    bounds.Encapsulate(origin + (rotation * new Vector3(x, top, z)));
                }
            }

            return bounds;
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
