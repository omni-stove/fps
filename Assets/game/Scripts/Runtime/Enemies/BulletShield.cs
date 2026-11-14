using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 盾は持ち主の子に置くので、Hitbox.FindFor は親の胴の Hitbox を返してしまう。弾の流れ (Hitscan・WeaponController) だけが先にここで止める。
    // 近接と爆発は Hitbox.FindFor を通って持ち主に届くので、盾ごと殴れて、盾越しの爆発も効く。
    [DisallowMultipleComponent]
    public sealed class BulletShield : MonoBehaviour
    {
        public const string PlaceholderName = "Shield";

        // 敵のカプセル (半径 0.5m、高さ 2m) の前面に、胴を隠す大きさで立てる。
        public static readonly Vector3 PlaceholderLocalPosition = new Vector3(0f, 0.1f, 0.65f);
        public static readonly Vector3 PlaceholderLocalScale = new Vector3(1.1f, 1.5f, 0.1f);

        [SerializeField, Tooltip("下ろしたときに、構えた位置からずらす量 (親から見た向き、m)")]
        private Vector3 _loweredOffset = new Vector3(0f, -0.6f, -0.15f);

        [SerializeField, Range(0f, 90f), Tooltip("下ろしたときに前へ倒す角度 (度)")]
        private float _loweredTiltDegrees = 70f;

        private Collider _collider;
        private bool _poseCaptured;
        private Vector3 _raisedPosition;
        private Quaternion _raisedRotation;
        private bool _raised = true;

        public event Action<BulletShield, int> Blocked;

        public bool IsRaised => _raised && isActiveAndEnabled;

        public Collider Collider => _collider != null ? _collider : (_collider = GetComponent<Collider>());

        public EnemyAgent Owner => GetComponentInParent<EnemyAgent>();

        private void Awake()
        {
            CapturePose();
            if (Collider == null)
            {
                Debug.LogError($"{ShieldLog.LogTag} shield '{name}' has no collider; it cannot stop bullets", this);
            }
        }

        // 下ろしたら当たり判定を切る。切らずに通すと弾が盾の位置で胴に当たったことになり、盾の陰の胴が見えないまま撃ち抜ける。
        public void SetRaised(bool raised)
        {
            CapturePose();
            if (_raised == raised)
            {
                return;
            }

            _raised = raised;
            var collider = Collider;
            if (collider != null)
            {
                collider.enabled = raised;
            }

            if (raised)
            {
                transform.localPosition = _raisedPosition;
                transform.localRotation = _raisedRotation;
            }
            else
            {
                transform.localPosition = _raisedPosition + _loweredOffset;
                transform.localRotation = Quaternion.AngleAxis(_loweredTiltDegrees, Vector3.right) * _raisedRotation;
            }
        }

        public static bool TryBlock(Collider collider, int damage, Vector3? sourcePosition, GameObject shooter)
        {
            if (collider == null)
            {
                throw new ArgumentNullException(nameof(collider));
            }

            var shield = collider.GetComponent<BulletShield>();
            if (shield == null || !shield.IsRaised)
            {
                return false;
            }

            shield.OnBlocked(damage, sourcePosition, shooter);
            return true;
        }

        public static BulletShield Create(Transform owner, GameObject prefab)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            if (prefab == null)
            {
                return CreatePlaceholder(owner);
            }

            // 弾が止まるかは当たったコライダーと同じ物に BulletShield があるかで見る (TryBlock) ので、子のコライダーでは止まらない。
            if (prefab.GetComponent<Collider>() == null)
            {
                throw new ArgumentException($"shield prefab '{prefab.name}' has no collider on its root; it cannot stop bullets", nameof(prefab));
            }

            var plate = UnityEngine.Object.Instantiate(prefab, owner, false);
            plate.name = prefab.name;
            var shield = plate.GetComponent<BulletShield>();
            return shield != null ? shield : plate.AddComponent<BulletShield>();
        }

        public static BulletShield CreatePlaceholder(Transform owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = PlaceholderName;
            plate.transform.SetParent(owner, false);
            plate.transform.localPosition = PlaceholderLocalPosition;
            plate.transform.localRotation = Quaternion.identity;
            plate.transform.localScale = PlaceholderLocalScale;
            return plate.AddComponent<BulletShield>();
        }

        private void CapturePose()
        {
            if (_poseCaptured)
            {
                return;
            }

            _poseCaptured = true;
            _raisedPosition = transform.localPosition;
            _raisedRotation = transform.localRotation;
        }

        private static string DescribeShooter(GameObject shooter)
        {
            if (shooter == null)
            {
                return null;
            }

            var damageable = shooter.GetComponentInParent<Damageable>();
            return damageable != null ? damageable.Id : shooter.name;
        }

        private void OnBlocked(int damage, Vector3? sourcePosition, GameObject shooter)
        {
            var owner = Owner;
            var id = owner != null ? owner.Id : name;
            Debug.Log(ShieldLog.Blocked(id, damage, DescribeShooter(shooter)), this);
            Blocked?.Invoke(this, damage);

            // 止めても撃たれたことには気付かせる。気付かないと、盾に撃ち込まれている間も棒立ちのままになる。
            if (owner != null && sourcePosition.HasValue)
            {
                owner.Alert(sourcePosition.Value);
            }
        }
    }
}
