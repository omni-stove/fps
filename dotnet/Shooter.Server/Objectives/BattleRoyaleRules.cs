using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が BattleRoyale のとき、分隊で戦い、縮むエリアと物資投下の中で最後に残った分隊を勝ちにする。
    // 分隊・復帰・順位はオフラインと同じ Core の BattleRoyaleSession、エリアは ZoneSchedule、物資投下は SupplyDropSchedule で決める。
    // 体力・ダウン・蘇生・出血は Session (TeamDeathmatchSession) が受け持ち、味方を分隊で数える (AreHostile) ので、
    // BattleRoyaleSession にはダウンを持たせず、倒れきったことだけを渡す。Session は自分では出直させず (RespawnsByRound)、復帰はここで出直させる
    internal sealed class BattleRoyaleRules : IMatchObjectiveRules, ISnapshotContributor
    {
        // オフラインの SupplyDropper.GroundProbeHeight・GroundProbeDistance と同じ。Core から Runtime は読めないので同じ値を持つ
        private const float _groundProbeHeight = 500f;
        private const float _groundProbeDistance = _groundProbeHeight * 2f;

        private readonly IMatchObjectiveHost _host;
        private readonly BattleRoyaleSpec _spec;
        private readonly IBattleRoyaleWorld _world;
        private readonly BattleRoyaleSession _battleRoyale;
        private readonly SupplyDropSchedule _drops;
        private readonly Random _random;
        private readonly Dictionary<int, float> _owedDamage = new Dictionary<int, float>();
        private readonly HashSet<int> _outside = new HashSet<int>();
        private readonly List<FallingCrate> _falling = new List<FallingCrate>();
        private readonly List<int> _deathBoxes = new List<int>();
        private readonly List<int> _redeploys = new List<int>();
        private readonly List<int> _scratchIds = new List<int>();
        private readonly HashSet<int> _bots = new HashSet<int>();
        private readonly Dictionary<int, BotPlan> _botPlans = new Dictionary<int, BotPlan>();
        private readonly List<ServerLootBox> _boxes = new List<ServerLootBox>();
        private readonly List<BotPickupOption> _pickups = new List<BotPickupOption>();
        private readonly List<Vector3> _crates = new List<Vector3>();
        private ServerMapData _map;
        private NetMapZoneCircle _circle;
        private float? _dropHeight;
        private ZoneSchedule _zone;
        private ZoneState _zoneState;
        private float _zoneElapsed;
        private float _damageTimer;
        private int _nextCrateNumber = 1;
        private float _now;
        private bool _started;

        // 種を渡さなければ起動の時刻から決める。エリアの中心の種は battle-royale.json の zone.seed があればそれを使う
        public BattleRoyaleRules(IMatchObjectiveHost host, BattleRoyaleSpec spec, IBattleRoyaleWorld world, int? seed = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _random = new Random(seed ?? Environment.TickCount);
            var limit = spec.HasTimeLimit ? spec.TimeLimitSeconds : float.PositiveInfinity;
            _battleRoyale = new BattleRoyaleSession(spec.SquadCount, spec.SquadSize, limit, ActionRules.Default.WithDowned(Downed.Off), spec.Redeploy);
            _drops = new SupplyDropSchedule(spec.SupplyDrops);
            // Session は試合ごとに替わりうるので、呼ばれたときに host から引く
            _battleRoyale.ExternalDowned = id =>
            {
                var session = _host.Session;
                return session != null && session.HasPlayer(id) && session.IsDowned(id);
            };
        }

        public MatchObjective Objective => MatchObjective.BattleRoyale;

        public bool UsesLoot => true;

        public BattleRoyaleSession BattleRoyale => _battleRoyale;

        public BattleRoyaleSpec Spec => _spec;

        // エリアが動いていない (UseMap の前・始まる前) なら null
        public ZoneState? Zone => _zone != null ? _zoneState : (ZoneState?)null;

        public bool PlaysInRounds => false;

        // チームのキル数は分隊の勝ち負けではないので、試合の点数をキルの点数で書き換えさせない
        public bool KeepsMatchScore => true;

        public bool HoldsFire => false;

        // 制限時間の無い試合はエリアで決着させるので、残り時間は出さない
        public float? RemainingSeconds => _spec.HasTimeLimit ? Math.Max(0f, _battleRoyale.Mode.RemainingSeconds) : 0f;

        // 行き先の無い Bot には次の円の中を巡らせる。円は段階が替わるときだけ動くので、巡る路を作り直すのもそのときだけ
        public ObjectiveRally? BotRally
        {
            get
            {
                if (!_battleRoyale.IsLive || _zone == null)
                {
                    return null;
                }

                var target = _zoneState.TargetCenter;
                return new ObjectiveRally(
                    new Vector3(target.X, _circle.Center.Y, target.Y), _zoneState.TargetRadius * BattleRoyaleBotPlanner.SafeRadiusFraction);
            }
        }

        // オフラインの BattleRoyaleRunner と同じく、ルールのダウンが Off でもダウンを使う。点数と時間は目的が見るので Session の上限は届かない値にする
        public TeamDeathmatchSpec CreateSessionSpec(GameRules rules, NetMatchSettings settings)
        {
            ArgumentNullException.ThrowIfNull(rules);
            ArgumentNullException.ThrowIfNull(settings);
            var sessionRules = rules.Actions.Downed == Downed.Revive ? rules : rules.WithActions(rules.Actions.WithDowned(Downed.Revive));
            return new TeamDeathmatchSpec(int.MaxValue, float.MaxValue, settings.RespawnSeconds, sessionRules, respawnsByRound: true);
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
            var circle = Build(points, "cannot start the " + Objective + " match on map " + map.Name + ": ");
            StopField();
            _map = map;
            _circle = circle;
            _dropHeight = points.DropHeight;
            _zone = null;
            if (_started)
            {
                _host.Log.WriteLine(BattleRoyaleRulesLog.Started(_spec, _circle, _dropHeight));
            }
        }

        public void Start()
        {
            if (_circle == null)
            {
                throw new InvalidOperationException("the " + Objective + " rules have no map; call UseMap before Start");
            }

            if (_started)
            {
                return;
            }

            _started = true;
            _battleRoyale.RedeployDecided += HandleRedeployDecided;
            _battleRoyale.SquadEliminated += HandleSquadEliminated;
            _battleRoyale.Ended += HandleEnded;
            _battleRoyale.Mode.RespawnReady += HandleRespawnReady;
            _host.Log.WriteLine(BattleRoyaleRulesLog.Started(_spec, _circle, _dropHeight));
        }

        public void Stop()
        {
            if (!_started)
            {
                return;
            }

            _started = false;
            _battleRoyale.RedeployDecided -= HandleRedeployDecided;
            _battleRoyale.SquadEliminated -= HandleSquadEliminated;
            _battleRoyale.Ended -= HandleEnded;
            _battleRoyale.Mode.RespawnReady -= HandleRespawnReady;
            StopField();
            _deathBoxes.Clear();
            _redeploys.Clear();
            _bots.Clear();
            _botPlans.Clear();
        }

        public bool Tick(bool matchPlaying, float deltaSeconds)
        {
            if (!_started || !matchPlaying)
            {
                return false;
            }

            _now += deltaSeconds;
            var mode = _battleRoyale.Mode;
            if (mode.State == GameModeState.Waiting)
            {
                BeginBattle();
            }

            // 最後の分隊が決まった Tick に倒れた人の箱も置く
            PlaceDeathBoxes();
            if (!_battleRoyale.IsLive)
            {
                return mode.State == GameModeState.Ended;
            }

            AdvanceZone(deltaSeconds);
            DamageOutside(deltaSeconds);
            AdvanceDrops(deltaSeconds);
            OpenBoxesForBots();
            _battleRoyale.Tick(deltaSeconds);
            PlaceDeathBoxes();
            Redeploy();
            return mode.State == GameModeState.Ended;
        }

        // 試合の合間にチームを移った人とマップが替わったときにも呼ばれるので、もう分隊にいる人は振り分け直さない。
        // 降下の後に入った人は、まだ立っている分隊に空きがあればそこへ入れて降ろし、無ければこの試合には入れない
        public void PlayerJoined(int playerId)
        {
            var session = _host.Session;
            var mode = _battleRoyale.Mode;
            if (session == null || !session.HasPlayer(playerId) || mode.HasPlayer(playerId))
            {
                return;
            }

            if (!_battleRoyale.IsLive)
            {
                if (!mode.NextSide.HasValue)
                {
                    _host.Log.WriteLine(BattleRoyaleRulesLog.NoSquad(playerId, false));
                    return;
                }

                _host.Log.WriteLine(BattleRoyaleRulesLog.Squad(playerId, mode.Join(playerId)));
                return;
            }

            var room = StandingSquadWithRoom();
            if (!room.HasValue)
            {
                _host.Log.WriteLine(BattleRoyaleRulesLog.NoSquad(playerId, true));
                return;
            }

            mode.AddPlayer(playerId, room.Value);
            _host.Log.WriteLine(BattleRoyaleRulesLog.Squad(playerId, room.Value));
            Deploy(playerId);
        }

        public void PlayerLeft(int playerId)
        {
            Forget(playerId);
            _deathBoxes.Remove(playerId);
            _redeploys.Remove(playerId);
            _bots.Remove(playerId);
            if (_battleRoyale.Mode.HasPlayer(playerId))
            {
                _battleRoyale.RemovePlayer(playerId);
            }
        }

        // Session はダウン中にとどめを刺されたとき・出血したとき・分隊が全滅したときにも Killed を出すので、ここに来るのは倒れきった人だけ
        public void PlayerKilled(int killerId, int victimId)
        {
            if (!_battleRoyale.IsLive || !_battleRoyale.Mode.HasPlayer(victimId))
            {
                return;
            }

            Forget(victimId);
            var attacker = killerId != victimId && _battleRoyale.Mode.HasPlayer(killerId) ? killerId : (int?)null;
            if (_battleRoyale.ReportLethal(victimId, attacker) == BattleRoyaleHit.Killed)
            {
                // 箱にするのは次の Tick。撃った弾の処理の途中で倒れた人の武器を取り上げないため
                _deathBoxes.Add(victimId);
            }
        }

        public void MatchRestarted()
        {
            StopField();
            _zone = null;
            _deathBoxes.Clear();
            _redeploys.Clear();
            _botPlans.Clear();
            _battleRoyale.Reset();
            _host.Log.WriteLine(BattleRoyaleRulesLog.Restart());
        }

        // 最後の分隊が決まる前に試合が終わった (時間・管理者の操作) なら、引き分けで順位を付け終える
        public void MatchEnded()
        {
            if (_battleRoyale.IsLive)
            {
                _battleRoyale.Mode.Finish(null);
            }

            StopField();
        }

        public RoundPhase? CurrentRoundPhase() => null;

        // 勝つのはチームでなく分隊なので、チームの勝者は出さない。勝った人は IsWinningPlayer で分隊から決める
        public Team? DecideWinner(Team? killWinner) => null;

        public bool IsWinningPlayer(int playerId, Team playerTeam, Team? winner)
        {
            var mode = _battleRoyale.Mode;
            var squad = mode.Winner;
            return mode.State == GameModeState.Ended && squad.HasValue && mode.HasPlayer(playerId) && mode.GetSide(playerId) == squad.Value;
        }

        // 分隊に入っていない人 (空きが無く外れた人) は誰の敵でもなく、撃っても撃たれても当たらない
        public bool AreHostile(int a, Team aTeam, int b, Team bTeam)
        {
            var mode = _battleRoyale.Mode;
            return a != b && mode.HasPlayer(a) && mode.HasPlayer(b) && mode.GetSide(a) != mode.GetSide(b);
        }

        // 分隊に入っていない人は誰の味方でもない。AreHostile の裏返しにすると全員の味方になり、
        // 分隊の最後の 1 人がダウンで止まり、どの分隊のダウン中の人も起こせてしまう
        public bool AreAllies(int a, Team aTeam, int b, Team bTeam)
        {
            var mode = _battleRoyale.Mode;
            return a == b || (mode.HasPlayer(a) && mode.HasPlayer(b) && mode.GetSide(a) == mode.GetSide(b));
        }

        public InteractVerdict Interact(int playerId, int itemId)
        {
            return _battleRoyale.IsLive && _battleRoyale.Mode.HasPlayer(playerId) && _battleRoyale.IsStanding(playerId)
                ? InteractVerdict.Accepted
                : InteractVerdict.Refused(InteractRefusal.CannotAct);
        }

        // 行き先はオフラインの BattleRoyaleBotPlanner (エリアの中・武器の箱・物資投下の箱) で決め、決め直すのは間をあける。
        // ダウンした味方へは向かわせないのは、Bot は蘇生の要求を出さず、着いても起こせないため
        public Vector3? BotGoal(int playerId)
        {
            var session = _host.Session;
            if (!_battleRoyale.IsLive || _zone == null || session == null || !session.HasPlayer(playerId)
                || !_battleRoyale.Mode.HasPlayer(playerId) || !session.IsAlive(playerId) || session.IsDowned(playerId)
                || !_host.Bodies.TryGet(playerId, out var body))
            {
                _botPlans.Remove(playerId);
                return null;
            }

            _bots.Add(playerId);
            var previous = _botPlans.TryGetValue(playerId, out var plan) ? plan : null;
            if (previous != null && _now < previous.NextPlanAt)
            {
                return previous.Goal;
            }

            var errand = Plan(playerId, body.Position);
            var goal = errand.HasErrand ? SteadyGoal(previous, errand) : (Vector3?)null;
            _botPlans[playerId] = new BotPlan(errand.Kind, goal, _now + _spec.BotPlanIntervalSeconds);
            if (previous == null || previous.Kind != errand.Kind)
            {
                _host.Log.WriteLine(BattleRoyaleLog.Errand(playerId, errand.Kind, errand.Destination));
            }

            return goal;
        }

        public void FillMatch(ref MatchSnapshot snapshot)
        {
            snapshot.Objective = Objective;
            snapshot.AlphaPoints = 0;
            snapshot.BravoPoints = 0;
            snapshot.HasActivePoint = false;
            snapshot.ActivePointIndex = 0;
            snapshot.ObjectiveRemainingSeconds = 0;
            if (_zone != null)
            {
                snapshot.ZoneCenter = _zoneState.Center;
                snapshot.ZoneRadius = _zoneState.Radius;
                snapshot.NextZoneCenter = _zoneState.TargetCenter;
                snapshot.NextZoneRadius = _zoneState.TargetRadius;
                snapshot.ZonePhase = _zoneState.PhaseIndex + 1;
                snapshot.ZonePhaseRemainingSeconds = HudFormat.DisplayedSeconds(_zoneState.SecondsUntilStageEnds);
            }
            else
            {
                var center = _circle != null ? _circle.HorizontalCenter : Vector2.Zero;
                var radius = _circle != null ? _circle.Radius : 0f;
                snapshot.ZoneCenter = center;
                snapshot.ZoneRadius = radius;
                snapshot.NextZoneCenter = center;
                snapshot.NextZoneRadius = radius;
                snapshot.ZonePhase = 0;
                snapshot.ZonePhaseRemainingSeconds = 0;
            }

            snapshot.SquadsRemaining = _battleRoyale.Mode.State == GameModeState.Waiting ? SquadsWithPlayers() : _battleRoyale.SquadsRemaining;
        }

        public void FillControlPoints(SnapshotList<ControlPointSnapshot> points)
        {
            ArgumentNullException.ThrowIfNull(points);
            points.Clear();
        }

        // 箱は ServerLoot が目的の物の後に足す
        public void FillObjectiveItems(SnapshotList<ObjectiveItemSnapshot> items)
        {
            ArgumentNullException.ThrowIfNull(items);
            items.Clear();
        }

        // 分隊に入っていない人も行を書き換えるのは、行を使い回すスナップショットに前の人の分隊を残さないため
        public void FillPlayer(int playerId, ref PlayerSnapshot snapshot)
        {
            var mode = _battleRoyale.Mode;
            snapshot.SquadId = mode.HasPlayer(playerId) ? (byte)mode.GetSide(playerId).Id : (byte)0;
        }

        // 戻れる回数と戻るまでの秒数は持ち主にだけ送る
        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var owner = snapshot.Owner;
            if (receiver == null || !_battleRoyale.Mode.HasPlayer(receiver.PlayerId))
            {
                owner.RedeploysLeft = 0;
                owner.RedeploySeconds = null;
                return;
            }

            var playerId = receiver.PlayerId;
            owner.RedeploysLeft = _battleRoyale.GetRedeploysLeft(playerId);
            var remaining = _battleRoyale.GetRedeployRemaining(playerId);
            owner.RedeploySeconds = remaining.HasValue ? HudFormat.DisplayedSeconds(remaining.Value) : (int?)null;
        }

        private NetMapZoneCircle Build(NetMapPoints points, string failure)
        {
            var circle = points.Zone;
            if (circle == null)
            {
                throw new InvalidOperationException(
                    failure + "it has no zone; place a ShrinkingZone in the scene and export the map points with make export-server-rules");
            }

            try
            {
                // 種は円の大きさの確かめに関わらないので何でもよい
                _ = new ZoneSchedule(circle.HorizontalCenter, circle.Radius, _spec.Zone.Phases, 0);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException(
                    failure + "the zone of radius " + circle.Radius.ToString(CultureInfo.InvariantCulture)
                    + " does not fit the zone phases of " + ServerRulesData.BattleRoyaleTuningFileName + ": " + e.Message,
                    e);
            }

            return circle;
        }

        // 始まる前に入った人を分隊に入れ終えてから始め、全員の武器を空にして降ろす
        private void BeginBattle()
        {
            var session = _host.Session;
            if (session == null)
            {
                return;
            }

            foreach (var playerId in session.GetPlayerIds())
            {
                PlayerJoined(playerId);
            }

            _battleRoyale.Start();
            _host.Log.WriteLine(BattleRoyaleRulesLog.Begin(_battleRoyale.EntrantCount, _battleRoyale.Mode.PlayerCount));
            if (_battleRoyale.EntrantCount < 2)
            {
                _host.Log.WriteLine(BattleRoyaleRulesLog.FewSquads(_battleRoyale.EntrantCount));
            }

            StartZone();
            _drops.Stop();
            _falling.Clear();
            _nextCrateNumber = 1;
            BeginDropStage(_zoneState);
            CopyPlayerIds();
            foreach (var playerId in _scratchIds)
            {
                Deploy(playerId);
            }
        }

        private void StartZone()
        {
            var seed = _spec.Zone.Seed ?? _random.Next();
            _zone = new ZoneSchedule(_circle.HorizontalCenter, _circle.Radius, _spec.Zone.Phases, seed);
            _zoneElapsed = 0f;
            _damageTimer = 0f;
            _owedDamage.Clear();
            _outside.Clear();
            _zoneState = _zone.Evaluate(0f);
            _host.Log.WriteLine(ZoneLog.Started(_zone));
            _host.Log.WriteLine(ZoneLog.Stage(_zoneState, _zone.PhaseCount));
            _battleRoyale.SetZonePhase(_zoneState.PhaseIndex);
        }

        private void AdvanceZone(float deltaSeconds)
        {
            _zoneElapsed += deltaSeconds;
            var previous = _zoneState;
            _zoneState = _zone.Evaluate(_zoneElapsed);
            if (_zoneState.PhaseIndex != previous.PhaseIndex || _zoneState.Stage != previous.Stage)
            {
                _host.Log.WriteLine(ZoneLog.Stage(_zoneState, _zone.PhaseCount));
                BeginDropStage(_zoneState);
            }

            _battleRoyale.SetZonePhase(_zoneState.PhaseIndex);
        }

        // オフラインの ShrinkingZone と同じく、間隔ごとにその段階の毎秒のダメージを間隔の分だけ与え、1 に満たない端数は人ごとに持ち越す
        private void DamageOutside(float deltaSeconds)
        {
            var interval = _spec.Zone.DamageIntervalSeconds;
            _damageTimer += deltaSeconds;
            while (_damageTimer >= interval && _battleRoyale.IsLive)
            {
                _damageTimer -= interval;
                HurtOutside(_zoneState.DamagePerSecond * interval);
            }
        }

        private void HurtOutside(float amount)
        {
            var session = _host.Session;
            CopyPlayerIds();
            foreach (var playerId in _scratchIds)
            {
                // 前の人へのダメージで決着したら、残りには触らない
                if (!_battleRoyale.IsLive)
                {
                    return;
                }

                ServerBody body = null;
                var counts = session != null && session.HasPlayer(playerId) && session.IsAlive(playerId)
                    && _host.Bodies.TryGet(playerId, out body) && body.HasSpawned;
                var inside = !counts || _zoneState.Contains(new Vector2(body.Position.X, body.Position.Z));
                if (inside)
                {
                    if (_outside.Remove(playerId))
                    {
                        _owedDamage.Remove(playerId);
                        if (counts)
                        {
                            _host.Log.WriteLine(ZoneLog.Inside(Id(playerId)));
                        }
                    }

                    continue;
                }

                if (_outside.Add(playerId))
                {
                    _host.Log.WriteLine(ZoneLog.Outside(Id(playerId)));
                }

                _owedDamage.TryGetValue(playerId, out var owed);
                owed += amount;
                var whole = (int)Math.Floor(owed);
                _owedDamage[playerId] = owed - whole;
                if (whole > 0)
                {
                    var outcome = _world.HurtByZone(playerId, whole);
                    _host.Log.WriteLine(BattleRoyaleRulesLog.ZoneDamage(playerId, whole, outcome));
                }
            }
        }

        // 次の円が決まったら、その段階の数を数え始める。最後の円の後は次の円が無いので落とすのをやめる (オフラインの SupplyDropper と同じ)
        private void BeginDropStage(ZoneState state)
        {
            if (state.Stage == ZoneStage.Closed)
            {
                _drops.Stop();
                return;
            }

            if (state.PhaseIndex == _drops.StageIndex)
            {
                return;
            }

            var count = _drops.BeginStage(state.PhaseIndex);
            _host.Log.WriteLine(SupplyDropLog.Stage(state.PhaseIndex, count));
        }

        // 箱は落とす高さから落ちる速さで着くまでの間を待ってから置く。サーバーは落ちている箱を送らないので、着いた所に開けられる箱を置く
        private void AdvanceDrops(float deltaSeconds)
        {
            if (!_battleRoyale.IsLive)
            {
                return;
            }

            var index = 0;
            while (index < _falling.Count)
            {
                var crate = _falling[index];
                crate.Remaining -= deltaSeconds;
                if (crate.Remaining > 0f)
                {
                    index++;
                    continue;
                }

                _falling.RemoveAt(index);
                var itemId = _world.SpawnCrate(crate.Landing, _spec.SupplyDrops.TableId, _spec.SupplyDrops.Reach);
                _host.Log.WriteLine(SupplyDropLog.Landed(crate.Number, crate.Landing));
                _host.Log.WriteLine(BattleRoyaleRulesLog.CrateLanded(crate.Number, itemId));
            }

            var due = _drops.Tick(deltaSeconds);
            for (var i = 0; i < due; i++)
            {
                DropCrate();
            }
        }

        private void DropCrate()
        {
            var spec = _spec.SupplyDrops;
            var point = SupplyDropSchedule.PickPoint(_zoneState.TargetCenter, _zoneState.TargetRadius, spec.PlaceFraction, _random);
            var number = _nextCrateNumber++;
            var probe = new Vector3(point.X, _circle.Center.Y, point.Y);
            if (!TryFindGround(probe, out var ground))
            {
                ground = _circle.Center.Y;
                _host.Log.WriteLine(SupplyDropLog.NoGround(number, probe));
            }

            var landing = new Vector3(point.X, ground, point.Y);
            _host.Log.WriteLine(SupplyDropLog.Dropped(number, landing + new Vector3(0f, spec.DropHeight, 0f), ground));
            _falling.Add(new FallingCrate(number, landing, spec.DropHeight / spec.FallSpeed));
        }

        private bool TryFindGround(Vector3 point, out float height)
        {
            var origin = new Vector3(point.X, point.Y + _groundProbeHeight, point.Z);
            if (_map != null && _map.World.Raycast(origin, -Vector3.UnitY, _groundProbeDistance, ServerHitboxes.RaycastLayers, out var hit))
            {
                height = hit.Point.Y;
                return true;
            }

            height = point.Y;
            return false;
        }

        // 武器を空にして、地点ファイルの降ろす高さから降ろす。高さの無いマップは地面から始める
        private void Deploy(int playerId)
        {
            _world.Disarm(playerId);
            if (!_dropHeight.HasValue)
            {
                _host.Log.WriteLine(BattleRoyaleRulesLog.Ground(playerId));
                return;
            }

            if (!_host.Bodies.TryGet(playerId, out var body))
            {
                return;
            }

            var height = _dropHeight.Value;
            var from = body.Position + new Vector3(0f, height, 0f);
            var placed = _world.Drop(playerId, from);
            _host.Log.WriteLine(BattleRoyaleRulesLog.Drop(playerId, height, from, placed));
        }

        private void PlaceDeathBoxes()
        {
            if (_deathBoxes.Count == 0)
            {
                return;
            }

            _scratchIds.Clear();
            _scratchIds.AddRange(_deathBoxes);
            _deathBoxes.Clear();
            foreach (var playerId in _scratchIds)
            {
                _world.SpawnDeathBox(playerId);
            }
        }

        // 戻る待ちを終えた人を Session で出直させ、出た所から降ろす。出直しで配られたロードアウトは降ろす前に手放させる
        private void Redeploy()
        {
            if (_redeploys.Count == 0)
            {
                return;
            }

            _scratchIds.Clear();
            _scratchIds.AddRange(_redeploys);
            _redeploys.Clear();
            var session = _host.Session;
            foreach (var playerId in _scratchIds)
            {
                if (!_battleRoyale.IsLive || session == null || !session.HasPlayer(playerId) || !_battleRoyale.Mode.HasPlayer(playerId)
                    || !session.RespawnPlayer(playerId))
                {
                    continue;
                }

                _host.Log.WriteLine(BattleRoyaleRulesLog.Redeployed(playerId, _battleRoyale.GetRedeploysLeft(playerId)));
                Deploy(playerId);
            }
        }

        // 人の開ける要求と同じく ServerLoot に通すので、届くか・開けられるかの判断は同じになる
        private void OpenBoxesForBots()
        {
            if (_bots.Count == 0)
            {
                return;
            }

            _boxes.Clear();
            _world.CollectUnopenedBoxes(_boxes);
            if (_boxes.Count == 0)
            {
                return;
            }

            var session = _host.Session;
            _scratchIds.Clear();
            _scratchIds.AddRange(_bots);
            foreach (var playerId in _scratchIds)
            {
                if (session == null || !session.HasPlayer(playerId) || !_battleRoyale.Mode.HasPlayer(playerId) || !_battleRoyale.IsStanding(playerId)
                    || !session.IsAlive(playerId) || session.IsDowned(playerId) || !_host.Bodies.TryGet(playerId, out var body))
                {
                    continue;
                }

                for (var i = 0; i < _boxes.Count; i++)
                {
                    var box = _boxes[i];
                    if (HorizontalDistanceSquared(box.Position, body.Position) > box.Reach * box.Reach)
                    {
                        continue;
                    }

                    if (_world.Open(playerId, box.ItemId).IsAccepted)
                    {
                        _boxes.RemoveAt(i);

                        // 中身で武器を持ったら行き先が変わるので、次に聞かれたときに決め直す
                        _botPlans.Remove(playerId);
                        break;
                    }

                    // 高さや見通しで断られた箱で止まると、次の箱を開けられなくなる
                }
            }
        }

        private BotErrand Plan(int playerId, Vector3 position)
        {
            _boxes.Clear();
            _world.CollectUnopenedBoxes(_boxes);
            _pickups.Clear();
            _crates.Clear();
            foreach (var box in _boxes)
            {
                if (box.Kind == ObjectiveItemKind.SupplyCrate)
                {
                    _crates.Add(box.Position);
                }
                else
                {
                    _pickups.Add(new BotPickupOption(box.Position, box.HasWeapon));
                }
            }

            return BattleRoyaleBotPlanner.Plan(
                position,
                _zoneState,
                _world.IsArmed(playerId),
                _pickups,
                Array.Empty<Vector3>(),
                _spec.BotSearchRadius,
                _crates,
                _spec.SupplyDrops.BotSearchRadius);
        }

        // エリアへ入る先は今の位置から毎回作り直されるので、前の先がまだ次の円の中なら同じ先を返し、Bot の巡る路を作り直させない
        private Vector3 SteadyGoal(BotPlan previous, BotErrand errand)
        {
            if (errand.Kind == BotErrandKind.EnterZone && previous != null && previous.Kind == BotErrandKind.EnterZone && previous.Goal.HasValue
                && BattleRoyaleBotPlanner.InTarget(_zoneState, previous.Goal.Value))
            {
                return previous.Goal.Value;
            }

            return errand.Destination;
        }

        private Side? StandingSquadWithRoom()
        {
            Side? best = null;
            var bestCount = int.MaxValue;
            for (var i = 0; i < _spec.SquadCount; i++)
            {
                var squad = new Side(i);
                var members = _battleRoyale.Mode.CountMembers(squad);
                if (members >= _spec.SquadSize || _battleRoyale.GetPlacement(squad).HasValue || _battleRoyale.CountRemaining(squad) == 0)
                {
                    continue;
                }

                if (members < bestCount)
                {
                    best = squad;
                    bestCount = members;
                }
            }

            return best;
        }

        private int SquadsWithPlayers()
        {
            var count = 0;
            for (var i = 0; i < _spec.SquadCount; i++)
            {
                if (_battleRoyale.Mode.CountMembers(new Side(i)) > 0)
                {
                    count++;
                }
            }

            return count;
        }

        // 回している最中に倒れた人・抜けた人で並びが崩れないよう、写しを回す
        private void CopyPlayerIds()
        {
            _scratchIds.Clear();
            _scratchIds.AddRange(_battleRoyale.Mode.GetPlayerIds());
            _scratchIds.Sort();
        }

        private void Forget(int playerId)
        {
            _outside.Remove(playerId);
            _owedDamage.Remove(playerId);
            _botPlans.Remove(playerId);
        }

        // エリアと物資投下を止める。最後の円はスナップショットに残すので _zone は次の試合まで持つ
        private void StopField()
        {
            _drops.Stop();
            _falling.Clear();
            _outside.Clear();
            _owedDamage.Clear();
            _damageTimer = 0f;
        }

        private void HandleRedeployDecided(int playerId, RedeployVerdict verdict)
        {
            if (verdict == RedeployVerdict.Disabled)
            {
                return;
            }

            var seconds = verdict == RedeployVerdict.Waiting ? _battleRoyale.RedeployRules.DelaySeconds : (float?)null;
            _host.Log.WriteLine(BattleRoyaleLog.Redeploy(
                playerId, verdict, _battleRoyale.ZonePhaseIndex, _battleRoyale.GetRedeploysLeft(playerId), seconds));
        }

        private void HandleSquadEliminated(Side squad, int place)
        {
            _host.Log.WriteLine(BattleRoyaleLog.Eliminated(squad, place, _battleRoyale.SquadsRemaining));
        }

        // 倒れた Tick のうちに戻れる (待ちが 0 秒) こともあるので、Session の出来事の途中で出直させず、Tick の終わりにまとめて出直させる
        private void HandleRespawnReady(int playerId)
        {
            if (!_redeploys.Contains(playerId))
            {
                _redeploys.Add(playerId);
            }
        }

        private void HandleEnded(Side? winner)
        {
            StopField();
            _host.Log.WriteLine(BattleRoyaleLog.Placements(_battleRoyale));
            _host.Log.WriteLine(BattleRoyaleRulesLog.Ended(_battleRoyale));
        }

        private static string Id(int playerId) => playerId.ToString(CultureInfo.InvariantCulture);

        private static float HorizontalDistanceSquared(Vector3 a, Vector3 b)
        {
            var dx = a.X - b.X;
            var dz = a.Z - b.Z;
            return (dx * dx) + (dz * dz);
        }

        private sealed class FallingCrate
        {
            public FallingCrate(int number, Vector3 landing, float remaining)
            {
                Number = number;
                Landing = landing;
                Remaining = remaining;
            }

            public int Number { get; }

            public Vector3 Landing { get; }

            public float Remaining { get; set; }
        }

        private sealed class BotPlan
        {
            public BotPlan(BotErrandKind kind, Vector3? goal, float nextPlanAt)
            {
                Kind = kind;
                Goal = goal;
                NextPlanAt = nextPlanAt;
            }

            public BotErrandKind Kind { get; }

            public Vector3? Goal { get; }

            public float NextPlanAt { get; }
        }
    }
}
