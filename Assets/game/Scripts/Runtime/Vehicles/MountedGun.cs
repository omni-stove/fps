using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 乗り降り・撃ち方・耐久は乗り物の土台 (Vehicle・VehicleSeat・VehicleWeaponMount) をそのまま使い、ここは形の確かめ・前面の盾・敵が探す一覧だけを持つ。
    // 同じことを別に持つと、プレイヤーと敵とで乗り方や撃ち方の規則がずれるため。
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Vehicle))]
    public sealed class MountedGun : MonoBehaviour
    {
        public const string LogTag = MountedGunLog.Tag;

        // 銃の回る点から見た仮の盾。座った銃手の胸から頭を前から隠す大きさにする。
        public static readonly Vector3 PlaceholderShieldLocalPosition = new Vector3(0f, 0f, 0.35f);
        public static readonly Vector3 PlaceholderShieldLocalScale = new Vector3(1.1f, 0.9f, 0.06f);

        private static readonly List<MountedGun> _active = new List<MountedGun>();

        [SerializeField, Tooltip("前面の盾。空なら盾の見た目から作って銃に付ける")]
        private BulletShield _shield;

        [SerializeField, Tooltip("盾の見た目。根元に Collider を持たせる。空なら仮の板を作る")]
        private GameObject _shieldPrefab;

        private Vehicle _vehicle;
        private bool _initialized;
        private string _problem;

        public static IReadOnlyList<MountedGun> Active
        {
            get
            {
                _active.RemoveAll(gun => gun == null || !gun.IsLive);
                return _active;
            }
        }

        public Vehicle Vehicle => _vehicle != null ? _vehicle : (_vehicle = GetComponent<Vehicle>());

        public VehicleSeat Seat => Vehicle.Seats.Count > 0 ? Vehicle.Seats[0] : null;

        public VehicleWeaponMount Mount => Vehicle.WeaponMounts.Count > 0 ? Vehicle.WeaponMounts[0] : null;

        public BulletShield Shield => _shield;

        public GameObject ShieldPrefab
        {
            get => _shieldPrefab;
            set => _shieldPrefab = value;
        }

        public string Problem => _problem;

        public bool IsReady => _initialized && _problem == null;

        public bool IsFree => IsLive && !Vehicle.IsWrecked && Seat != null && !Seat.IsOccupied;

        public Vector3 BoardPoint
        {
            get
            {
                var seat = Seat;
                foreach (var point in seat.EnterPoints)
                {
                    if (point != null)
                    {
                        return point.position;
                    }
                }

                return seat.SitPosition;
            }
        }

        // OnEnable が走らない Edit Mode でも一覧に載せられるよう、isActiveAndEnabled ではなく自分で見る。
        private bool IsLive => IsReady && enabled && gameObject.activeInHierarchy && Vehicle.IsLive;

        private void Awake()
        {
            if (!TryInitialize())
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (IsReady && !_active.Contains(this))
            {
                _active.Add(this);
            }
        }

        private void OnDisable()
        {
            _active.Remove(this);
        }

        public bool TryInitialize()
        {
            if (_initialized)
            {
                return _problem == null;
            }

            _initialized = true;
            // 座席が無い乗り物は Spec を作れず例外になるので、形の確かめより先に見る。
            _problem = Vehicle.Seats.Count == 0
                ? MountedGunLog.ReasonSeatCount
                : MountedGunShape.Problem(Vehicle.Spec, GetComponent<IVehicleMotor>() != null);
            if (_problem != null)
            {
                Debug.LogError(MountedGunLog.Invalid(name, _problem), this);
                return false;
            }

            if (_shield == null)
            {
                _shield = CreateShield();
            }

            if (!_active.Contains(this))
            {
                _active.Add(this);
            }

            return true;
        }

        // 使えないプレハブで盾の無い銃座にすると前からの弾が銃手に通るので、誤りは出したうえで仮の板で続ける。
        private BulletShield CreateShield()
        {
            if (_shieldPrefab == null)
            {
                return BulletShield.CreatePlaceholder(Mount.transform, PlaceholderShieldLocalPosition, PlaceholderShieldLocalScale);
            }

            try
            {
                return BulletShield.Create(Mount.transform, _shieldPrefab);
            }
            catch (ArgumentException e)
            {
                Debug.LogError(MountedGunLog.ShieldFallback(name, e.Message), this);
                return BulletShield.CreatePlaceholder(Mount.transform, PlaceholderShieldLocalPosition, PlaceholderShieldLocalScale);
            }
        }

        public bool CanAimAt(Vector3 worldPoint)
        {
            return TryLocalDirection(worldPoint, out var local) && Mount.Aim.CanReach(local);
        }

        public float AimOffsetDegrees(Vector3 worldPoint)
        {
            return TryLocalDirection(worldPoint, out var local) ? Mount.Aim.OffsetDegrees(local) : 0f;
        }

        private bool TryLocalDirection(Vector3 worldPoint, out System.Numerics.Vector3 local)
        {
            local = default;
            var mount = Mount;
            if (mount == null)
            {
                throw new InvalidOperationException($"'{name}' has no VehicleWeaponMount");
            }

            var toPoint = worldPoint - mount.transform.position;
            if (toPoint.sqrMagnitude < 1e-8f)
            {
                return false;
            }

            local = (Quaternion.Inverse(Vehicle.transform.rotation) * toPoint).ToNumerics();
            return true;
        }
    }
}
