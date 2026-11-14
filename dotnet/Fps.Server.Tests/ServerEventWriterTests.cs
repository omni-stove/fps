using System;
using System.IO;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerEventWriterTests
    {
        [Test]
        public void WritesOneLinePerEvent()
        {
            using (var output = new StringWriter())
            using (var log = new StringWriter())
            {
                var writer = new ServerEventWriter(true, output, log);

                writer.Write("{\"type\":\"a\"}");
                writer.Write("{\"type\":\"b\"}");

                Assert.That(output.ToString(), Is.EqualTo("{\"type\":\"a\"}" + Environment.NewLine + "{\"type\":\"b\"}" + Environment.NewLine));
                Assert.That(log.ToString(), Is.Empty);
            }
        }

        [Test]
        public void DisabledWriterWritesNothing()
        {
            using (var output = new StringWriter())
            using (var log = new StringWriter())
            {
                var writer = new ServerEventWriter(false, output, log);

                writer.Write("{}");

                Assert.That(output.ToString(), Is.Empty);
            }
        }

        [Test]
        public void BrokenOutputWarnsOnceAndTurnsJsonOff()
        {
            using (var log = new StringWriter())
            {
                var output = new StringWriter();
                output.Dispose();
                var writer = new ServerEventWriter(true, output, log);

                writer.Write("{}");
                writer.Write("{}");

                Assert.That(writer.Enabled, Is.False);
                Assert.That(log.ToString(), Does.StartWith("[Net] cannot write match events to stdout ("));
                Assert.That(log.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries), Has.Length.EqualTo(1));
            }
        }
    }
}
