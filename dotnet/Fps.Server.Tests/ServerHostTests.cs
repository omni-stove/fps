using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerHostTests
    {
        private List<string> _calls;
        private StringWriter _log;

        [SetUp]
        public void SetUp()
        {
            _calls = new List<string>();
            _log = new StringWriter();
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
        }

        [Test]
        public void HooksRunInRegistrationOrderAndStopInReverse()
        {
            var host = new ServerHost();
            host.Register(new RecordingModule("a", _calls));
            host.Register(new RecordingModule("b", _calls));

            host.Start(Context());
            host.Tick(0.1f);
            host.OnConnected(new ConnectionId(7));
            host.OnDisconnected(new ConnectionId(7));
            host.Stop();

            Assert.That(_calls, Is.EqualTo(new[]
            {
                "a.Start", "b.Start",
                "a.Tick1", "b.Tick1",
                "a.Connected#7", "b.Connected#7",
                "a.Disconnected#7", "b.Disconnected#7",
                "b.Stop", "a.Stop",
            }));
            Assert.That(host.State, Is.EqualTo(ServerHostState.Stopped));
            Assert.That(host.TickCount, Is.EqualTo(1));
        }

        [Test]
        public void FailedStartStopsOnlyTheStartedModules()
        {
            var host = new ServerHost();
            host.Register(new RecordingModule("a", _calls));
            host.Register(new RecordingModule("b", _calls) { ThrowOnStart = true });
            host.Register(new RecordingModule("c", _calls));

            Assert.Throws<InvalidOperationException>(() => host.Start(Context()));

            Assert.That(_calls, Is.EqualTo(new[] { "a.Start", "b.Start", "a.Stop" }));
            Assert.That(host.State, Is.EqualTo(ServerHostState.Stopped));
            Assert.That(_log.ToString(), Does.Contain("module b failed to start"));
        }

        [Test]
        public void FailedStopStillStopsTheRest()
        {
            var host = new ServerHost();
            host.Register(new RecordingModule("a", _calls));
            host.Register(new RecordingModule("b", _calls) { ThrowOnStop = true });
            host.Start(Context());

            var e = Assert.Throws<AggregateException>(host.Stop);

            Assert.That(e.InnerExceptions, Has.Count.EqualTo(1));
            Assert.That(_calls, Does.Contain("a.Stop"));
            Assert.That(host.State, Is.EqualTo(ServerHostState.Stopped));
        }

        [Test]
        public void StopTwiceIsHarmless()
        {
            var host = new ServerHost();
            host.Register(new RecordingModule("a", _calls));
            host.Start(Context());

            host.Stop();
            host.Stop();

            Assert.That(_calls, Is.EqualTo(new[] { "a.Start", "a.Stop" }));
        }

        [Test]
        public void RegisterIsOnlyAllowedBeforeStart()
        {
            var host = new ServerHost();
            var module = new RecordingModule("a", _calls);
            host.Register(module);

            Assert.Throws<InvalidOperationException>(() => host.Register(module));
            Assert.Throws<ArgumentNullException>(() => host.Register(null));

            host.Start(Context());
            Assert.Throws<InvalidOperationException>(() => host.Register(new RecordingModule("b", _calls)));
        }

        [Test]
        public void HooksNeedARunningServer()
        {
            var host = new ServerHost();

            Assert.Throws<InvalidOperationException>(() => host.Tick(0.1f));
            Assert.Throws<InvalidOperationException>(() => host.OnConnected(new ConnectionId(1)));

            host.Start(Context());
            host.Stop();

            Assert.Throws<InvalidOperationException>(() => host.Tick(0.1f));
            Assert.Throws<InvalidOperationException>(() => host.OnDisconnected(new ConnectionId(1)));
            Assert.Throws<InvalidOperationException>(() => host.Start(Context()));
        }

        [Test]
        public void BadTickAndConnectionAreRejected()
        {
            var host = new ServerHost();
            host.Start(Context());

            Assert.Throws<ArgumentOutOfRangeException>(() => host.Tick(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => host.Tick(float.NaN));
            Assert.Throws<ArgumentException>(() => host.OnConnected(ConnectionId.None));
            Assert.That(host.TickCount, Is.Zero);
        }

        private ServerContext Context()
        {
            return new ServerContext(
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort), "1.0.0", _log);
        }
    }
}
