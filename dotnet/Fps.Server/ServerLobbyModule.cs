using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Fps.Protocol;
using Fps.Server.Eos;

namespace Fps.Server
{
    /// <summary><c>-register-lobby</c> のときだけ足す、EOS のロビーにサーバーを載せておくモジュール。</summary>
    internal sealed class ServerLobbyModule : IServerModule, IDisposable
    {
        private readonly Func<TextWriter, IServerLobbyBackend> _createBackend;
        private readonly string _endpoint;
        private readonly Func<float> _clock;
        private readonly Action<int> _sleep;
        private ServerContext _context;
        private ServerLobbyRegistration _registration;

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

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            var backend = _createBackend(context.Log) ?? throw new InvalidOperationException("the EOS lobby factory returned no backend");
            _registration = new ServerLobbyRegistration(
                backend,
                _endpoint,
                context.Version,
                context.Log,
                _clock,
                _sleep,
                reason => context.Control.Fail(ServerProgram.ExitLobbyFailed, ServerLobbyService.LogPrefix + reason));
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _registration.Tick(_context.Match.PlayerCount, _context.Control.IsDraining);
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

        /// <summary>ロビーを閉じ終えるのを待たずに SDK を手放す。<see cref="Stop"/> を通らずに捨てるとき用。</summary>
        public void Dispose()
        {
            _registration?.Dispose();
            _registration = null;
        }

        private static Func<float> StopwatchClock()
        {
            var watch = Stopwatch.StartNew();
            return () => (float)watch.Elapsed.TotalSeconds;
        }
    }
}
