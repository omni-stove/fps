using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Shooter.Server.Tests
{
    public sealed class ServerLoopTests
    {
        [Test]
        public void AStopRequestAfterTheLoopIsDisposedIsIgnored()
        {
            using (var transport = new RecordingTransport())
            using (var log = new StringWriter())
            {
                var environment = new Dictionary<string, string>();
                Assert.That(
                    ServerLaunch.TryParse(Array.Empty<string>(), name => environment.TryGetValue(name, out var value) ? value : null, out var launch, out var error),
                    Is.True,
                    error);
                var server = new MatchServer(
                    launch, transport, new ServerHost(), TestMaps.Data(), log, new ServerEventWriter(false, log, log), () => DateTime.UnixEpoch);
                var loop = new ServerLoop(server);
                loop.Dispose();

                Assert.That(() => loop.RequestStop(), Throws.Nothing);
            }
        }
    }
}
