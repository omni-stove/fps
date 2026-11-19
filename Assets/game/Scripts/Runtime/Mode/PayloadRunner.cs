using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // MovingPlatform と同じく PlayerController より先に台車を動かし、同じフレームのうちに乗っている体を運ばせる。
    [DefaultExecutionOrder(MovingPlatform.ExecutionOrder)]
    public sealed class PayloadRunner : ModeRunnerBase
    {
        public const int DefaultAttackerSideId = 0;
        public const float DefaultRespawnSeconds = 5f;

        // 動く台車へ bot の巡回路を毎フレーム引き直すと NavMesh の経路を探し直し続けるので、台車がこの割合 (範囲の半径に対して) だけ動いたら引き直す。
        public const float RerouteRadiusFraction = 0.5f;

        [SerializeField, Tooltip("攻め側・戻る秒・台車の調整値のアセット。設定すれば下の値の代わりにこれを使う")]
        private PayloadDefinition _definition;

        [SerializeField, Tooltip("押す台車。空ならシーンの PayloadCart を使う")]
        private PayloadCart _cart;

        [SerializeField, Min(0), Tooltip("台車を押す側 (攻め側) の陣営の ID。0 = Alpha, 1 = Bravo。もう一方が守り側")]
        private int _attackerSideId = DefaultAttackerSideId;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _respawnSeconds = DefaultRespawnSeconds;

        private readonly List<Damageable> _candidates = new List<Damageable>();

        // ModeSession は Tick の終わりに残り時間を減らすので、通った中継点は Tick の後で延びた後の残り時間と一緒に書く。
        private readonly List<int> _reachedCheckpoints = new List<int>();
        private Payload _payload;
        private PayloadCart _activeCart;
        private string _loggedMotion;
        private int _loggedPushers = -1;
        private Vector3? _routedAt;

        public Payload Payload => _payload;

        public PayloadCart Cart => _activeCart;

        public PayloadDefinition Definition
        {
            get => _definition;
            set => _definition = value;
        }

        public PayloadCart ConfiguredCart
        {
            get => _cart;
            set => _cart = value;
        }

        public int AttackerSideId
        {
            get => _attackerSideId;
            set => _attackerSideId = value;
        }

        public float RespawnSeconds
        {
            get => _respawnSeconds;
            set => _respawnSeconds = value;
        }

        public static PayloadSpec Tuning => KitTuning.Payload;

        public static Payload CreatePayload(PayloadSpec spec, PayloadRoute route, int attackerSideId, float respawnSeconds)
        {
            if (attackerSideId != Side.Alpha.Id && attackerSideId != Side.Bravo.Id)
            {
                throw new ArgumentOutOfRangeException(nameof(attackerSideId), attackerSideId, "attackerSideId must be 0 (Alpha) or 1 (Bravo)");
            }

            return new Payload(spec, route, new Side(attackerSideId), respawnSeconds);
        }

        public Payload CreatePayload(PayloadRoute route)
        {
            var spec = Tuning;
            return _definition != null
                ? _definition.CreatePayload(route, spec)
                : CreatePayload(spec, route, _attackerSideId, _respawnSeconds);
        }

        public void Begin(
            Payload payload,
            PayloadCart cart,
            IReadOnlyList<SideMember> members,
            IReadOnlyList<SideSpawnPoint> spawnPoints,
            SideMember localPlayer = null)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            if (cart == null)
            {
                throw new ArgumentNullException(nameof(cart));
            }

            if (IsStarted)
            {
                throw new InvalidOperationException($"{nameof(PayloadRunner)} '{name}' has already started a mode");
            }

            if (!ReferenceEquals(cart.Route, payload.Route))
            {
                throw new ArgumentException($"the payload must run on the route of PayloadCart '{cart.name}'; build it from cart.BuildRoute()", nameof(payload));
            }

            Attach(payload, cart);
            try
            {
                Begin(payload.Session, members, spawnPoints, localPlayer);
            }
            catch
            {
                Detach();
                throw;
            }
        }

        protected override ModeSession CreateSession()
        {
            var cart = _cart != null ? _cart : FindAnyObjectByType<PayloadCart>();
            if (cart == null)
            {
                throw new InvalidOperationException("シーンに PayloadCart がありません。台車を置き、道筋の点を並べてください。");
            }

            var payload = CreatePayload(cart.BuildRoute());
            Attach(payload, cart);
            return payload.Session;
        }

        protected override void OnBegan()
        {
            if (_payload != null)
            {
                Debug.Log(PayloadLog.Started(_payload), this);
            }
        }

        protected override void TickSession(ModeSession session, float deltaSeconds)
        {
            if (_payload == null)
            {
                session.Tick(deltaSeconds);
                return;
            }

            UpdatePresence();
            session.Tick(deltaSeconds);

            // Tick の中で Stop されたら、外した台車を動かさない。
            if (_payload == null)
            {
                return;
            }

            _activeCart.MoveTo(_payload.Distance, deltaSeconds);
            foreach (var checkpoint in _reachedCheckpoints)
            {
                Debug.Log(PayloadLog.Checkpoint(_payload, checkpoint), this);
            }

            _reachedCheckpoints.Clear();
            ShowMotion();
            RouteBotsIfMoved();
        }

        protected override object HudDetail => _payload != null ? PayloadStatus.Of(_payload) : null;

        protected override void OnEnded(Side? winner)
        {
            if (_payload != null)
            {
                Debug.Log(PayloadLog.Ended(_payload, winner), this);
            }
        }

        protected override void OnStop()
        {
            Detach();
        }

        private void Attach(Payload payload, PayloadCart cart)
        {
            Detach();
            _payload = payload;
            _activeCart = cart;
            _payload.CheckpointReached += OnCheckpointReached;

            var zone = cart.Zone;
            if (zone != null)
            {
                // 円盤の既定はオフラインの Alpha から見た色なので、陣営を変えたプレイヤーには自分の陣営から見た色にする
                zone.ViewerTeam = PlayerSide.TryGetTeam(out var playerTeam) ? playerTeam : (Team?)null;
                zone.SetHighlight(true);
            }

            ShowZone();
        }

        private void Detach()
        {
            if (_payload != null)
            {
                _payload.CheckpointReached -= OnCheckpointReached;
            }

            _payload = null;
            _activeCart = null;
            _reachedCheckpoints.Clear();
            _loggedMotion = null;
            _loggedPushers = -1;
            _routedAt = null;
        }

        private void UpdatePresence()
        {
            var zone = _activeCart.Zone;
            var members = Members;
            _candidates.Clear();
            foreach (var member in members)
            {
                _candidates.Add(member != null ? member.Damageable : null);
            }

            if (zone == null)
            {
                return;
            }

            zone.Scan(_candidates);
            var occupants = zone.Occupants;
            for (var id = 0; id < members.Count; id++)
            {
                var damageable = _candidates[id];
                _payload.SetNearCart(id, damageable != null && Contains(occupants, damageable));
            }
        }

        private void ShowMotion()
        {
            var motion = PayloadLog.MotionName(_payload);
            var pushers = _payload.Pushers;
            // 守り側の出入りだけでも持ち主の色が変わるので、ログの変化とは別に毎回塗り直す
            ShowZone();
            if (motion == _loggedMotion && pushers == _loggedPushers)
            {
                return;
            }

            _loggedMotion = motion;
            _loggedPushers = pushers;
            Debug.Log(PayloadLog.Motion(_payload), this);
        }

        private void ShowZone()
        {
            var zone = _activeCart != null ? _activeCart.Zone : null;
            if (zone == null || _payload == null)
            {
                return;
            }

            Side? holder = null;
            var attackers = _payload.Pushers;
            var defenders = _payload.CountNearCart(_payload.Defenders);
            if (attackers > 0 && defenders == 0)
            {
                holder = _payload.Attackers;
            }
            else if (defenders > 0 && attackers == 0)
            {
                holder = _payload.Defenders;
            }

            zone.ShowOwner(Side.ToTeam(holder));
        }

        private void OnCheckpointReached(int checkpoint)
        {
            _reachedCheckpoints.Add(checkpoint);
        }

        private void RouteBotsIfMoved()
        {
            var zone = _activeCart.Zone;
            if (zone == null)
            {
                return;
            }

            var center = _activeCart.transform.position;
            if (_routedAt.HasValue && Vector3.Distance(_routedAt.Value, center) < zone.Radius * RerouteRadiusFraction)
            {
                return;
            }

            var slot = 0;
            foreach (var member in Members)
            {
                var agent = member != null ? member.GetComponent<EnemyAgent>() : null;
                if (agent == null)
                {
                    continue;
                }

                agent.SetPatrolRoute(HardpointRunner.RouteAround(center, zone.Radius, slot));
                slot++;
            }

            _routedAt = center;
        }

        private static bool Contains(IReadOnlyList<Damageable> list, Damageable item)
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i] == item)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
