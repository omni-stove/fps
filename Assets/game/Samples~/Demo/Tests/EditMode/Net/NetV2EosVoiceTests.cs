using System.Collections.Generic;
using NUnit.Framework;
using Shooter.NetV2;

namespace Shooter.Net.Tests
{
    public sealed class NetV2EosVoiceTests
    {
        private readonly List<string> _logs = new List<string>();
        private IEosClientTransportFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();
            _factory = NetV2Eos.Factory;
        }

        [TearDown]
        public void TearDown()
        {
            NetV2Eos.Factory = _factory;
        }

        [Test]
        public void NoMatchLobbyMeansNoVoice()
        {
            Assert.That(NetV2Eos.CreateVoiceSession(null, _logs.Add), Is.Null);
            Assert.That(NetV2Eos.CreateVoiceSession("", _logs.Add), Is.Null);
            Assert.That(_logs, Has.Count.EqualTo(2));
            Assert.That(_logs, Has.All.StartWith(VoiceLog.Off("the match has no lobby")));
        }

        [Test]
        public void ABuildWithoutEosHasNoVoiceEvenWithALobby()
        {
            NetV2Eos.Factory = null;

            Assert.That(NetV2Eos.CreateVoiceSession("lobby-1", _logs.Add), Is.Null);
            Assert.That(_logs, Is.EqualTo(new[] { VoiceLog.Off("EOS is not in this build") }));
        }
    }
}
