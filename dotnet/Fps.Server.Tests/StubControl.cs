using System.Collections.Generic;
using Fps.Protocol;

namespace Fps.Server.Tests
{
    internal sealed class StubControl : IServerControl
    {
        public bool IsDraining { get; set; }

        public List<string> Failures { get; } = new List<string>();

        public int ExitCode { get; private set; }

        public List<(ConnectionId Connection, string Reason)> Kicks { get; } = new List<(ConnectionId Connection, string Reason)>();

        public void Fail(int exitCode, string message)
        {
            Failures.Add(message);
            ExitCode = exitCode;
        }

        public void Kick(ConnectionId connection, string reason, string logText)
        {
            Kicks.Add((connection, reason));
        }
    }
}
