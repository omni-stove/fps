using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 有効な間だけ Died を購読する。Edit Mode では OnEnable が走らないので、Bind を直接呼べるようにする。
    [DisallowMultipleComponent]
    public sealed class DropOnDeath : MonoBehaviour
    {
        public const string LogTag = PickupLog.Tag;

        // 倒され続けるモードで拾われない物が溜まり続けないよう、落とした物は既定で消す。
        public const float DefaultLifetimeSeconds = 30f;

        public const float DefaultSpreadRadius = 0.75f;

        public const int DefaultAmmoAmount = 30;

        [SerializeField, Tooltip("倒れたときに落とす物 (モードが DecideDrops で決め直せる)")]
        private PickupContents[] _drops = { PickupContents.ForReserveAmmo(DefaultAmmoAmount) };

        [SerializeField, Tooltip("落とした物の拾い方")]
        private PickupCollectMode _collectMode = PickupCollectMode.Touch;

        [SerializeField, Range(0f, Pickup.MaxLookAngleDegrees), Tooltip("Look で、照準の向きからこの角度 (度) 以内に見た目が見えていれば照準が合っているとする。0 なら見た目に照準の線が当たったときだけ")]
        private float _lookAngleDegrees = Pickup.DefaultLookAngleDegrees;

        [SerializeField, Min(0f), Tooltip("落とした物が消えるまでの秒数。0 なら消えない")]
        private float _lifetimeSeconds = DefaultLifetimeSeconds;

        [SerializeField, Min(0f), Tooltip("2 つ以上落とすときに並べる円の半径 (m)")]
        private float _spreadRadius = DefaultSpreadRadius;

        [SerializeField, Tooltip("落とした物の見た目 (Pickup.VisualPrefab)。空なら中身の種類で色を分けた仮の箱")]
        private GameObject _visualPrefab;

        private readonly List<PickupContents> _decided = new List<PickupContents>();
        private Damageable _damageable;

        public Action<DropContext, List<PickupContents>> DecideDrops { get; set; }

        public event Action<DropOnDeath, IReadOnlyList<Pickup>> Dropped;

        public IReadOnlyList<PickupContents> Drops
        {
            get => _drops ?? Array.Empty<PickupContents>();
            set
            {
                if (value == null)
                {
                    _drops = Array.Empty<PickupContents>();
                    return;
                }

                var copy = new PickupContents[value.Count];
                for (var i = 0; i < copy.Length; i++)
                {
                    // 保存されてから Play で気づくのを避けるため、設定した時点で検査する。
                    value[i]?.ToSpec();
                    copy[i] = value[i];
                }

                _drops = copy;
            }
        }

        public PickupCollectMode CollectMode
        {
            get => _collectMode;
            set => _collectMode = value;
        }

        public float LifetimeSeconds
        {
            get => _lifetimeSeconds;
            set => _lifetimeSeconds = Mathf.Max(0f, value);
        }

        public float LookAngleDegrees
        {
            get => _lookAngleDegrees;
            set => _lookAngleDegrees = float.IsNaN(value) ? 0f : Mathf.Clamp(value, 0f, Pickup.MaxLookAngleDegrees);
        }

        public float SpreadRadius
        {
            get => _spreadRadius;
            set => _spreadRadius = Mathf.Max(0f, value);
        }

        public GameObject VisualPrefab
        {
            get => _visualPrefab;
            set => _visualPrefab = value;
        }

        public Damageable Damageable => _damageable;

        private void OnEnable()
        {
            var damageable = GetComponent<Damageable>();
            if (damageable == null)
            {
                Debug.LogError($"{LogTag} DropOnDeath '{name}' と同じ GameObject に Damageable がありません。", this);
                enabled = false;
                return;
            }

            Bind(damageable);
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void Bind(Damageable damageable)
        {
            if (damageable == null)
            {
                throw new ArgumentNullException(nameof(damageable));
            }

            if (_damageable == damageable)
            {
                return;
            }

            Unbind();
            _damageable = damageable;
            _damageable.Died += OnDied;
        }

        public void Unbind()
        {
            if (_damageable != null)
            {
                _damageable.Died -= OnDied;
            }

            _damageable = null;
        }

        public IReadOnlyList<Pickup> Drop(IReadOnlyList<PickupContents> contents, Vector3 groundPosition)
        {
            if (contents == null)
            {
                throw new ArgumentNullException(nameof(contents));
            }

            var valid = new List<PickupContents>(contents.Count);
            foreach (var item in contents)
            {
                if (item != null)
                {
                    valid.Add(item);
                }
            }

            var owner = _damageable != null ? _damageable.Id : name;
            var dropped = new List<Pickup>(valid.Count);
            for (var i = 0; i < valid.Count; i++)
            {
                var position = groundPosition + SpreadOffset(i, valid.Count, _spreadRadius);
                Pickup pickup;
                try
                {
                    pickup = Pickup.Create(valid[i], position, _collectMode, visualPrefab: _visualPrefab, lookAngleDegrees: _lookAngleDegrees);
                }
                catch (ArgumentException e)
                {
                    Debug.LogError($"{LogTag} DropOnDeath '{name}' の {i} 番目の中身が不正なため落とせません: {e.Message}", this);
                    continue;
                }

                pickup.LifetimeSeconds = _lifetimeSeconds;
                dropped.Add(pickup);
                Debug.Log(PickupLog.Dropped(owner, pickup.name, pickup.Spec, position.ToNumerics()), pickup);
            }

            var result = dropped.AsReadOnly();
            Dropped?.Invoke(this, result);
            return result;
        }

        // 2 つ以上落とすときは、重ならないよう足元の周りの円に等間隔で並べる。
        public static Vector3 SpreadOffset(int index, int count, float radius)
        {
            if (count <= 1 || radius <= 0f)
            {
                return Vector3.zero;
            }

            var angle = index * Mathf.PI * 2f / count;
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        private void OnDied(Damageable damageable)
        {
            var position = damageable.GroundPoint;
            var attribution = CombatEvents.CurrentAttribution;
            var killer = attribution.HasValue ? CombatEvents.OfflineObject(attribution.Value.KillerId) : null;
            if (killer == damageable.gameObject)
            {
                killer = null;
            }

            var side = SideMember.Of(damageable.gameObject);
            var context = new DropContext(this, damageable, killer, side != null ? side.Side : (Side?)null, position);

            _decided.Clear();
            foreach (var item in Drops)
            {
                if (item != null)
                {
                    _decided.Add(item.Clone());
                }
            }

            var decide = DecideDrops;
            if (decide != null)
            {
                // モードのフックの例外で、同じ Died を待つ他の購読者 (モードの得点など) まで止めない。
                try
                {
                    decide(context, _decided);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{LogTag} DropOnDeath '{name}' の DecideDrops が失敗したため何も落としません: {e.Message}", this);
                    Debug.LogException(e, this);
                    return;
                }
            }

            Drop(_decided.ToArray(), position);
        }
    }
}
