using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
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

        [SerializeField, Range(1f, RiotShieldSpec.MaxBlockArcDegrees), Tooltip("盾の正面を中心に、弾を止める撃ち手の向きの扇の全体の角度 (度)。360 ならどの向きからでも止める")]
        private float _blockArcDegrees = RiotShieldSpec.MaxBlockArcDegrees;

        private Collider _collider;
        private bool _poseCaptured;
        private Vector3 _raisedPosition;
        private Quaternion _raisedRotation;
        private bool _raised = true;

        public event Action<BulletShield, int> Blocked;

        public bool IsRaised => _raised && isActiveAndEnabled;

        public Collider Collider => _collider != null ? _collider : (_collider = GetComponent<Collider>());

        public EnemyAgent Owner => GetComponentInParent<EnemyAgent>();

        // 敵でない持ち主 (盾の武器を持つプレイヤー) は、体力の持ち主でログと知らせを出す。
        public Damageable Holder => GetComponentInParent<Damageable>();

        public float BlockArcDegrees
        {
            get => _blockArcDegrees;
            set
            {
                if (!(value > 0f && value <= RiotShieldSpec.MaxBlockArcDegrees))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, "block arc must be greater than 0 and at most 360 degrees");
                }

                _blockArcDegrees = value;
            }
        }

        // 撃った位置が分からない弾 (爆風の破片など) は向きを決められないので止める側に倒す。
        public bool Covers(Vector3? sourcePosition)
        {
            if (_blockArcDegrees >= RiotShieldSpec.MaxBlockArcDegrees || !sourcePosition.HasValue)
            {
                return true;
            }

            return MeleeJudge.IsWithinArc(
                transform.position.ToNumerics(), transform.forward.ToNumerics(), _blockArcDegrees, sourcePosition.Value.ToNumerics());
        }

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
            // 扇の外から撃たれた弾は止めず、呼び手がそのまま着弾させる。盾の板は持ち主の子なので、Hitbox.FindFor を通って持ち主に当たる。
            if (shield == null || !shield.IsRaised || !shield.Covers(sourcePosition))
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
            return CreatePlaceholder(owner, PlaceholderLocalPosition, PlaceholderLocalScale);
        }

        // 固定銃座のように敵より大きな物の前にも立てるので、位置と大きさを持ち主が決められるようにする。
        public static BulletShield CreatePlaceholder(Transform owner, Vector3 localPosition, Vector3 localScale)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = PlaceholderName;
            plate.transform.SetParent(owner, false);
            plate.transform.localPosition = localPosition;
            plate.transform.localRotation = Quaternion.identity;
            plate.transform.localScale = localScale;
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
            // 固定銃座の盾は敵に付かないので、盾の名前ではなく、どの銃座の盾が止めたかを出す。銃座も体力を持つので、持ち主より先に見る。
            var vehicle = owner == null ? GetComponentInParent<Vehicle>() : null;
            var holder = owner == null && vehicle == null ? Holder : null;
            var id = owner != null ? owner.Id : vehicle != null ? vehicle.name : holder != null ? holder.Id : name;
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
