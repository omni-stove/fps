using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerHost
    {
        public const string LogTag = "[ServerHost]";

        private readonly List<IServerModule> _modules = new List<IServerModule>();
        private int _startedCount;
        private ServerContext _context;

        public ServerHostState State { get; private set; }

        public long TickCount { get; private set; }

        public IReadOnlyList<IServerModule> Modules => _modules;

        public void Register(IServerModule module)
        {
            ArgumentNullException.ThrowIfNull(module);

            if (State != ServerHostState.NotStarted)
            {
                throw new InvalidOperationException("modules can only be registered before the server starts");
            }

            if (_modules.Contains(module))
            {
                throw new InvalidOperationException("module " + module.Name + " is already registered");
            }

            _modules.Add(module);
        }

        public void Start(ServerContext context)
        {
            if (State != ServerHostState.NotStarted)
            {
                throw new InvalidOperationException("the server can only be started once (state is " + State + ")");
            }

            _context = context ?? throw new ArgumentNullException(nameof(context));
            State = ServerHostState.Running;
            try
            {
                foreach (var module in _modules)
                {
                    module.Start(context);
                    _startedCount++;
                }
            }
            catch
            {
                var failed = _modules[_startedCount];
                context.Log.WriteLine(LogTag + " module " + failed.Name + " failed to start; stopping the started modules");
                StopStartedModules();
                throw;
            }

            context.Log.WriteLine(LogTag + " started with " + _modules.Count.ToString(CultureInfo.InvariantCulture) + " module(s)");
        }

        public void Tick(float deltaSeconds)
        {
            ThrowIfNotRunning();
            if (float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds) || deltaSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "delta seconds must be finite and not negative");
            }

            TickCount++;
            foreach (var module in _modules)
            {
                module.Tick(TickCount, deltaSeconds);
            }
        }

        public void OnConnected(ConnectionId connection)
        {
            ThrowIfNotRunning();
            ThrowIfNone(connection);
            foreach (var module in _modules)
            {
                module.OnConnected(connection);
            }
        }

        public void OnDisconnected(ConnectionId connection)
        {
            ThrowIfNotRunning();
            ThrowIfNone(connection);
            foreach (var module in _modules)
            {
                module.OnDisconnected(connection);
            }
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            ThrowIfNotRunning();
            ThrowIfNone(connection);
            ArgumentNullException.ThrowIfNull(message);
            foreach (var module in _modules)
            {
                module.OnMessage(connection, message);
            }
        }

        public void Stop()
        {
            if (State == ServerHostState.NotStarted)
            {
                State = ServerHostState.Stopped;
                return;
            }

            if (State == ServerHostState.Stopped)
            {
                return;
            }

            var failures = StopStartedModules();
            _context.Log.WriteLine(LogTag + " stopped after " + TickCount.ToString(CultureInfo.InvariantCulture) + " tick(s)");
            if (failures.Count > 0)
            {
                throw new AggregateException("some server modules failed to stop", failures);
            }
        }

        private List<Exception> StopStartedModules()
        {
            State = ServerHostState.Stopped;
            var failures = new List<Exception>();
            for (var i = _startedCount - 1; i >= 0; i--)
            {
                var module = _modules[i];
#pragma warning disable CA1031 // 1 つのモジュールが止まらなくても、残りのモジュールが握っている接続や資源を手放させるため
                try
                {
                    module.Stop();
                }
                catch (Exception e)
                {
                    _context.Log.WriteLine(LogTag + " module " + module.Name + " failed to stop: " + e.Message);
                    failures.Add(e);
                }
#pragma warning restore CA1031
            }

            _startedCount = 0;
            return failures;
        }

        private void ThrowIfNotRunning()
        {
            if (State != ServerHostState.Running)
            {
                throw new InvalidOperationException("the server is not running (state is " + State + ")");
            }
        }

        private static void ThrowIfNone(ConnectionId connection)
        {
            if (!connection.IsValid)
            {
                throw new ArgumentException("a connection hook needs a connection", nameof(connection));
            }
        }
    }
}
