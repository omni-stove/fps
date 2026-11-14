using System;
using System.Collections.Generic;
using System.Globalization;
using Fps.Protocol;

namespace Fps.Server.Tests
{
    /// <summary>呼ばれたフックを共有の記録に足していくモジュール。指定したフックで投げることもできる。</summary>
    internal sealed class RecordingModule : IServerModule
    {
        private readonly List<string> _calls;
        private ServerContext _context;

        public RecordingModule(string name, List<string> calls)
        {
            Name = name;
            _calls = calls;
        }

        public string Name { get; }

        public bool ThrowOnStart { get; set; }

        public bool ThrowOnStop { get; set; }

        public ConnectionId KickOnNextTick { get; set; }

        public void Start(ServerContext context)
        {
            _context = context;
            _calls.Add(Name + ".Start");
            if (ThrowOnStart)
            {
                throw new InvalidOperationException(Name + " cannot start");
            }
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _calls.Add(Name + ".Tick" + tick.ToString(CultureInfo.InvariantCulture));
            if (KickOnNextTick.IsValid)
            {
                _context.Kick(KickOnNextTick, "test-kick", "kicked by " + Name);
                _calls.Add(Name + ".Kicked" + KickOnNextTick);
                KickOnNextTick = ConnectionId.None;
            }
        }

        public void OnConnected(ConnectionId connection)
        {
            _calls.Add(Name + ".Connected" + connection);
        }

        public void OnDisconnected(ConnectionId connection)
        {
            _calls.Add(Name + ".Disconnected" + connection);
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            _calls.Add(Name + ".Message" + connection + ":" + message.Id.ToString(CultureInfo.InvariantCulture));
        }

        public void Stop()
        {
            _calls.Add(Name + ".Stop");
            if (ThrowOnStop)
            {
                throw new InvalidOperationException(Name + " cannot stop");
            }
        }
    }
}
