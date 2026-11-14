using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 同じ GameObject の <see cref="Damageable"/> が倒れたら、倒れた位置 (足元) に <see cref="Drops"/> の拾える物を落とす。
    /// 落とす物はモードが <see cref="DecideDrops"/> で決め直せる (例: タグを落とす・武器を落とさない)。
    /// 落とした物は <see cref="LifetimeSeconds"/> 後に消え、<see cref="Dropped"/> で知らせる (モードはそこで <see cref="Pickup.Collected"/> を購読する)。
    /// </summary>
    /// <remarks>
    /// 有効な間だけ Died を購読する。Edit Mode では OnEnable が走らないので <see cref="Bind"/> を直接呼ぶ。
    /// 2 つ以上落とすときは、重ならないよう足元の周りの円 (<see cref="SpreadRadius"/>) に等間隔で並べる。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class DropOnDeath : MonoBehaviour
    {
        public const string LogTag = PickupLog.Tag;

        /// <summary>落とした物が消えるまでの既定の秒数。倒され続けるモードで拾われない物が溜まり続けないようにする。</summary>
        public const float DefaultLifetimeSeconds = 30f;

        /// <summary>2 つ以上落とすときに並べる円の既定の半径 (m)。</summary>
        public const float DefaultSpreadRadius = 0.75f;

        /// <summary>既定で落とす予備弾の数。</summary>
        public const int DefaultAmmoAmount = 30;

        [SerializeField, Tooltip("倒れたときに落とす物 (モードが DecideDrops で決め直せる)")]
        private PickupContents[] _drops = { PickupContents.ForReserveAmmo(DefaultAmmoAmount) };

        [SerializeField, Tooltip("落とした物の拾い方")]
        private PickupCollectMode _collectMode = PickupCollectMode.Touch;

        [SerializeField, Min(0f), Tooltip("落とした物が消えるまでの秒数。0 なら消えない")]
        private float _lifetimeSeconds = DefaultLifetimeSeconds;

        [SerializeField, Min(0f), Tooltip("2 つ以上落とすときに並べる円の半径 (m)")]
        private float _spreadRadius = DefaultSpreadRadius;

        [SerializeField, Tooltip("落とした物の見た目 (Pickup.VisualPrefab)。空なら中身の種類で色を分けた仮の箱")]
        private GameObject _visualPrefab;

        private readonly List<PickupContents> _decided = new List<PickupContents>();
        private Damageable _damageable;

        /// <summary>
        /// モードが落とす物を決めるフック。倒れるたびに、<see cref="Drops"/> の写しを入れた drops で呼ばれ、
        /// drops を書き換える (空にすれば何も落とさない)。null なら <see cref="Drops"/> をそのまま落とす。
        /// 例外を投げたらエラーを出して何も落とさない。
        /// </summary>
        public Action<DropContext, List<PickupContents>> DecideDrops { get; set; }

        /// <summary>落としたときに、この部品と落とした物 (落とさなかったときは空) を渡して呼ばれる。</summary>
        public event Action<DropOnDeath, IReadOnlyList<Pickup>> Dropped;

        /// <summary>倒れたときに落とす物。null の要素は飛ばす。設定すると写しを持つ。</summary>
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

        /// <summary>落とした物が消えるまでの秒数。0 なら消えない。負の値は 0 にする。</summary>
        public float LifetimeSeconds
        {
            get => _lifetimeSeconds;
            set => _lifetimeSeconds = Mathf.Max(0f, value);
        }

        /// <summary>2 つ以上落とすときに並べる円の半径 (m)。負の値は 0 にする。</summary>
        public float SpreadRadius
        {
            get => _spreadRadius;
            set => _spreadRadius = Mathf.Max(0f, value);
        }

        /// <summary>落とした物の見た目 (<see cref="Pickup.VisualPrefab"/>)。null なら仮の箱。次に落とす物から使う。</summary>
        public GameObject VisualPrefab
        {
            get => _visualPrefab;
            set => _visualPrefab = value;
        }

        /// <summary>購読している体力。未購読なら null。</summary>
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

        /// <summary>damageable が倒れたら落とすようにする。前に購読していたものは外す。</summary>
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

        /// <summary>購読を外す。</summary>
        public void Unbind()
        {
            if (_damageable != null)
            {
                _damageable.Died -= OnDied;
            }

            _damageable = null;
        }

        /// <summary>
        /// contents の物を groundPosition の周りに落とし、<c>[Pickup] dropped</c> を出して <see cref="Dropped"/> を呼ぶ。落とした物を返す。
        /// 不正な中身はエラーを出して飛ばす。倒れたときに呼ばれる。モードがコードから落とすのにも使える。
        /// </summary>
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
                    pickup = Pickup.Create(valid[i], position, _collectMode, visualPrefab: _visualPrefab);
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

        /// <summary>count 個を並べるときの index 番目の、足元からのずれ。1 個なら真下 (ずれ 0)。</summary>
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
