using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// キルコンファームド (<see cref="Fps.Core.KillConfirmed"/>) をシーンで進める <see cref="ModeRunnerBase"/>。陣営 0 が Alpha、陣営 1 が Bravo。
    /// 倒しただけでは点が入らず、倒れた人の落としたタグ (<see cref="Pickup"/>) を敵が拾えば確定 (1 点)、味方が拾えば阻止 (0 点) になる。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>始めたときに全参加者の <see cref="DropOnDeath"/> の <see cref="DropOnDeath.DecideDrops"/> にタグを足すフックを掛ける。
    /// DropOnDeath の無い参加者 (実行時に作られるプレイヤーなど) には、タグだけを落とす DropOnDeath を付ける。
    /// 前から掛かっていたフックは先に呼び、止めるときに戻す。</item>
    /// <item>落ちたタグは <see cref="Fps.Core.KillConfirmed.DropTag"/> に登録し、<see cref="Pickup.Collected"/> を
    /// <see cref="Fps.Core.KillConfirmed.Collect"/> に渡す。消えるまでの秒数は Core が数え、消えたら Pickup を壊す
    /// (Pickup 自身の <see cref="Pickup.LifetimeSeconds"/> は 0 にする)。</item>
    /// <item>参加者でない者に拾われたタグは、点も阻止も数えずに無くす。</item>
    /// <item>出現点に戻した参加者は、残っているタグの範囲から出たことにする (倒れた場所のタグの範囲に入ったまま数えられ、
    /// 戻った瞬間に遠くから自分のタグを拾ってしまわないように)。</item>
    /// <item>試合が終わったら残ったタグの Pickup を壊す。</item>
    /// <item>HUD には得点 (= 確定数) の下に、陣営ごとの確定数・阻止数と落ちているタグの数 (<see cref="KillConfirmedText.HudLines"/>) を出す。
    /// タグを落とした・拾った・消えたときに <c>[Mode] killconfirmed</c> のログを出す。</item>
    /// </list>
    /// </remarks>
    public sealed class KillConfirmedRunner : ModeRunnerBase
    {
        public const int DefaultScoreLimit = 20;
        public const float DefaultTimeLimitSeconds = 300f;
        public const float DefaultRespawnSeconds = 3f;
        public const float DefaultTagLifetimeSeconds = 30f;

        /// <summary>タグを拾える範囲の既定の半径 (m)。</summary>
        public const float DefaultTagRadius = Pickup.DefaultRadius;

        [SerializeField, Tooltip("このモードの調整値のアセット。配線すると下の確定数・制限時間・リスポーン・タグの秒数の代わりにこれを読む")]
        private KillConfirmedDefinition _definition;

        [SerializeField, Min(1), Tooltip("どちらかのチームがこの確定数に届いたら試合が終わる")]
        private int _scoreLimit = DefaultScoreLimit;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)")]
        private float _timeLimitSeconds = DefaultTimeLimitSeconds;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        [SerializeField, Min(0.01f), Tooltip("落ちたタグが誰にも拾われずに消えるまでの秒数")]
        private float _tagLifetimeSeconds = DefaultTagLifetimeSeconds;

        [SerializeField, Min(Pickup.MinRadius), Tooltip("タグを拾える範囲の半径 (m)")]
        private float _tagRadius = DefaultTagRadius;

        private readonly List<HookedDrop> _hooked = new List<HookedDrop>();
        private readonly Dictionary<int, Pickup> _pickupOf = new Dictionary<int, Pickup>();
        private readonly Dictionary<Pickup, KillConfirmedTag> _tagOf = new Dictionary<Pickup, KillConfirmedTag>();
        private readonly List<Collider> _colliders = new List<Collider>();
        private KillConfirmed _killConfirmed;

        /// <summary>進めているキルコンファームド。開始前は null。</summary>
        public KillConfirmed KillConfirmed => _killConfirmed;

        /// <summary>調整値のアセット。null なら Inspector の値 (既定は Default* の値) で始める。</summary>
        public KillConfirmedDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

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

        /// <summary>タグを拾える範囲の半径 (m)。<see cref="Pickup.MinRadius"/> 未満は落とすときに切り上げる。</summary>
        public float TagRadius
        {
            get => _tagRadius;
            set => _tagRadius = value;
        }

        /// <summary>今落ちているタグの Pickup の数。</summary>
        public int TagPickupCount => _pickupOf.Count;

        /// <summary>落ちている tagId のタグの Pickup。拾われたか消えたか、知らない番号なら null。</summary>
        public Pickup PickupOf(int tagId)
        {
            return _pickupOf.TryGetValue(tagId, out var pickup) ? pickup : null;
        }

        /// <summary>
        /// <see cref="KillConfirmedSpec"/> を、<see cref="Definition"/> があればその値で、無ければ Inspector の値で作る。
        /// アセットの値が不正なら <see cref="InvalidOperationException"/>、Inspector の値が不正なら <see cref="ArgumentOutOfRangeException"/>。
        /// </summary>
        public KillConfirmedSpec CreateSpec()
        {
            if (_definition != null)
            {
                return _definition.ToSpec();
            }

            return new KillConfirmedSpec(_scoreLimit, _timeLimitSeconds, _respawnSeconds, _tagLifetimeSeconds);
        }

        /// <summary>
        /// killConfirmed で、members を参加者に killConfirmed.Session を始める (<see cref="ModeRunnerBase.Begin"/>)。
        /// 引数が不正なら <see cref="ArgumentException"/> を投げ、何も購読しない。
        /// </summary>
        public void Begin(
            KillConfirmed killConfirmed,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null,
            IModeHud hudView = null)
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
                Begin(killConfirmed.Session, members, spawnPoints, localPlayer, hudView);
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

        protected override void AppendHudTexts(List<LocalizedText> lines)
        {
            if (_killConfirmed != null)
            {
                lines.AddRange(KillConfirmedText.HudLines(_killConfirmed));
            }
        }

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

        /// <summary>
        /// 設定やフックが足した持ち主の分からないタグを外し、倒れた参加者のタグを 1 枚足す。
        /// DropOnDeath とこの Runner のどちらが先に倒れたことを受けても同じになるよう、ここではモードの状態を変えない (登録は <see cref="OnDropped"/>)。
        /// </summary>
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
                pickup.Radius = _tagRadius;
                pickup.Collected += OnTagCollected;
                _tagOf.Add(pickup, tag);
                _pickupOf.Add(tag.Id, pickup);
                Debug.Log(KillConfirmedText.DropLog(tag), pickup);
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
                Debug.Log(KillConfirmedText.DiscardLog(tag, collection.Collector != null ? collection.Collector.Id : null), this);
                _killConfirmed.Discard(tag.Id);
                return;
            }

            var result = _killConfirmed.Collect(tag.Id, collectorId);
            Debug.Log(KillConfirmedText.CollectLog(tag, collectorId, collector.Side, result), collector);

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
            Debug.Log(KillConfirmedText.ExpireLog(tag), this);
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
