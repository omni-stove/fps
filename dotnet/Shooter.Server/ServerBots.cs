using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // Bot を接続の無いプレイヤーとして ServerMatch に入れ、人と同じ入力 (InputFrame) を移動のモジュールに、
    // 射撃を戦闘のモジュールの人と同じ検査に渡すのは、ルール・戦闘・スナップショット・クライアントの予測が Bot を人と分けずに扱えるようにするため。
    internal sealed partial class ServerBots : IServerModule, ISnapshotContributor
    {
        public const string LogTag = "[Bot]";

        public const float PositionLogIntervalSeconds = 2f;

        private readonly MatchRulesModule _rules;
        private readonly ServerMovement _movement;
        private readonly ServerCombat _combat;
        private readonly ServerDeployables _deployables;
        private readonly ServerMelee _melee;
        private readonly List<ServerDecoy> _decoys = new List<ServerDecoy>();
        private readonly SortedDictionary<int, ServerBotAgent> _agents = new SortedDictionary<int, ServerBotAgent>();
        private readonly List<int> _ids = new List<int>();
        private readonly Queue<Team> _trimRequests = new Queue<Team>();
        private readonly List<Vector3> _patrolPoints = new List<Vector3>();
        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private ServerBodies _bodies;
        private NetBotSpec _spec;
        private ServerBotNavigation _navigation;
        private ServerBotCoverProbe _coverProbe;
        private bool _hasNavMesh;
        private bool _laddersOn;
        private int _launchBots;
        private int _target;
        private int _nextBotId = BotRoster.FirstBotPlayerId;
        private int _loggedDesired = -1;
        private bool _unavailable;

        // null なら Bot は出現地点を巡っている
        private Vector3? _ralliedAt;

        // deployables が null なら、囮に気を引かれない (置いた物の無いサーバー)。melee が null なら殴らない (近接の無いサーバー)
        public ServerBots(
            MatchRulesModule rules, ServerMovement movement, ServerCombat combat, ServerDeployables deployables = null, ServerMelee melee = null)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _deployables = deployables;
            _melee = melee;
        }

        public string Name => "bots";

        public int Count => _agents.Count;

        public int Target => _target;

        public IEnumerable<int> BotIds => _agents.Keys;

        internal ServerBotNavigation Navigation => _navigation;

        internal bool TryGetAgent(int playerId, out ServerBotAgent agent)
        {
            return _agents.TryGetValue(playerId, out agent);
        }

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _match = context.Match;
            _bodies = context.Bodies;
            var data = context.Rules ?? throw new InvalidOperationException("no match rules are loaded; start the server with " + ServerLaunch.RulesFlag);
            _spec = data.Bots;
            _launchBots = context.Options.Bots;
            _target = BotRoster.TargetPlayers(_launchBots, data.Rules.MinPlayers);
            _log.WriteLine(LogTag + " fill target=" + Text(_target) + " bots=" + Text(_launchBots) + " minPlayers=" + Text(data.Rules.MinPlayers));
            _laddersOn = data.Rules.Actions.Ladder == Ladder.On;
            UseMap(context.Map);

            _match.PlayerJoined += HandlePlayerJoined;
            _match.PlayerLeft += HandlePlayerLeft;
            _match.MapChanged += HandleMapChanged;
            StartArchetypes();
        }

        private void UseMap(ServerMapData map)
        {
            _patrolPoints.Clear();
            if (map.Points != null)
            {
                foreach (var point in map.Points.SpawnPoints)
                {
                    _patrolPoints.Add(point.Position);
                }
            }

            // 戦闘のモジュールはマップを替えると建物を作り直すので、その時の建物に問い合わせる
            _navigation = new ServerBotNavigation(
                map.Navigation,
                map.NavMesh.Links,
                map.Ladders.Ladders,
                map.World,
                colliderId => _combat.Structures != null && _combat.Structures.IsPiece(colliderId),
                _spec.NavSampleRadius,
                _bodies.Geometry.Standing.Height * 0.5f,
                _laddersOn);
            var geometry = _bodies.Geometry;
            _coverProbe = new ServerBotCoverProbe(
                PlaceCover,
                IsWorldBlocked,
                -geometry.Standing.BottomY,
                geometry.Crouched.EyeY - geometry.Crouched.BottomY,
                geometry.Standing.EyeY - geometry.Standing.BottomY);
            _hasNavMesh = HasNavMeshNearPatrolPoints();
            if (_target == 0 && !ObjectiveDrivesBots)
            {
                return;
            }

            if (!_hasNavMesh)
            {
                _log.WriteLine(LogTag + " no NavMesh near the spawn points of " + map.Name
                    + "; bots walk straight to their goals. Export " + map.Name + Shooter.Core.Navigation.NavMeshFile.Extension
                    + " with make export-server-data");
            }

            if (_navigation.UnmatchedLadderLinks > 0)
            {
                _log.WriteLine(LogTag + " " + Text(_navigation.UnmatchedLadderLinks) + " ladder link(s) of " + map.Name
                    + " have no ladder in " + map.Name + NetMapLadders.Extension + "; bots try to walk across them");
            }
        }

        // 判断と経路は前のマップの NavMesh と巡回地点を持っているので、同じ Id のまま作り直す (試合からは抜かない)
        private void HandleMapChanged(ServerMapData map)
        {
            UseMap(map);
            _ids.Clear();
            _ids.AddRange(_agents.Keys);
            foreach (var playerId in _ids)
            {
                _agents[playerId] = NewAgent(playerId);
            }

            _ralliedAt = null;
            _log.WriteLine(LogTag + " map=" + map.Name + " bots=" + Text(_agents.Count) + " patrolPoints=" + Text(_patrolPoints.Count)
                + " navMesh=" + (_hasNavMesh ? "yes" : "no"));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            if (_target > 0 || ObjectiveDrivesBots)
            {
                Reconcile();
            }

            DetonateFallen();
            if (_agents.Count == 0)
            {
                return;
            }

            // 行き先を先に決めるのは、Rally が行き先を持つ Bot の路を上書きしないよう、この tick の行き先で見分けるため
            Goals();
            Rally();
            _ids.Clear();
            _ids.AddRange(_agents.Keys);
            foreach (var playerId in _ids)
            {
                if (!_agents.TryGetValue(playerId, out var agent))
                {
                    continue;
                }

                try
                {
                    Step(agent, tick);
                }
                catch (ArgumentException e)
                {
                    // 位置や向きが壊れた (有限でない) Bot は、同じ例外でログを埋めないよう抜く
                    _log.WriteLine(LogTag + " player=" + Text(playerId) + " failed: " + e.Message);
                    Remove(playerId, "error");
                }
            }
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
        }

        public void Stop()
        {
            if (_match == null)
            {
                return;
            }

            _ids.Clear();
            _ids.AddRange(_agents.Keys);
            foreach (var playerId in _ids)
            {
                Remove(playerId, "stopping");
            }

            _match.PlayerJoined -= HandlePlayerJoined;
            _match.PlayerLeft -= HandlePlayerLeft;
            _match.MapChanged -= HandleMapChanged;
            StopArchetypes();
            _trimRequests.Clear();
        }

        private void HandlePlayerJoined(ServerPlayer player)
        {
            if (!player.IsBot && _target > 0)
            {
                // 抜くのは次の Reconcile。足りているかは人数を数え直して決める
                _trimRequests.Enqueue(player.Team);
            }
        }

        // 外から抜かれた (試合を止めるときなど) Bot の判断も捨てる
        private void HandlePlayerLeft(ServerPlayer player)
        {
            if (player.IsBot && _agents.Remove(player.PlayerId))
            {
                _movement.RemoveBot(player.PlayerId);
            }
        }

        private void Reconcile()
        {
            var session = _rules.Session;
            if (session == null)
            {
                return;
            }

            var humans = _match.HumanCount;
            var wanted = _rules.Objective?.BotsWanted;
            var desired = wanted.HasValue ? Math.Max(0, wanted.Value) : BotRoster.DesiredBots(_launchBots, session.Spec.Rules.MinPlayers, humans);
            if (desired != _loggedDesired)
            {
                _loggedDesired = desired;
                _log.WriteLine(LogTag + " humans=" + Text(humans) + " bots=" + Text(_agents.Count) + " desired=" + Text(desired)
                    + (wanted.HasValue ? " by=objective" : string.Empty));
            }

            while (_agents.Count > desired)
            {
                if (wanted.HasValue)
                {
                    if (!TrimForObjective())
                    {
                        break;
                    }

                    continue;
                }

                Team? preferred = _trimRequests.Count > 0 ? _trimRequests.Dequeue() : (Team?)null;
                if (!Trim(preferred, humans > 0 ? "replaced" : "no-humans"))
                {
                    break;
                }
            }

            // 足りている間に入った人の分は、抜く必要が無かったので忘れる
            _trimRequests.Clear();
            if (session.State == GameModeState.Ended)
            {
                return;
            }

            while (_agents.Count < desired && !_unavailable)
            {
                Add();
            }
        }

        private bool Trim(Team? preferred, string reason)
        {
            int alphaPlayers = 0, bravoPlayers = 0, alphaBots = 0, bravoBots = 0;
            foreach (var player in _match.Players)
            {
                var bot = _agents.ContainsKey(player.PlayerId);
                if (player.Team == Team.Bravo)
                {
                    bravoPlayers++;
                    bravoBots += bot ? 1 : 0;
                }
                else
                {
                    alphaPlayers++;
                    alphaBots += bot ? 1 : 0;
                }
            }

            var team = BotRoster.TeamToTrim(preferred, alphaPlayers, bravoPlayers, alphaBots, bravoBots);
            if (!team.HasValue)
            {
                return false;
            }

            var victim = 0;
            foreach (var playerId in _agents.Keys)
            {
                if (_match.TryGetPlayer(playerId, out var player) && player.Team == team.Value)
                {
                    victim = playerId;
                }
            }

            if (victim == 0)
            {
                return false;
            }

            Remove(victim, reason);
            return true;
        }

        private bool ObjectiveDrivesBots => _rules.Objective?.BotsWanted.HasValue == true;

        // チームの人数でなく目的の決める順で抜く。同じ順なら後から足した Bot (Id の大きい方) を抜く
        private bool TrimForObjective()
        {
            var objective = _rules.Objective;
            if (objective == null)
            {
                return false;
            }

            var victim = 0;
            var lowest = int.MaxValue;
            foreach (var playerId in _agents.Keys)
            {
                var order = objective.BotDropOrder(playerId);
                if (order <= lowest)
                {
                    lowest = order;
                    victim = playerId;
                }
            }

            if (victim == 0)
            {
                return false;
            }

            Remove(victim, "objective");
            return true;
        }

        private void Add()
        {
            if (_nextBotId == int.MaxValue)
            {
                _unavailable = true;
                _log.WriteLine(LogTag + " ran out of bot player ids; bots are disabled");
                return;
            }

            var playerId = _nextBotId++;
            _agents.Add(playerId, NewAgent(playerId));

            // 足した Bot は出現地点を巡る路で作るので、次の Rally で目的の場所へ向け直す
            _ralliedAt = null;

            var player = _match.JoinBot(playerId);
            _movement.AddBot(player);
            _log.WriteLine(LogTag + " added player=" + Text(playerId) + " name=" + BotRoster.KillFeedName(playerId) + " team=" + player.Team);
        }

        private ServerBotAgent NewAgent(int playerId)
        {
            var agent = new ServerBotAgent(playerId, _spec, _navigation, ShuffledPatrolRoute(playerId), new Random(playerId), _combat.SightBlockers);
            agent.UseMelee(_melee?.SwingOf(playerId));
            return agent;
        }

        private void Remove(int playerId, string reason)
        {
            _agents.Remove(playerId);
            _movement.RemoveBot(playerId);
            _match.LeaveBot(playerId);
            _log.WriteLine(LogTag + " removed player=" + Text(playerId) + " reason=" + reason);
        }

        private List<Vector3> ShuffledPatrolRoute(int playerId)
        {
            var route = new List<Vector3>(_patrolPoints);
            var random = new Random(playerId);
            for (var i = route.Count - 1; i > 0; i--)
            {
                var j = random.Next(i + 1);
                (route[i], route[j]) = (route[j], route[i]);
            }

            return route;
        }

        // 目的が場所を示す間は Bot をそのまわりに巡らせ、示さなくなったら (ルールが替わったなど) 出現地点を巡る路に戻す
        private void Rally()
        {
            var rally = _rules.Objective?.BotRally;
            if (!rally.HasValue)
            {
                if (_ralliedAt.HasValue)
                {
                    _ralliedAt = null;
                    foreach (var pair in _agents)
                    {
                        if (!pair.Value.Goal.HasValue)
                        {
                            pair.Value.SetRoute(ShuffledPatrolRoute(pair.Key));
                        }
                    }
                }

                return;
            }

            var center = rally.Value.Center;
            var radius = rally.Value.Radius;
            if (_ralliedAt.HasValue && Vector3.Distance(_ralliedAt.Value, center) < radius * _spec.RallyMoveRadiusFraction)
            {
                return;
            }

            // 行き先を持つ Bot の路は Goals が持つので、ここでは上書きも数えもしない
            var slot = 0;
            foreach (var agent in _agents.Values)
            {
                if (agent.Goal.HasValue)
                {
                    continue;
                }

                agent.SetRoute(RouteAround(center, radius, slot, _spec));
                slot++;
            }

            _ralliedAt = center;
            _log.WriteLine(LogTag + " rally x=" + F2(center.X) + " y=" + F2(center.Y) + " z=" + F2(center.Z)
                + " radius=" + F2(radius) + " bots=" + Text(slot));
        }

        // 巡る路の 1 点だけにするのは、敵を見たら戦い、見失ったら行き先へ戻る判断 (EnemyBrain) をそのまま使うため。
        // 行き先が替わったときだけ路を作り直すのは、毎 tick 作り直すと路の進み具合が毎回始めに戻るため
        private void Goals()
        {
            var objective = _rules.Objective;
            foreach (var pair in _agents)
            {
                var agent = pair.Value;
                var goal = objective?.BotGoal(pair.Key);
                if (goal == agent.Goal)
                {
                    continue;
                }

                agent.Goal = goal;
                if (goal.HasValue)
                {
                    agent.SetRoute(new[] { goal.Value });
                    _log.WriteLine(ServerBotLog.Goal(pair.Key, goal.Value));
                }
                else
                {
                    agent.SetRoute(DefaultRoute(pair.Key));
                    _log.WriteLine(ServerBotLog.GoalCleared(pair.Key));
                }
            }
        }

        // 行き先を手放した Bot を、ほかの Bot と同じく BotRally のまわりか出現地点を巡る路に戻す
        private IReadOnlyList<Vector3> DefaultRoute(int playerId)
        {
            var rally = _rules.Objective?.BotRally;
            if (!rally.HasValue || !_ralliedAt.HasValue)
            {
                return ShuffledPatrolRoute(playerId);
            }

            var slot = 0;
            foreach (var pair in _agents)
            {
                if (pair.Key == playerId)
                {
                    break;
                }

                if (!pair.Value.Goal.HasValue)
                {
                    slot++;
                }
            }

            return RouteAround(_ralliedAt.Value, rally.Value.Radius, slot, _spec);
        }

        // 範囲の中で向かい合う 2 点を往復させる。Bot ごとに向きをずらし、同じ所に固まらないようにする
        internal static Vector3[] RouteAround(Vector3 center, float radius, int slot, NetBotSpec spec = null)
        {
            spec ??= NetBotSpec.Default;
            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (slot < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slot), slot, "slot must not be negative");
            }

            var angle = slot * spec.RallyRouteAngleStepDegrees % 360f / ServerBotMath.DegreesPerRadian;
            var offset = new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * (radius * spec.RallyRouteRadiusFraction);
            return new[] { center + offset, center - offset };
        }

        private bool HasNavMeshNearPatrolPoints()
        {
            foreach (var point in _patrolPoints)
            {
                if (_navigation.CanSample(point))
                {
                    return true;
                }
            }

            return false;
        }

        private void Step(ServerBotAgent agent, long tick)
        {
            var playerId = agent.PlayerId;
            var session = _rules.Session;
            if (session == null || !session.HasPlayer(playerId) || !_bodies.TryGet(playerId, out var body))
            {
                return;
            }

            var dt = _movement.TickSeconds;
            if (!session.IsAlive(playerId))
            {
                agent.MarkDead();
                return;
            }

            var respawned = body.TeleportTick != agent.SpawnTick;
            agent.SpawnTick = body.TeleportTick;
            agent.Restart(respawned);
            agent.Clock += dt;

            _movement.TryGetState(playerId, out var motion);
            var position = body.Position;
            var feet = position + new Vector3(0f, body.Shape.BottomY, 0f);
            var eye = body.EyePosition;
            var yaw = body.Yaw * ServerBotMath.DegreesPerRadian;
            var thinking = session.State != GameModeState.Ended && !session.IsDowned(playerId);
            var perception = default(EnemyPerception);
            var decision = EnemyDecision.None;
            if (thinking)
            {
                perception = Perceive(agent, body, session);
                agent.EnableMelee(_melee != null && _melee.IsOn);
                if (agent.PrepareCover(perception, _coverProbe))
                {
                    _log.WriteLine(ServerBotLog.CoverFound(playerId, agent.CoverPointCount));
                }

                decision = agent.Brain.Tick(dt, perception);
            }

            // 突進している間と爆ぜた tick は、歩く入力を出さずに種類の動きだけをする
            if (StepArchetype(agent, body, session, motion, thinking, dt))
            {
                LogPosition(agent, session);
                return;
            }

            var step = BotRouteStep.None;
            var climb = 0f;
            var route = agent.Route;
            if (route.IsOnLadderLink)
            {
                // はしごを渡っている間は判断で止まらない (オフラインの EnemyAgent と同じく、渡り切ってから判断に戻る)
                step = route.Next(feet, motion.Ladder, agent.Corners, out climb);
            }
            else if (decision.MoveTo.HasValue && _hasNavMesh)
            {
                if (!route.Refresh(agent.Clock, position, decision.MoveTo.Value))
                {
                    // 届かない先 (NavMesh の外の相手・壊れていない建物の中など) は Brain にあきらめさせ、次の tick に別の先を選ばせる
                    agent.Brain.AbandonGoal();
                }

                step = route.Next(feet, motion.Ladder, agent.Corners, out climb);
            }

            BotInputPlan plan;
            var move = Vector2.Zero;
            switch (step)
            {
                case BotRouteStep.Climb:
                    plan = agent.Driver.Plan(dt, EnemyDecision.None, position, eye, yaw, agent.Pitch, null);
                    move = new Vector2(0f, climb);
                    break;
                case BotRouteStep.ApproachLadder:
                    plan = agent.Driver.Plan(dt, new EnemyDecision(agent.Corners[0], null, false), position, eye, yaw, agent.Pitch, agent.Corners);
                    move = plan.Move;
                    break;
                case BotRouteStep.Walk:
                    plan = agent.Driver.Plan(dt, decision, position, eye, yaw, agent.Pitch, agent.Corners);
                    move = plan.Move;
                    break;
                default:
                    // NavMesh が無ければ目的地へまっすぐ進む。経路が引けない・着いたなら動かずに向きと射撃だけ決める
                    var straight = decision.MoveTo.HasValue && !_hasNavMesh;
                    var standing = new EnemyDecision(straight ? decision.MoveTo : null, decision.LookAt, decision.Fire, decision.Crouch);
                    plan = agent.Driver.Plan(dt, standing, position, eye, yaw, agent.Pitch, null);
                    move = plan.Move;
                    break;
            }

            agent.Pitch = plan.Pitch;
            var traversing = step == BotRouteStep.Climb || step == BotRouteStep.ApproachLadder;
            var sprint = plan.Sprint;
            var crouch = plan.Crouch || decision.Crouch;
            var gait = agent.GaitMultiplier;
            if (gait.HasValue && !traversing)
            {
                // 盾持ちの遅い歩みと自爆兵の駆け足は種類の速さで決め、計画の走り・しゃがみは使わない
                var motor = _movement.Settings.Motor;
                move = ServerBotAgent.GaitMove(move, gait.Value, motor.WalkSpeed, motor.SprintSpeed, out sprint);
                crouch = false;
            }

            var buttons = InputButtons.None;
            if (sprint && agent.SprintAllowed && !traversing)
            {
                buttons |= InputButtons.Sprint;
            }

            // 判断が物陰でしゃがませる (隠れている) 時も、入力の計画 (構えた時にしゃがむか) と同じくしゃがむ
            if (crouch && !traversing)
            {
                buttons |= InputButtons.Crouch;
            }

            _movement.ApplyBotInput(playerId, new InputFrame((int)Math.Min(tick, int.MaxValue), move, plan.Yaw / ServerBotMath.DegreesPerRadian, buttons));

            _movement.TryGetState(playerId, out var after);
            ReportLadder(agent, after.Ladder, route);
            if (agent.IsStuck(body.Position, step != BotRouteStep.None && move != Vector2.Zero))
            {
                _log.WriteLine(LogTag + " stuck player=" + Text(playerId) + " state=" + agent.Brain.State + "; abandoning the goal");
                agent.Brain.AbandonGoal();
                route.Clear();
            }

            if (!traversing && !after.Ladder.IsClimbing)
            {
                // 振ったことは近接のモジュールが人と同じ行でログに出す
                if (thinking && agent.WantsSwing)
                {
                    _melee.BotSwing(playerId);
                }
                else if (thinking)
                {
                    TryThrowGrenade(agent, body, perception, decision);
                }

                Shoot(agent, plan, decision.LookAt, (int)Math.Min(tick, int.MaxValue), dt);
            }

            Bash(agent, body, decision.LookAt, thinking && !traversing && !after.Ladder.IsClimbing, dt);
            LogPosition(agent, session);
        }

        private void LogPosition(ServerBotAgent agent, TeamDeathmatchSession session)
        {
            if (agent.Clock < agent.NextPositionLog || !_bodies.TryGet(agent.PlayerId, out var body))
            {
                return;
            }

            var playerId = agent.PlayerId;
            agent.NextPositionLog = agent.Clock + PositionLogIntervalSeconds;
            var at = body.Position;
            _log.WriteLine(LogTag + " pos player=" + Text(playerId) + " x=" + F2(at.X) + " y=" + F2(at.Y) + " z=" + F2(at.Z)
                + " state=" + agent.Brain.State + " health=" + Text(session.GetHealth(playerId)));
        }

        private void ReportLadder(ServerBotAgent agent, CharacterLadderState ladder, ServerBotRoute route)
        {
            if (ladder.IsClimbing == agent.WasClimbing)
            {
                return;
            }

            agent.WasClimbing = ladder.IsClimbing;
            var climb = !ladder.IsClimbing ? "Done" : (route.IsOnLadderLink && !route.Ascending ? "Down" : "Up");
            var index = ladder.IsClimbing ? ladder.Index : route.Ladder;
            _log.WriteLine(LogTag + " ladder player=" + Text(agent.PlayerId) + " ladder=" + Text(index) + " climb=" + climb);
        }

        private void Shoot(ServerBotAgent agent, BotInputPlan plan, Vector3? lookAt, int tick, float dt)
        {
            var playerId = agent.PlayerId;
            if (!_combat.TryGetWeapon(playerId, out var spec, out var state))
            {
                return;
            }

            var trigger = agent.TriggerFor(spec);
            trigger.Tick(dt);
            if (state.IsReloading || state.IsSwitching)
            {
                return;
            }

            if (state.AmmoInMagazine <= 0)
            {
                _combat.BotReload(playerId);
                return;
            }

            // Semi も連射間隔ごとに引き直す (人が速く叩くのと同じ)。間隔より速くはサーバーの検査も通さない
            if (!plan.Fire || !agent.IsFacing(plan.FireOrigin, plan.FireDirection, lookAt) || !_rules.CanFire(playerId) || !trigger.Pull(true, true))
            {
                return;
            }

            if (_combat.BotFire(playerId, plan.FireOrigin, agent.Scatter(plan.FireOrigin, plan.FireDirection, lookAt), tick))
            {
                agent.Brain.RecordShot();

                // 狙撃兵は撃てたら次の足場へ移る。撃った瞬間の足元から次を選ぶ (オフラインの EnemySniper と同じ)
                if (_bodies.TryGet(playerId, out var body) && agent.ConfirmShot(body.Position + new Vector3(0f, body.Shape.BottomY, 0f)))
                {
                    _log.WriteLine(ServerBotLog.SniperFired(playerId, agent.ShotDamageMultiplier));
                }
            }
        }

        // 狙うのは判断が向いている所 (最後に見た・聞いた相手の体の中心) の足元。爆風の届く所に味方がいれば投げない
        private void TryThrowGrenade(ServerBotAgent agent, ServerBody body, in EnemyPerception perception, EnemyDecision decision)
        {
            // 隠れている間と、High の物陰で覗く間 (立った目より高い壁のすぐ後ろに立つ) は、弧が壁に当たって自分の足元で爆ぜるので投げない
            if (!agent.CanThrowGrenade
                || (agent.Brain.State == EnemyState.TakeCover
                    && (agent.Brain.CoverPhase == EnemyCoverPhase.Hiding
                        || agent.Brain.CurrentCoverPoint?.Height == CoverHeight.High))
                || !ServerBotAgent.WantsGrenade(agent.Brain.State, perception.HasLineOfSight, body.Position, decision.LookAt, _spec.Tactics))
            {
                return;
            }

            var playerId = agent.PlayerId;
            var spec = _combat.BotGrenade(playerId);
            if (spec == null)
            {
                return;
            }

            var target = decision.LookAt.Value - new Vector3(0f, _coverProbe.CenterHeight, 0f);
            if (AllyNear(playerId, target, spec.Impact.Explosion.Damage.Radius)
                || !ServerBotAgent.TryAimGrenade(body.EyePosition, target, spec, ServerGrenadeFlight.Gravity, out var direction)
                || !_combat.BotThrowGrenade(playerId, direction, spec))
            {
                return;
            }

            agent.NoteGrenadeThrown();
            _log.WriteLine(ServerBotLog.GrenadeThrown(playerId, agent.Brain.State, target));
        }

        private bool AllyNear(int selfId, Vector3 point, float radius)
        {
            var session = _rules.Session;
            foreach (var other in _bodies.All)
            {
                var id = other.PlayerId;
                if (id == selfId || session == null || !session.HasPlayer(id) || !session.IsAlive(id) || _rules.AreHostile(selfId, id))
                {
                    continue;
                }

                if (Vector3.Distance(other.Position, point) <= radius + _coverProbe.CenterHeight)
                {
                    return true;
                }
            }

            return false;
        }

        // NavMesh があればその上へ寄せ、寄せて大きくずれた点 (壁の中から向こう側へ寄せたなど) は捨てる。
        // NavMesh が無ければ Bot はまっすぐ歩くので、今の所から壁を通らずに着ける点だけにする
        private Vector3? PlaceCover(Vector3 from, Vector3 candidate)
        {
            if (_hasNavMesh)
            {
                if (!_navigation.TrySample(candidate, out var onMesh))
                {
                    return null;
                }

                var drift = new Vector2(onMesh.X - candidate.X, onMesh.Z - candidate.Z).Length();
                return drift <= _spec.Tactics.CoverSearchStep * 0.5f ? onMesh : (Vector3?)null;
            }

            var lift = new Vector3(0f, _coverProbe.CenterHeight, 0f);
            return IsWorldBlocked(from + lift, candidate + lift) ? (Vector3?)null : candidate;
        }

        // 物陰は世界の物だけで決める。煙 (視線を遮る物) はすぐ消えるので数えない
        private bool IsWorldBlocked(Vector3 from, Vector3 to)
        {
            var offset = to - from;
            var distance = offset.Length();
            return distance > 0f && _context.World.Raycast(from, offset / distance, distance, ServerHitboxes.RaycastLayers, out _);
        }

        private EnemyPerception Perceive(ServerBotAgent agent, ServerBody self, TeamDeathmatchSession session)
        {
            var selfId = self.PlayerId;
            var team = session.GetTeam(selfId);
            var eye = self.EyePosition;
            ServerBody seen = null;
            var seenDistance = float.PositiveInfinity;
            ServerBody nearest = null;
            var nearestDistance = float.PositiveInfinity;
            var sight = _spec.Brain.SightRange;
            var blind = _combat.StatusEffects.RemainingSeconds(selfId, StatusEffectKinds.Blind) > 0f;
            foreach (var other in _bodies.All)
            {
                var id = other.PlayerId;
                // 敵は目的が決める (BR では分隊に入れなかった人が敵でも味方でもない) ので、味方でないかでなく目的の AreHostile で外す
                if (id == selfId || !session.HasPlayer(id) || !_rules.AreHostile(selfId, id) || !session.IsAlive(id) || session.IsDowned(id))
                {
                    continue;
                }

                var distance = Vector3.Distance(self.Position, other.Position);
                if (distance < nearestDistance)
                {
                    nearest = other;
                    nearestDistance = distance;
                }

                if (!blind && distance <= sight && distance < seenDistance && HasLineOfSight(agent, eye, other.Center))
                {
                    seen = other;
                    seenDistance = distance;
                }
            }

            var health = session.GetHealth(selfId);
            var wasHit = agent.LastHealth != int.MaxValue && health < agent.LastHealth;
            agent.LastHealth = health;
            var healthFraction = session.MaxHealth > 0 ? Math.Clamp((float)health / session.MaxHealth, 0f, 1f) : 1f;
            // 目がくらんでいる間は、撃たれても誰に撃たれたか分からないままにする
            var heard = !blind && seen == null && wasHit && nearest != null;
            Vector3? hitSource = wasHit && !blind && nearest != null ? nearest.Position : (Vector3?)null;

            // 物陰に隠れている間は相手が見えなくなるが、オフラインの敵 (EnemySenses) と同じく、見通さずに相手の居場所は知っている。
            // 知らせないと、判断は隠れた途端に脅威を見失い、物陰を出てしまう
            var hiding = seen == null && !blind && nearest != null && nearestDistance <= sight && agent.Brain.State == EnemyState.TakeCover;
            var perception = new EnemyPerception(
                self.Position,
                seen?.Position ?? (hiding ? nearest.Position : (Vector3?)null),
                seen != null,
                heard,
                heard ? nearest.Position : (Vector3?)null,
                wasHit,
                hitSource,
                healthFraction);
            if (_deployables == null)
            {
                return perception;
            }

            // 同じ tick の先の Bot が撃って壊した囮を見ないよう、Bot ごとに集め直す
            _deployables.CollectDecoys(_decoys);
            return agent.Decoys.Contribute(
                perception, Side.FromTeam(team), !blind, sight, _decoys, point => HasLineOfSight(agent, eye, point), _log);
        }

        // 壊れた建物の部品は世界の当たりから外れているので、その穴越しには見える
        private bool HasLineOfSight(ServerBotAgent agent, Vector3 eye, Vector3 target)
        {
            var offset = target - eye;
            var distance = offset.Length();
            return !(distance > 0f)
                || (!_context.World.Raycast(eye, offset / distance, distance, ServerHitboxes.RaycastLayers, out _) && !agent.IsSightBlocked(eye, target));
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string F2(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
    }

    internal static class ServerBotMath
    {
        public const float DegreesPerRadian = (float)(180.0 / Math.PI);
    }
}
