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

        public RecordingModule(string name, List<string> calls)
        {
            Name = name;
            _calls = calls;
        }

        public string Name { get; }

        public bool ThrowOnStart { get; set; }

        public bool ThrowOnStop { get; set; }

        /// <summary>0 でなければ、最初の tick でこの終了コードでサーバーを止めさせる。</summary>
        public int FailOnTickWith { get; set; }

        private ServerContext Context { get; set; }

        public void Start(ServerContext context)
        {
            Context = context;
            _calls.Add(Name + ".Start");
            if (ThrowOnStart)
            {
                throw new InvalidOperationException(Name + " cannot start");
            }
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _calls.Add(Name + ".Tick" + tick.ToString(CultureInfo.InvariantCulture));
            if (FailOnTickWith != 0)
            {
                Context.Control.Fail(FailOnTickWith, Name + " cannot go on");
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
