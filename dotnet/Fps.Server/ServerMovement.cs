using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// 入力は落ちてよい届け方で直近の tick をまとめて送ってくるので、処理済みの tick は捨てる。
    /// 速く送り続けて先へ進むのを防ぐため、1 tick の入力を 1 つ使う予算を tick の速さで貯め、半秒分を超えて貯めない (Unity のサーバーと同じ)。
    /// ダウン中か・乗り越えてよいか・ルールで使えない動きはクライアントの申告を信じず、サーバーの知る状態とルールで決める。
    /// </summary>
    internal sealed class ServerMovement : IServerModule, ISnapshotContributor
    {
        public const float MaxInputBudget = TickClock.DefaultTickRate * 0.5f;

        private const float _warningIntervalSeconds = 1f;

        private readonly ServerMotorSettings _settings;
        private readonly float _tickSeconds;
        private readonly Dictionary<ConnectionId, Mover> _byConnection = new Dictionary<ConnectionId, Mover>();
        private readonly Dictionary<int, Mover> _byPlayer = new Dictionary<int, Mover>();
        private ServerContext _context;
        private TextWriter _log;
        private float _now;

        public ServerMovement(ServerMotorSettings settings, int tickRate = TickClock.DefaultTickRate)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "tickRate must be positive");
            }

            _tickSeconds = 1f / tickRate;
            TickRate = tickRate;
        }

        public event Action<ServerPlayer, int, float> Landed;

        public string Name => "movement";

        public int TickRate { get; }

        public ServerMotorSettings Settings => _settings;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _context.Bodies.Teleported += HandleTeleported;
            _context.Bodies.DownedChanged += HandleDownedChanged;
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

            // ルールのモジュールは参加の知らせ (この呼び出しより前) のうちに出現させるので、置き直しはもう体に書いてある
            var spawned = _context.Bodies.TryGet(player.PlayerId, out var shared) && shared.HasSpawned;
            var spawn = spawned ? shared.Position : _settings.SpawnPosition;
            var body = new CharacterBody(_context.World, _settings.Mover, _settings.Stance);
            var simulation = new CharacterMotorSimulation(body, new PlayerMotor(_settings.Motor), _tickSeconds, _settings.KillPlaneY)
            {
                SpawnPosition = spawn,
            };
            var mover = new Mover(player, simulation)
            {
                State = new CharacterMotorState(spawn, Vector3.Zero, false, false),
                InputBudget = MaxInputBudget,
                NextWarning = float.NegativeInfinity,
                Yaw = spawned ? shared.Yaw : 0f,
                Downed = shared != null && shared.IsDowned,
            };
            simulation.Place(mover.State);
            _byConnection[connection] = mover;
            _byPlayer[player.PlayerId] = mover;
            Publish(mover);
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
            }

            _byConnection.Clear();
            _byPlayer.Clear();
        }

        public bool TryGetState(int playerId, out CharacterMotorState state)
        {
            if (_byPlayer.TryGetValue(playerId, out var mover))
            {
                state = mover.State;
                return true;
            }

            state = default;
            return false;
        }

        public int LastProcessedInputTick(int playerId)
        {
            return _byPlayer.TryGetValue(playerId, out var mover) ? mover.LastProcessedTick : -1;
        }

        public bool Teleport(int playerId, Vector3 position)
        {
            if (!float.IsFinite(position.X) || !float.IsFinite(position.Y) || !float.IsFinite(position.Z))
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "position must be finite");
            }

            if (!_byPlayer.TryGetValue(playerId, out var mover))
            {
                return false;
            }

            mover.Simulation.SpawnPosition = position;
            mover.State = new CharacterMotorState(position, Vector3.Zero, false, false);
            mover.Simulation.Place(mover.State);
            Publish(mover);
            return true;
        }

        public bool SetDowned(int playerId, bool downed)
        {
            if (!_byPlayer.TryGetValue(playerId, out var mover))
            {
                return false;
            }

            mover.Downed = downed;
            return true;
        }

        public int FallDamage(float fallSpeed, int maxHealth)
        {
            return _settings.Rules.FallDamage == Fps.Core.FallDamage.On
                ? FallDamageCalculator.Calculate(fallSpeed, _settings.Motor, maxHealth)
                : 0;
        }

        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(receiver);
            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                if (!_byPlayer.TryGetValue(row.PlayerId, out var mover))
                {
                    continue;
                }

                var state = mover.State;
                row.Position = state.Position;
                row.Velocity = state.Velocity;
                row.Yaw = mover.Yaw;
                row.IsGrounded = state.IsGrounded;
                row.IsCrouched = state.IsCrouched;
                row.IsAiming = mover.Aiming;
                row.Cover = new CoverSync(state.Cover.Snapshot, state.Cover.TopY, state.Cover.Edges, state.Cover.AppliedPeek);
                row.Dodge = state.Dodge;
                row.Vault = state.Vault.IsVaulting
                    ? new VaultSync(true, state.Vault.Rise, state.Vault.Cross, state.Vault.Land, state.Vault.Elapsed)
                    : VaultSync.None;
            }

            if (_byPlayer.TryGetValue(receiver.PlayerId, out var own))
            {
                snapshot.Owner.LastProcessedInputTick = own.LastProcessedTick;
            }
        }

        private void Receive(Mover mover, InputMessage input)
        {
            for (var i = 0; i < input.Count; i++)
            {
                var frame = input[i];
                if (frame.Tick <= mover.LastProcessedTick)
                {
                    continue;
                }

                if (!CharacterMotorInput.IsValid(frame.Move, frame.Yaw))
                {
                    Warn(mover, "[Net] dropped non-finite input from client=" + Text(mover.Player.Connection) + " tick=" + Text(frame.Tick));
                    continue;
                }

                if (mover.InputBudget < 1f)
                {
                    Warn(
                        mover,
                        "[Net] dropped input from client=" + Text(mover.Player.Connection) + " tick=" + Text(frame.Tick)
                            + ": sending faster than the tick rate");
                    return;
                }

                mover.InputBudget -= 1f;
                Apply(mover, frame);
            }
        }

        private void Apply(Mover mover, InputFrame frame)
        {
            var input = new CharacterMotorInput(
                    frame.Move,
                    frame.Yaw,
                    frame.Has(InputButtons.Sprint),
                    frame.Has(InputButtons.Jump),
                    frame.Has(InputButtons.Crouch),
                    mover.Downed,
                    frame.Has(InputButtons.Cover),
                    frame.Has(InputButtons.Aim),
                    frame.Has(InputButtons.Dodge),
                    false)
                .WithRules(_settings.Rules.Actions);

            var previous = mover.State;
            var current = mover.Simulation.Step(previous, input);
            mover.State = current;
            mover.Yaw = input.Yaw;
            mover.Aiming = input.Restrained().Aim;
            mover.LastProcessedTick = frame.Tick;
            Publish(mover);
            Report(mover, frame.Tick, previous, current);
        }

        // 位置の持ち主はここなので、ルール・戦闘・Bot が同じ位置で判定できるよう、動かすたびに共有の体へ写す
        private void Publish(Mover mover)
        {
            if (_context.Bodies.TryGet(mover.Player.PlayerId, out var body))
            {
                var state = mover.State;
                body.SetPose(state.Position, mover.Yaw, state.IsCrouched ? PlayerStance.Crouching : PlayerStance.Standing);
            }
        }

        private void HandleTeleported(ServerBody body)
        {
            if (_byPlayer.TryGetValue(body.PlayerId, out var mover))
            {
                mover.Yaw = body.Yaw;
                Teleport(body.PlayerId, body.Position);
            }
        }

        private void HandleDownedChanged(ServerBody body)
        {
            SetDowned(body.PlayerId, body.IsDowned);
        }

        private void Report(Mover mover, int tick, CharacterMotorState previous, CharacterMotorState current)
        {
            var playerId = mover.Player.PlayerId;
            if (previous.IsCrouched != current.IsCrouched)
            {
                _log.WriteLine(ServerMovementLog.Stance(playerId, tick, current.IsCrouched));
            }

            var before = previous.Cover;
            var after = current.Cover;
            if (before.Phase != after.Phase || (after.IsInCover && before.Height != after.Height))
            {
                _log.WriteLine(ServerMovementLog.Cover(playerId, tick, after.Phase, after.IsInCover ? after.Height : (CoverHeight?)null));
            }

            if (previous.IsDodging != current.IsDodging)
            {
                _log.WriteLine(ServerMovementLog.Dodge(playerId, tick, current.IsDodging));
            }

            if (previous.IsVaulting != current.IsVaulting)
            {
                _log.WriteLine(ServerMovementLog.Vault(playerId, tick, current.IsVaulting));
            }

            // 着地した tick の速度は、まだ接地中の吸着の速度に戻る前の落下速度を持っている
            if (!previous.IsGrounded && current.IsGrounded)
            {
                Landed?.Invoke(mover.Player, tick, Math.Max(0f, -current.Velocity.Y));
            }
        }

        private void Warn(Mover mover, string text)
        {
            if (_now < mover.NextWarning)
            {
                return;
            }

            mover.NextWarning = _now + _warningIntervalSeconds;
            _log.WriteLine(text);
        }

        private static string Text(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Text(ConnectionId connection)
        {
            return connection.Value.ToString(CultureInfo.InvariantCulture);
        }

        private sealed class Mover
        {
            public Mover(ServerPlayer player, CharacterMotorSimulation simulation)
            {
                Player = player;
                Simulation = simulation;
            }

            public ServerPlayer Player { get; }

            public CharacterMotorSimulation Simulation { get; }

            public CharacterMotorState State { get; set; }

            public int LastProcessedTick { get; set; } = -1;

            public float InputBudget { get; set; }

            public float Yaw { get; set; }

            public bool Aiming { get; set; }

            public bool Downed { get; set; }

            public float NextWarning { get; set; }
        }
    }
}
