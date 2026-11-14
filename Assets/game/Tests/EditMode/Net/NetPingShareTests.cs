using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    public sealed class NetPingShareTests
    {
        private const ulong _server = 0;
        private const ulong _alice = 1;
        private const ulong _bob = 2;
        private const ulong _carol = 3;

        // ほかのテストの物の近くで視線を引かないよう、離れた所に置く
        private static readonly Vector3 _origin = new Vector3(900f, 500f, 500f);

        private readonly List<ulong> _recipients = new List<ulong>();
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void OnlyConnectedPlayersOfThePingersTeamReceiveIt()
        {
            var members = new List<KeyValuePair<ulong, Team>>
            {
                new KeyValuePair<ulong, Team>(_alice, Team.Alpha),
                new KeyValuePair<ulong, Team>(_bob, Team.Bravo),
                new KeyValuePair<ulong, Team>(_carol, Team.Alpha),
                new KeyValuePair<ulong, Team>(_alice, Team.Alpha),
            };

            NetTeamDeathmatch.CollectPingRecipients(members, Team.Alpha, new[] { _server, _alice, _bob, _carol }, _server, false, _recipients);
            Assert.That(_recipients, Is.EqualTo(new[] { _alice, _carol }));

            NetTeamDeathmatch.CollectPingRecipients(members, Team.Bravo, new[] { _server, _alice, _bob, _carol }, _server, false, _recipients);
            Assert.That(_recipients, Is.EqualTo(new[] { _bob }));

            // 抜けた人には送らない
            NetTeamDeathmatch.CollectPingRecipients(members, Team.Alpha, new[] { _server, _alice }, _server, false, _recipients);
            Assert.That(_recipients, Is.EqualTo(new[] { _alice }));
        }

        [Test]
        public void TheHostIsARecipientButADedicatedServerIsNot()
        {
            var members = new List<KeyValuePair<ulong, Team>>
            {
                new KeyValuePair<ulong, Team>(_server, Team.Alpha),
                new KeyValuePair<ulong, Team>(_alice, Team.Alpha),
            };

            NetTeamDeathmatch.CollectPingRecipients(members, Team.Alpha, new[] { _server, _alice }, _server, true, _recipients);
            Assert.That(_recipients, Is.EqualTo(new[] { _server, _alice }));

            NetTeamDeathmatch.CollectPingRecipients(members, Team.Alpha, new[] { _server, _alice }, _server, false, _recipients);
            Assert.That(_recipients, Is.EqualTo(new[] { _alice }));
        }

        [Test]
        public void CollectingRecipientsRejectsMissingLists()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => NetTeamDeathmatch.CollectPingRecipients(null, Team.Alpha, new ulong[0], _server, false, _recipients));
            Assert.Throws<System.ArgumentNullException>(
                () => NetTeamDeathmatch.CollectPingRecipients(new List<KeyValuePair<ulong, Team>>(), Team.Alpha, new ulong[0], _server, false, null));
        }

        [TestCase(true, true, false, ExpectedResult = true)]
        [TestCase(false, true, false, ExpectedResult = false)]
        [TestCase(true, false, false, ExpectedResult = false)]
        [TestCase(true, true, true, ExpectedResult = false)]
        public bool TheOwnerOnlyRequestsAPingTheServerMayAccept(bool pingEnabled, bool alive, bool watchingDeathCamera)
        {
            return NetTeamDeathmatch.CanRequestPing(pingEnabled, alive, watchingDeathCamera);
        }

        [Test]
        public void AWallHidesAnEnemyButOtherPlayersDoNot()
        {
            var enemy = Player("Enemy", _origin + (Vector3.forward * 10f));
            Physics.SyncTransforms();

            Assert.That(NetTeamDeathmatch.HasPingLineOfSight(_origin, enemy, 150f), Is.True, "open ground");
            Assert.That(NetTeamDeathmatch.HasPingLineOfSight(_origin, enemy, 5f), Is.False, "out of reach");

            // 間に立つ味方は視線を遮らない
            Player("Ally", _origin + (Vector3.forward * 5f));
            Physics.SyncTransforms();
            Assert.That(NetTeamDeathmatch.HasPingLineOfSight(_origin, enemy, 150f), Is.True, "behind another player");

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _created.Add(wall);
            wall.transform.position = _origin + (Vector3.forward * 7f);
            wall.transform.localScale = new Vector3(4f, 10f, 0.2f);
            Physics.SyncTransforms();

            Assert.That(NetTeamDeathmatch.HasPingLineOfSight(_origin, enemy, 150f), Is.False, "behind a wall");
            Assert.That(NetTeamDeathmatch.HasPingLineOfSight(_origin, null, 150f), Is.False);
        }

        [Test]
        public void ThePingSitsAtTheMiddleOfTheBody()
        {
            var player = Player("Body", _origin + (Vector3.right * 20f));

            Assert.That(NetTeamDeathmatch.TryGetBodyCapsule(player, out var bottom, out var top, out _), Is.True);
            Assert.That(Vector3.Distance(NetTeamDeathmatch.PingAimPoint(player), Vector3.Lerp(bottom, top, 0.5f)), Is.LessThan(1e-4f));
        }

        [Test]
        public void TheSpecCannotBeCleared()
        {
            var go = new GameObject("Match");
            _created.Add(go);
            go.AddComponent<NetworkObject>();
            var match = go.AddComponent<NetTeamDeathmatch>();

            Assert.That(match.PingSpec, Is.SameAs(PingSpec.Default));
            var spec = new PingSpec(minIntervalSeconds: 2f);
            match.PingSpec = spec;
            Assert.That(match.PingSpec, Is.SameAs(spec));
            Assert.Throws<System.ArgumentNullException>(() => match.PingSpec = null);
            Assert.That(match.SharedPings, Is.Not.Null);
        }

        private NetPlayer Player(string name, Vector3 position)
        {
            var go = new GameObject(name);
            _created.Add(go);
            go.transform.position = position;
            var controller = go.AddComponent<CharacterController>();
            controller.height = 1.8f;
            controller.radius = 0.4f;
            go.AddComponent<NetworkObject>();
            return go.AddComponent<NetPlayer>();
        }
    }
}
