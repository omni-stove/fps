using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が Payload のとき、攻め側が台車を道筋の終点まで押し切るか、守り側が時間まで止めるかで勝敗を決める。
    // 台車の進み・中継点・戻り・時間切れはオフラインと同じ Core の Payload に任せ、ここは体の位置を台車のそばにいるかどうかに直して渡すだけにする。
    // Session (TeamDeathmatchSession) は体力・出現だけを受け持ち、キル数でも時間でも先に終わらないよう上限を届かない値にする
    internal sealed class PayloadRules : IMatchObjectiveRules
    {
        // 台車は 1 台なので、スナップショットの物の Id は 1 つに決めて送る
        public const int CartItemId = 0;

        private readonly NetMatchSettings _settings;
        private readonly IMatchObjectiveHost _host;

        // Payload は Tick の中で中継点を知らせ、残り時間はその後で減るので、延びた後の残り時間と一緒に Tick の後で書く (PayloadRunner と同じ)
        private readonly List<int> _reachedCheckpoints = new List<int>();
        private Payload _payload;
        private NetMapPayload _cart;
        private bool _started;
        private string _loggedMotion;
        private int _loggedPushers = -1;

        public PayloadRules(NetMatchSettings settings, IMatchObjectiveHost host)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _host = host ?? throw new ArgumentNullException(nameof(host));
        }

        public MatchObjective Objective => MatchObjective.Payload;

        // UseMap の前は null
        public Payload Payload => _payload;

        public bool PlaysInRounds => false;

        public bool KeepsMatchScore => false;

        public bool HoldsFire => false;

        public float? RemainingSeconds => _payload != null ? Math.Max(0f, _payload.Session.RemainingSeconds) : null;

        // UseMap の前は null
        public Vector3? CartPosition => _payload != null ? _payload.Route.PositionAt(_payload.Distance) : (Vector3?)null;

        // 台車のそばを巡らせ、攻め側には押させ、守り側には止めさせる
        public ObjectiveRally? BotRally => _payload != null ? new ObjectiveRally(CartPosition.Value, _cart.Radius) : (ObjectiveRally?)null;

        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, settings.RespawnSeconds, rules);
        }

        public void CheckMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            Build(points, "cannot change to map " + map.Name + " for the " + Objective + " match: ");
        }

        public void UseMap(ServerMapData map, NetMapPoints points)
        {
            ArgumentNullException.ThrowIfNull(map);
            ArgumentNullException.ThrowIfNull(points);
            var built = Build(points, "cannot start the " + Objective + " match on map " + map.Name + ": ");
            var started = _started;
            if (started)
            {
                Stop();
            }

            _cart = built.Cart;
            _payload = built.Payload;
            ForgetLogged();
            if (started)
            {
                Start();
            }
        }

        public void Start()
        {
            if (_payload == null)
            {
                throw new InvalidOperationException("the " + Objective + " rules have no map; call UseMap before Start");
            }

            if (_started)
            {
                return;
            }

            _started = true;
            _payload.CheckpointReached += HandleCheckpointReached;
            _host.Log.WriteLine(PayloadRulesLog.Started(_payload, _cart));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _payload.CheckpointReached -= HandleCheckpointReached;
            _reachedCheckpoints.Clear();
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (_payload == null || !matchPlaying)
            {
                return false;
            }

            var session = _payload.Session;
            if (session.State == GameModeState.Waiting)
            {
                session.Start();
            }

            if (session.State != GameModeState.Playing)
            {
                return false;
            }

            LocatePlayers();
            session.Tick(deltaSeconds);
            foreach (var checkpoint in _reachedCheckpoints)
            {
                _host.Log.WriteLine(PayloadRulesLog.Checkpoint(_payload, checkpoint));
            }

            _reachedCheckpoints.Clear();
            LogMotion();
            return session.State == GameModeState.Ended;
        }

        // チームを替えて入り直した人も来るので、陣営が違えば入れ直す
        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            if (_payload == null || session == null || !session.HasPlayer(playerId))
            {
                return;
            }

            var side = Side.FromTeam(session.GetTeam(playerId));
            var modeSession = _payload.Session;
            if (modeSession.HasPlayer(playerId))
            {
                if (modeSession.GetSide(playerId) == side)
                {
                    return;
                }

                RemovePlayer(playerId);
            }

            modeSession.AddPlayer(playerId, side);
            _payload.SetNearCart(playerId, false);
        }

        public void PlayerLeft(int playerId)
        {
            if (_payload != null && _payload.Session.HasPlayer(playerId))
            {
                RemovePlayer(playerId);
            }
        }

        // 次の Tick で位置を見直すまでの間も、倒れた人をスナップショットの押している人数に数えない
        public void PlayerKilled(int killerId, int victimId)
        {
            if (_payload != null && _payload.Session.HasPlayer(victimId))
            {
                _payload.SetNearCart(victimId, false);
            }
        }

        public void MatchRestarted()
        {
            if (_payload == null)
            {
                return;
            }

            _payload.Reset();
            _reachedCheckpoints.Clear();
            ForgetLogged();
            _host.Log.WriteLine(PayloadRulesLog.Restart());
        }

        public void MatchEnded()
        {
            if (_payload != null)
            {
                _host.Log.WriteLine(PayloadRulesLog.State(_payload));
            }
        }

        public RoundPhase? CurrentRoundPhase() => null;

        // 押し切る前に試合が終わったなら、時間切れと同じく守り側の勝ちにする
        public Team? DecideWinner(Team? killWinner)
        {
            if (_payload == null)
            {
                return killWinner;
            }

            var session = _payload.Session;
            if (session.State == GameModeState.Ended)
            {
                return Side.ToTeam(session.Winner);
            }

            return Side.ToTeam(_payload.IsDelivered ? _payload.Attackers : _payload.Defenders);
        }

        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = 0;
            snapshot.BravoPoints = 0;
            snapshot.HasActivePoint = false;
            snapshot.ActivePointIndex = 0;
            snapshot.ObjectiveRemainingSeconds = 0;
            if (_payload == null)
            {
                snapshot.PayloadProgress = 0f;
                snapshot.PayloadCheckpointsPassed = 0;
                snapshot.PayloadPushers = 0;
                return;
            }

            snapshot.PayloadProgress = _payload.Progress;
            snapshot.PayloadCheckpointsPassed = _payload.CheckpointsReached;
            snapshot.PayloadPushers = _payload.Pushers;
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
        }

        // 台車は誰も運ばないので、道筋の上にある間はいつも Home で送り、陣営は押す側にする
        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
            if (_payload == null)
            {
                return;
            }

            ref var item = ref items.Add();
            item.ItemId = CartItemId;
            item.Present = ObjectiveItemFields.All;
            item.Kind = ObjectiveItemKind.Cart;
            item.Team = _payload.Attackers.ToTeam();
            item.HasOwner = false;
            item.OwnerId = 0;
            item.Position = CartPosition.Value;
            item.State = ObjectiveItemState.Home;
            item.HasCarrier = false;
            item.CarrierId = 0;
        }

        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
        }

        private (NetMapPayload Cart, Payload Payload) Build(NetMapPoints points, string failure)
        {
            var cart = points.Payload;
            if (cart == null)
            {
                throw new InvalidOperationException(
                    failure + "it has no payload route; place a PayloadCart with its route in the scene and export the map points with make export-server-rules");
            }

            try
            {
                return (cart, new Payload(_settings.Payload, cart.Route, Side.FromTeam(cart.Attackers), _settings.RespawnSeconds));
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException(failure + e.Message, e);
            }
        }

        // 倒れている人・ダウン中の人・まだ出ていない人は台車のそばに数えない (HardpointRules と同じ)
        private void LocatePlayers()
        {
            var session = _host.Session;
            var area = _cart.AreaAt(_payload.Distance);
            foreach (var body in _host.Bodies.All)
            {
                var playerId = body.PlayerId;
                if (!_payload.Session.HasPlayer(playerId))
                {
                    continue;
                }

                var counts = body.HasSpawned
                    && session != null
                    && session.HasPlayer(playerId)
                    && session.IsAlive(playerId)
                    && !session.IsDowned(playerId);
                _payload.SetNearCart(playerId, counts && area.Contains(body.Position));
            }
        }

        private void RemovePlayer(int playerId)
        {
            _payload.SetNearCart(playerId, false);
            _payload.Session.RemovePlayer(playerId);
        }

        private void LogMotion()
        {
            var motion = PayloadLog.MotionName(_payload);
            var pushers = _payload.Pushers;
            if (motion == _loggedMotion && pushers == _loggedPushers)
            {
                return;
            }

            _loggedMotion = motion;
            _loggedPushers = pushers;
            _host.Log.WriteLine(PayloadRulesLog.Motion(_payload));
        }

        private void ForgetLogged()
        {
            _loggedMotion = null;
            _loggedPushers = -1;
        }

        private void HandleCheckpointReached(int checkpoint)
        {
            _reachedCheckpoints.Add(checkpoint);
        }
    }
}
