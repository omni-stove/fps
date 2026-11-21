using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Server.Eos;

namespace Shooter.Server
{
    internal sealed class ServerLobbyModule : IServerModule, IDisposable
    {
        private readonly Func<TextWriter, IServerLobbyBackend> _createBackend;
        private readonly string _endpoint;
        private readonly Func<float> _clock;
        private readonly Action<int> _sleep;
        private ServerContext _context;
        private ServerMatch _match;
        private ServerLobbyRegistration _registration;
        private int? _skillAverage;
        private string _region;
        private Func<GameRules> _currentRules;

        public ServerLobbyModule(Func<TextWriter, IServerLobbyBackend> createBackend, string endpoint)
            : this(createBackend, endpoint, StopwatchClock(), Thread.Sleep)
        {
        }

        public ServerLobbyModule(Func<TextWriter, IServerLobbyBackend> createBackend, string endpoint, Func<float> clock, Action<int> sleep)
        {
            _createBackend = createBackend ?? throw new ArgumentNullException(nameof(createBackend));
            _endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _sleep = sleep ?? throw new ArgumentNullException(nameof(sleep));
        }

        public string Name => "lobby";

        public ServerLobbyRegistration Registration => _registration;

        public int? SkillAverage => _skillAverage;

        // 作ったロビーの属性は人数・平均・地図・モードしか書き直さないので、登録を始めた後に替えても載らない。Start より前だけ受ける。
        public string Region
        {
            get => _region;
            set
            {
                if (_context != null)
                {
                    throw new InvalidOperationException("set Region before the lobby module starts");
                }

                if (value != null && !ServerLobby.IsRegionName(value))
                {
                    throw new ArgumentException("'" + value + "' is not a server region (" + ServerLobby.RegionRule + ")", nameof(value));
                }

                _region = value;
            }
        }

        // 今の試合のルール。ServerContext.Rules は起動時のルールのままで、次の試合のルール (ServerMatch.RequestNextRules) を映さないので、
        // ルールのモジュールから引く。null なら起動時のルールを使う。
        public Func<GameRules> CurrentRules
        {
            get => _currentRules;
            set
            {
                if (_context != null)
                {
                    throw new InvalidOperationException("set CurrentRules before the lobby module starts");
                }

                _currentRules = value;
            }
        }

        // ルールが無いサーバーは目的を持たないので、目的を載せる前と同じ TeamDeathmatchMode のままにする。
        public string Mode
        {
            get
            {
                var rules = _currentRules?.Invoke() ?? _context?.Rules?.Rules;
                return ServerLobby.ModeName(rules?.Objective ?? MatchObjective.None);
            }
        }

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _match = context.Match;
            _match.PlayerJoined += HandlePlayersChanged;
            _match.PlayerLeft += HandlePlayersChanged;
            UpdateSkillAverage();
            var backend = _createBackend(context.Log) ?? throw new InvalidOperationException("the EOS lobby factory returned no backend");
            _registration = new ServerLobbyRegistration(
                backend,
                _endpoint,
                context.Version,
                context.Rules?.Settings ?? NetMatchSettings.Default,
                context.Log,
                _clock,
                _sleep,
                reason => context.Control.Fail(ServerProgram.ExitLobbyFailed, ServerLobbyService.LogPrefix + reason),
                region: _region);
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _registration.Tick(_context.Match.HumanCount, _skillAverage, _context.Match.Map?.Name, Mode, _context.Control.IsDraining);
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
            Unsubscribe();
            if (_registration == null)
            {
                return;
            }

            try
            {
                _registration.CloseAndWait();
            }
            finally
            {
                _registration.Dispose();
                _registration = null;
            }
        }

        public void Dispose()
        {
            Unsubscribe();
            _registration?.Dispose();
            _registration = null;
        }

        private void HandlePlayersChanged(ServerPlayer player)
        {
            UpdateSkillAverage();
        }

        private void UpdateSkillAverage()
        {
            var ratings = new List<int?>();
            foreach (var player in _match.Players)
            {
                ratings.Add(player.SkillRating);
            }

            _skillAverage = ServerLobby.SkillAverage(ratings);
        }

        private void Unsubscribe()
        {
            if (_match == null)
            {
                return;
            }

            _match.PlayerJoined -= HandlePlayersChanged;
            _match.PlayerLeft -= HandlePlayersChanged;
            _match = null;
        }

        private static Func<float> StopwatchClock()
        {
            var watch = Stopwatch.StartNew();
            return () => (float)watch.Elapsed.TotalSeconds;
        }
    }
}
