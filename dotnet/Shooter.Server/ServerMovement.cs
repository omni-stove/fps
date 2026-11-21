using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 入力は落ちてよい届け方で直近の tick をまとめて送ってくるので、処理済みの tick は捨てる。
    // 速く送り続けて先へ進むのを防ぐため、1 tick の入力を 1 つ使う予算を tick の速さで貯め、半秒分を超えて貯めない (Unity のサーバーと同じ)。
    // ダウン中か・乗り越えてよいか・ルールで使えない動きはクライアントの申告を信じず、サーバーの知る状態とルールで決める。
    internal sealed partial class ServerMovement : IServerModule, ISnapshotContributor
    {
        public const float MaxInputBudget = TickClock.DefaultTickRate * 0.5f;

        private ServerMotorSettings _settings;
        private readonly float _tickSeconds;
        private readonly ServerCheatSignals _signals;
        private readonly Dictionary<ConnectionId, Mover> _byConnection = new Dictionary<ConnectionId, Mover>();
        private readonly Dictionary<int, Mover> _byPlayer = new Dictionary<int, Mover>();
        private ServerContext _context;
        private TextWriter _log;
        private KnockbackSpec _knockback = KnockbackSpec.Default;
        private float _now;

        public ServerMovement(ServerMotorSettings settings, int tickRate = TickClock.DefaultTickRate, ServerCheatSignals signals = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _signals = signals;
            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "tickRate must be positive");
            }

            _tickSeconds = 1f / tickRate;
            TickRate = tickRate;
        }

        public event Action<ServerPlayer, int, float> Landed;

        // 息が切れたダメージの出来事の数を渡す。体力はルールが持つので、ここでは与えずに落下と同じく戦闘のモジュールに任せる。
        public event Action<ServerPlayer, int, int> Drowned;

        public string Name => "movement";

        public int TickRate { get; }

        public float TickSeconds => _tickSeconds;

        public ServerMotorSettings Settings => _settings;

        internal ServerCheatSignals CheatSignals => _signals;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _knockback = context.Rules?.Knockback ?? KnockbackSpec.Default;
            _context.Bodies.Teleported += HandleTeleported;
            _context.Bodies.DownedChanged += HandleDownedChanged;
            _context.Match.MapChanged += HandleMapChanged;
            _context.Match.RulesChanged += HandleRulesChanged;
            if (_signals != null)
            {
                _signals.ShotAccepted += HandleShotAccepted;
            }
        }

        // 撃った向きの pitch は人の体 (Mover) に覚える。抜けると体ごと消えるので、番号を使い回す次の人へ残らない
        private void HandleShotAccepted(int shooterId, Vector3 origin, Vector3 direction, float shotTick)
        {
            if (BotRoster.IsBotId(shooterId) || !_byPlayer.TryGetValue(shooterId, out var mover))
            {
                return;
            }

            mover.ShotPitch = ServerCheatSignals.PitchOf(direction);
        }

        // 体の寸法と移動の値はクライアントの予測と同じ NetPlayer.prefab の値なので替えず、ルール (落下ダメージ・使える動き) だけを替える
        private void HandleRulesChanged(GameRules rules)
        {
            _settings = _settings.WithRules(rules);
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
            var refill = deltaSeconds * TickRate;
            foreach (var mover in _byPlayer.Values)
            {
                mover.InputBudget = Math.Min(MaxInputBudget, mover.InputBudget + refill);
            }
        }

        public void OnConnected(ConnectionId connection)
        {
            if (!_context.Match.TryGetPlayer(connection, out var player))
            {
                return;
            }

            _byConnection[connection] = Add(player);
        }

        private Mover Add(ServerPlayer player)
        {
            // ルールのモジュールは参加の知らせ (この呼び出しより前) のうちに出現させるので、置き直しはもう体に書いてある
            var spawned = _context.Bodies.TryGet(player.PlayerId, out var shared) && shared.HasSpawned;
            var spawn = spawned ? shared.Position : _settings.SpawnPosition;
            var mover = new Mover(player, CreateSimulation(spawn))
            {
                State = new CharacterMotorState(spawn, Vector3.Zero, false, false),
                InputBudget = MaxInputBudget,
                NextWarning = float.NegativeInfinity,
                Yaw = spawned ? shared.Yaw : 0f,
                Downed = shared != null && shared.IsDowned,
            };
            mover.Simulation.Place(mover.State);
            _byPlayer[player.PlayerId] = mover;
            Publish(mover);
            return mover;
        }

        private CharacterMotorSimulation CreateSimulation(Vector3 spawn)
        {
            var body = new CharacterBody(_context.World, _settings.Mover, _settings.Stance);
            return new CharacterMotorSimulation(body, new PlayerMotor(_settings.Motor), _tickSeconds, _settings.KillPlaneY, _settings.Prone)
            {
                SpawnPosition = spawn,
                Ladders = _context.Map.Ladders.Ladders,
                Water = _context.Map.Water,
                Knockback = _knockback,
                Swim = _settings.Swim,
                WallRun = _settings.WallRun,
                Parachute = _settings.Parachute,
            };
        }

        // 体は作るときに世界の当たり・はしご・水を受け取るので、作り直す。乗り物も替わるので座席から外し、置き直しは続くルールの出現に任せる
        private void HandleMapChanged(ServerMapData map)
        {
            foreach (var mover in _byPlayer.Values)
            {
                var position = mover.State.Position;
                CancelMantle(mover);
                mover.Simulation = CreateSimulation(position);
                mover.Riding = false;
                mover.State = new CharacterMotorState(position, Vector3.Zero, false, false);
                mover.Simulation.Place(mover.State);
                Publish(mover);
            }
        }

        public void OnDisconnected(ConnectionId connection)
        {
            if (_byConnection.Remove(connection, out var mover))
            {
                _byPlayer.Remove(mover.Player.PlayerId);
            }
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (message is InputMessage input && _byConnection.TryGetValue(connection, out var mover))
            {
                Receive(mover, input);
            }
        }

        public void Stop()
        {
            if (_context != null)
            {
                _context.Bodies.Teleported -= HandleTeleported;
                _context.Bodies.DownedChanged -= HandleDownedChanged;
                _context.Match.MapChanged -= HandleMapChanged;
                _context.Match.RulesChanged -= HandleRulesChanged;
            }

            if (_signals != null)
            {
                _signals.ShotAccepted -= HandleShotAccepted;
            }

            _byConnection.Clear();
            _byPlayer.Clear();
        }

        private void HandleTeleported(ServerBody body)
        {
            if (_byPlayer.TryGetValue(body.PlayerId, out var mover))
            {
                mover.Yaw = body.Yaw;

                // 出現し直すとサーバーが向きを決め直すので、その前に撃った縦の向きを次の入力に載せない
                mover.ShotPitch = null;
                Teleport(body.PlayerId, body.Position);
            }
        }

        private void HandleDownedChanged(ServerBody body)
        {
            SetDowned(body.PlayerId, body.IsDowned);
        }

        private sealed class Mover
        {
            public Mover(ServerPlayer player, CharacterMotorSimulation simulation)
            {
                Player = player;
                Simulation = simulation;
            }

            public ServerPlayer Player { get; }

            public CharacterMotorSimulation Simulation { get; set; }

            public CharacterMotorState State { get; set; }

            public int LastProcessedTick { get; set; } = -1;

            public float InputBudget { get; set; }

            public float Yaw { get; set; }

            public float? ShotPitch { get; set; }

            public bool Aiming { get; set; }

            public bool Sprinting { get; set; }

            public bool Downed { get; set; }

            public bool Riding { get; set; }

            public VaultPath MantlePath { get; set; }

            public float MantleElapsed { get; set; }

            public float NextWarning { get; set; }
        }
    }
}
