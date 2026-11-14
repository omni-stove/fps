using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>
    /// 登録した <see cref="IServerModule"/> に、起動・tick・接続・切断・停止を配る。何をするかはモジュールが決め、
    /// ここは順番と、失敗したときに起動済みのモジュールを止めることだけを受け持つ。
    /// </summary>
    internal sealed class ServerHost
    {
        public const string LogTag = "[ServerHost]";

        private readonly List<IServerModule> _modules = new List<IServerModule>();
        private int _startedCount;
        private ServerContext _context;

        public ServerHostState State { get; private set; }

        /// <summary>今までに進めた tick の数。</summary>
        public long TickCount { get; private set; }

        /// <summary>登録した順のモジュール。</summary>
        public IReadOnlyList<IServerModule> Modules => _modules;

        /// <summary>モジュールを末尾に足す。起動の前にだけ呼べる。</summary>
        /// <exception cref="InvalidOperationException">起動した後か、同じモジュールを 2 度足した。</exception>
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

        /// <summary>
        /// 登録した順にモジュールを起動する。どれかが投げたら、起動済みのものを逆順に止めて
        /// <see cref="ServerHostState.Stopped"/> になり、その例外を投げ直す。
        /// </summary>
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

        /// <summary>tick を 1 つ進め、登録した順にモジュールへ配る。</summary>
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

        /// <summary>
        /// 登録と逆の順にモジュールを止める。止まらなかったモジュールがあっても残りを止めてから
        /// <see cref="AggregateException"/> で知らせる。起動前・停止後に呼んでも何もしない。
        /// </summary>
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
