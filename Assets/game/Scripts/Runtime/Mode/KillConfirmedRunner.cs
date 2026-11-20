using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // タグが消えるまでの秒数は Core が数えるので、Pickup 自身の LifetimeSeconds は 0 にする。
    // 出現点に戻した参加者を残っているタグの範囲から出たことにするのは、倒れた場所のタグの範囲に入ったまま数えられ、
    // 戻った瞬間に遠くから自分のタグを拾ってしまわないようにするため。
    public sealed class KillConfirmedRunner : ModeRunnerBase
    {
        public const int DefaultScoreLimit = KillConfirmedSpec.DefaultScoreLimit;
        public const float DefaultTimeLimitSeconds = KillConfirmedSpec.DefaultTimeLimitSeconds;
        public const float DefaultRespawnSeconds = KillConfirmedSpec.DefaultRespawnSeconds;
        public const float DefaultTagLifetimeSeconds = KillConfirmedSpec.DefaultTagLifetimeSeconds;
        public const int DefaultPointsPerConfirm = KillConfirmedSpec.DefaultPointsPerConfirm;

        public const float DefaultTagRadius = KillConfirmedSpec.DefaultTagPickupRadius;

        [SerializeField, Tooltip("このモードの調整値のアセット。配線すると下の得点上限・制限時間・リスポーン・タグの秒数・確定の点・タグの半径の代わりにこれを読む。空なら入口 (GameContent) のものを使う")]
        private KillConfirmedDefinition _definition;

        [SerializeField, Min(1), Tooltip("どちらかのチームがこの確定数に届いたら試合が終わる")]
        private int _scoreLimit = DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        [SerializeField, Min(0.01f), Tooltip("落ちたタグが誰にも拾われずに消えるまでの秒数")]
        private float _tagLifetimeSeconds = DefaultTagLifetimeSeconds;

        [SerializeField, Min(1), Tooltip("敵のタグを 1 枚拾って確定させたときに入る点")]
        private int _pointsPerConfirm = DefaultPointsPerConfirm;

        [SerializeField, Min(Pickup.MinRadius), Tooltip("タグを拾える範囲の半径 (m)")]
        private float _tagRadius = DefaultTagRadius;

        private readonly List<HookedDrop> _hooked = new List<HookedDrop>();
        private readonly Dictionary<int, Pickup> _pickupOf = new Dictionary<int, Pickup>();
        private readonly Dictionary<Pickup, KillConfirmedTag> _tagOf = new Dictionary<Pickup, KillConfirmedTag>();
        private readonly List<Collider> _colliders = new List<Collider>();
        private KillConfirmed _killConfirmed;

        public KillConfirmed KillConfirmed => _killConfirmed;

        public KillConfirmedDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public KillConfirmedDefinition EffectiveDefinition => GameContent.Pick(_definition, c => c.KillConfirmed);

        public int ScoreLimit
        {
            get => _scoreLimit;
            set => _scoreLimit = value;
        }

        public float TimeLimitSeconds
        {
            get => _timeLimitSeconds;
            set => _timeLimitSeconds = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public float TagLifetimeSeconds
        {
            get => _tagLifetimeSeconds;
            set => _tagLifetimeSeconds = value;
        }

        public int PointsPerConfirm
        {
            get => _pointsPerConfirm;
            set => _pointsPerConfirm = value;
        }

        public float TagRadius
        {
            get => _tagRadius;
            set => _tagRadius = value;
        }

        public int TagPickupCount => _pickupOf.Count;

        public Pickup PickupOf(int tagId)
        {
            return _pickupOf.TryGetValue(tagId, out var pickup) ? pickup : null;
        }

        public KillConfirmedSpec CreateSpec()
        {
            var definition = EffectiveDefinition;
            if (definition != null)
            {
                return definition.ToSpec();
            }

            return new KillConfirmedSpec(
                _scoreLimit, _timeLimitSeconds, _respawnSeconds, _tagLifetimeSeconds, _pointsPerConfirm, _tagRadius);
        }

        public void Begin(
            KillConfirmed killConfirmed,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            if (killConfirmed == null)
            {
                throw new ArgumentNullException(nameof(killConfirmed));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"{nameof(KillConfirmedRunner)} '{name}' has already started a mode");
            }

            Attach(killConfirmed);
            try
            {
                Begin(killConfirmed.Session, members, spawnPoints, localPlayer);
            }
            catch
            {
                Detach();
                throw;
            }
        }

        protected override ModeSession CreateSession()
        {
            var killConfirmed = new KillConfirmed(CreateSpec());
            Attach(killConfirmed);
            return killConfirmed.Session;
        }

        protected override void OnBegan()
        {
            foreach (var member in Members)
            {
                HookDrops(member);
            }
        }

        protected override void OnSpawned(SideMember member)
        {
            if (_pickupOf.Count == 0)
            {
                return;
            }

            member.GetComponentsInChildren(true, _colliders);
            foreach (var pickup in _pickupOf.Values)
            {
                if (pickup == null)
                {
                    continue;
                }

                foreach (var collider in _colliders)
                {
                    pickup.HandleExit(collider);
                }
            }

            _colliders.Clear();
        }

        protected override void OnEnded(Side? winner)
        {
            // Core は試合の終わりでタグを消している。拾えるものを場に残さない。
            foreach (var pickup in new List<Pickup>(_tagOf.Keys))
            {
                Forget(pickup);
                DestroyPickup(pickup);
            }
        }

        protected override object HudDetail => _killConfirmed;

        protected override void OnStopping()
        {
            Detach();
        }

        private void Attach(KillConfirmed killConfirmed)
        {
            Detach();
            _killConfirmed = killConfirmed;
            _killConfirmed.TagExpired += OnTagExpired;
        }

        private void Detach()
        {
            foreach (var hooked in _hooked)
            {
                if (hooked.Drop == null)
                {
                    continue;
                }

                hooked.Drop.Dropped -= OnDropped;
                hooked.Drop.DecideDrops = hooked.Previous;
            }

            _hooked.Clear();

            foreach (var pickup in _tagOf.Keys)
            {
                if (pickup != null)
                {
                    pickup.Collected -= OnTagCollected;
                }
            }

            _tagOf.Clear();
            _pickupOf.Clear();

            if (_killConfirmed != null)
            {
                _killConfirmed.TagExpired -= OnTagExpired;
            }

            _killConfirmed = null;
        }

        private void HookDrops(SideMember member)
        {
            var damageable = member.Damageable;
            var drop = damageable.GetComponent<DropOnDeath>();
            if (drop == null)
            {
                drop = damageable.gameObject.AddComponent<DropOnDeath>();

                // 既定の中身 (予備弾) を足すと、モードが決めていない物まで落ちる。タグはフックが足す。
                drop.Drops = Array.Empty<PickupContents>();
            }

            // Edit Mode では OnEnable が走らず購読されないので、ここで繋ぐ。同じ体力なら何もしない。
            drop.Bind(damageable);

            var previous = drop.DecideDrops;
            drop.DecideDrops = (context, drops) =>
            {
                previous?.Invoke(context, drops);
                DecideTagDrop(context, drops);
            };
            drop.Dropped += OnDropped;
            _hooked.Add(new HookedDrop(drop, previous));
        }

        // DropOnDeath とこの Runner のどちらが先に倒れたことを受けても同じになるよう、ここではモードの状態を変えず、登録は OnDropped で行う。
        private void DecideTagDrop(DropContext context, List<PickupContents> drops)
        {
            drops.RemoveAll(item => item == null || item.Kind == PickupKind.Tag);
            if (_killConfirmed == null)
            {
                return;
            }

            var id = IdOf(SideMember.Of(context.Victim != null ? context.Victim.gameObject : null));
            if (id < 0 || !_killConfirmed.CanDropTag(id))
            {
                return;
            }

            drops.Add(PickupContents.ForTag(id, _killConfirmed.Session.GetSide(id)));
        }

        private void OnDropped(DropOnDeath source, IReadOnlyList<Pickup> pickups)
        {
            foreach (var pickup in pickups)
            {
                if (pickup == null || pickup.Spec.Kind != PickupKind.Tag)
                {
                    continue;
                }

                var ownerId = pickup.Spec.TagOwnerId;
                var tag = _killConfirmed != null && MemberOf(ownerId) != null ? _killConfirmed.DropTag(ownerId) : null;
                if (tag == null)
                {
                    // 試合の外で落ちたタグは数えようがないので、拾えるまま残さない。
                    DestroyPickup(pickup);
                    continue;
                }

                pickup.LifetimeSeconds = 0f;
                pickup.Radius = _killConfirmed.Spec.TagPickupRadius;
                pickup.Collected += OnTagCollected;
                _tagOf.Add(pickup, tag);
                _pickupOf.Add(tag.Id, pickup);
                Debug.Log(KillConfirmedLog.DropLog(tag), pickup);
            }
        }

        private void OnTagCollected(PickupCollection collection)
        {
            var pickup = collection.Pickup;
            if (!_tagOf.TryGetValue(pickup, out var tag))
            {
                return;
            }

            // Pickup は拾われたら自分で消えるので、結果にかかわらず手放す。
            Forget(pickup);

            var collector = SideMember.Of(collection.Collector != null ? collection.Collector.gameObject : null);
            var collectorId = IdOf(collector);
            if (collectorId < 0)
            {
                Debug.Log(KillConfirmedLog.DiscardLog(tag, collection.Collector != null ? collection.Collector.Id : null), this);
                _killConfirmed.Discard(tag.Id);
                return;
            }

            var result = _killConfirmed.Collect(tag.Id, collectorId);
            Debug.Log(KillConfirmedLog.CollectLog(tag, collectorId, collector.Side, result), collector);

            switch (result)
            {
                case KillConfirmedResult.Confirmed:
                    if (Session != null && Session.State == GameModeState.Playing)
                    {
                        LogScore();
                    }

                    break;
                case KillConfirmedResult.Denied:
                    break;
                default:
                    // Pickup はもう無いので、Core にだけ残して数え続けないようにする。
                    _killConfirmed.Discard(tag.Id);
                    break;
            }

            RefreshHud();
        }

        private void OnTagExpired(KillConfirmedTag tag)
        {
            Debug.Log(KillConfirmedLog.ExpireLog(tag), this);
            if (_pickupOf.TryGetValue(tag.Id, out var pickup))
            {
                Forget(pickup);
                DestroyPickup(pickup);
            }
        }

        private void Forget(Pickup pickup)
        {
            if (!_tagOf.TryGetValue(pickup, out var tag))
            {
                return;
            }

            _tagOf.Remove(pickup);
            _pickupOf.Remove(tag.Id);
            if (pickup != null)
            {
                pickup.Collected -= OnTagCollected;
            }
        }

        private static void DestroyPickup(Pickup pickup)
        {
            if (pickup == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(pickup.gameObject);
            }
            else
            {
                DestroyImmediate(pickup.gameObject);
            }
        }

        private readonly struct HookedDrop
        {
            public HookedDrop(DropOnDeath drop, Action<DropContext, List<PickupContents>> previous)
            {
                Drop = drop;
                Previous = previous;
            }

            public DropOnDeath Drop { get; }

            public Action<DropContext, List<PickupContents>> Previous { get; }
        }
    }
}
