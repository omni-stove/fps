using System.Collections.Generic;

namespace Fps.Server.Tests
{
    internal sealed class StubControl : IServerControl
    {
        public bool IsDraining { get; set; }

        public List<string> Failures { get; } = new List<string>();

        public int ExitCode { get; private set; }

        public void Fail(int exitCode, string message)
        {
            Failures.Add(message);
            ExitCode = exitCode;
        }
    }
}
