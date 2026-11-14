using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Net.Tests
{
    public sealed class NetworkSetupTests
    {
        private const string ServerId = "0123456789abcdef0123456789abcdef";

        private readonly List<GameObject> _created = new List<GameObject>();
        private Func<GameObject, NetTransportPlan, NetworkTransport> _savedEosFactory;

        [SetUp]
        public void SetUp()
        {
            _savedEosFactory = NetTransportFactories.Eos;
        }

        [TearDown]
        public void TearDown()
        {
            NetTransportFactories.Eos = _savedEosFactory;

            foreach (var go in _created)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            _created.Clear();
        }

        private GameObject Create(string name)
        {
            var go = new GameObject(name);
            _created.Add(go);
            return go;
        }

        private GameObject CreatePlayerPrefab()
        {
            var prefab = Create("TestPlayer");
            prefab.AddComponent<NetworkObject>();
            return prefab;
        }

        [Test]
        public void WiresTransportAndPlayerPrefab()
        {
            var target = Create("Net");
            var prefab = CreatePlayerPrefab();
            var options = new NetLaunchOptions(NetLaunchMode.Host, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort);

            var manager = NetworkSetup.Configure(target, options, prefab);

            var transport = target.GetComponent<UnityTransport>();
            Assert.That(manager, Is.SameAs(target.GetComponent<NetworkManager>()));
            Assert.That(transport, Is.Not.Null);
            Assert.That(manager.NetworkConfig.NetworkTransport, Is.SameAs(transport));
            Assert.That(manager.NetworkConfig.PlayerPrefab, Is.SameAs(prefab));
            Assert.That(manager.NetworkConfig.ConnectionApproval, Is.True);
            Assert.That(manager.NetworkConfig.EnableSceneManagement, Is.True, "clients follow the server's -map scene");
            Assert.That(manager.IsListening, Is.False);
        }

        [Test]
        public void ClientConnectsToGivenAddressAndPort()
        {
            var target = Create("Net");
            var options = new NetLaunchOptions(NetLaunchMode.Client, "192.168.1.20", 9001);

            NetworkSetup.Configure(target, options, CreatePlayerPrefab());

            var transport = target.GetComponent<UnityTransport>();
            Assert.That(transport.ConnectionData.Address, Is.EqualTo("192.168.1.20"));
            Assert.That(transport.ConnectionData.Port, Is.EqualTo((ushort)9001));
            Assert.That(transport.ConnectTimeoutMS, Is.EqualTo(NetworkSetup.ConnectTimeoutMs));
            Assert.That(transport.MaxConnectAttempts, Is.EqualTo(NetworkSetup.MaxConnectAttempts));
        }

        [Test]
        public void ServerListensOnGivenAddress()
        {
            var target = Create("Net");
            var options = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, 7000);

            NetworkSetup.Configure(target, options, CreatePlayerPrefab());

            var transport = target.GetComponent<UnityTransport>();
            Assert.That(transport.ConnectionData.ServerListenAddress, Is.EqualTo(NetLaunchOptions.AnyAddress));
            Assert.That(transport.ConnectionData.Port, Is.EqualTo((ushort)7000));
        }

        [Test]
        public void RejectsPrefabWithoutNetworkObject()
        {
            var target = Create("Net");
            var options = new NetLaunchOptions(NetLaunchMode.Host, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort);

            Assert.Throws<ArgumentException>(() => NetworkSetup.Configure(target, options, Create("NotNetworked")));
            Assert.That(target.GetComponent<NetworkManager>(), Is.Null);
            Assert.That(target.GetComponent<UnityTransport>(), Is.Null);
        }

        [Test]
        public void RejectsNetworkPrefabWithoutNetworkObject()
        {
            var target = Create("Net");
            var options = new NetLaunchOptions(NetLaunchMode.Host, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort);

            Assert.Throws<ArgumentException>(() =>
                NetworkSetup.Configure(target, options, CreatePlayerPrefab(), new[] { Create("NotNetworked") }));
            Assert.Throws<ArgumentException>(() =>
                NetworkSetup.Configure(target, options, CreatePlayerPrefab(), new GameObject[] { null }));
            Assert.That(target.GetComponent<NetworkManager>(), Is.Null);
            Assert.That(target.GetComponent<UnityTransport>(), Is.Null);
        }

        [Test]
        public void RejectsNullArguments()
        {
            var target = Create("Net");
            var prefab = CreatePlayerPrefab();
            var options = new NetLaunchOptions(NetLaunchMode.Host, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort);

            Assert.Throws<ArgumentNullException>(() => NetworkSetup.Configure(null, options, prefab));
            Assert.Throws<ArgumentNullException>(() => NetworkSetup.Configure(target, (NetLaunchOptions)null, prefab));
            Assert.Throws<ArgumentNullException>(() => NetworkSetup.Configure(target, (NetTransportPlan)null, prefab));
            Assert.Throws<ArgumentNullException>(() => NetworkSetup.Configure(target, options, null));
        }

        [Test]
        public void RejectsNonRootTarget()
        {
            var parent = Create("Parent");
            var child = Create("Child");
            child.transform.SetParent(parent.transform);
            var options = new NetLaunchOptions(NetLaunchMode.Host, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort);

            Assert.Throws<ArgumentException>(() => NetworkSetup.Configure(child, options, CreatePlayerPrefab()));
        }

        [Test]
        public void RejectsConfiguringTwice()
        {
            var target = Create("Net");
            var prefab = CreatePlayerPrefab();
            var options = new NetLaunchOptions(NetLaunchMode.Host, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort);
            NetworkSetup.Configure(target, options, prefab);

            Assert.Throws<InvalidOperationException>(() => NetworkSetup.Configure(target, options, prefab));
        }

        private static NetLaunchOptions EosClientOptions()
        {
            return new NetLaunchOptions(
                NetLaunchMode.Client, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Eos, ServerId);
        }

        private static EosCredentials Credentials()
        {
            return new EosCredentials("product", "sandbox", "deployment", "client", "secret");
        }

        [Test]
        public void EosOptionsNeedAPlan()
        {
            var target = Create("Net");

            Assert.Throws<ArgumentException>(() => NetworkSetup.Configure(target, EosClientOptions(), CreatePlayerPrefab()));
            Assert.That(target.GetComponent<NetworkTransport>(), Is.Null);
        }

        [Test]
        public void EosPlanUsesRegisteredFactory()
        {
            var target = Create("Net");
            var credentials = Credentials();
            var options = EosClientOptions();
            var antiCheat = AntiCheatSettings.Disabled("disabled for the test");
            var plan = NetTransportPlan.Decide(options, credentials, null).WithAntiCheat(antiCheat);
            NetTransportPlan receivedPlan = null;
            NetTransportFactories.Eos = (go, p) =>
            {
                receivedPlan = p;
                // 本物の EOS transport は EOS plugin のアセンブリにあるので、ここでは別の transport で代える
                return go.AddComponent<UnityTransport>();
            };

            var manager = NetworkSetup.Configure(target, plan, CreatePlayerPrefab());

            Assert.That(receivedPlan, Is.SameAs(plan));
            Assert.That(receivedPlan.Options, Is.SameAs(options));
            Assert.That(receivedPlan.Credentials, Is.SameAs(credentials));
            Assert.That(receivedPlan.AntiCheat, Is.SameAs(antiCheat));
            Assert.That(manager.NetworkConfig.NetworkTransport, Is.SameAs(target.GetComponent<NetworkTransport>()));
        }

        [Test]
        public void EosPlanWithoutFactoryFails()
        {
            var target = Create("Net");
            NetTransportFactories.Eos = null;
            var plan = NetTransportPlan.Decide(EosClientOptions(), Credentials(), null);

            Assert.Throws<InvalidOperationException>(() => NetworkSetup.Configure(target, plan, CreatePlayerPrefab()));
            Assert.That(target.GetComponent<NetworkManager>(), Is.Null);
        }

        [Test]
        public void EosFactoryMustAddToTarget()
        {
            var target = Create("Net");
            var elsewhere = Create("Elsewhere");
            NetTransportFactories.Eos = (go, p) => elsewhere.AddComponent<UnityTransport>();
            var plan = NetTransportPlan.Decide(EosClientOptions(), Credentials(), null);

            Assert.Throws<InvalidOperationException>(() => NetworkSetup.Configure(target, plan, CreatePlayerPrefab()));
        }

        [Test]
        public void FallbackPlanUsesUnityTransport()
        {
            var target = Create("Net");
            NetTransportFactories.Eos = (go, p) => throw new AssertionException("EOS factory must not be used");
            var plan = NetTransportPlan.Decide(EosClientOptions(), null, EosCredentials.VariableNames);

            var manager = NetworkSetup.Configure(target, plan, CreatePlayerPrefab());

            var transport = target.GetComponent<UnityTransport>();
            Assert.That(plan.FellBackToUdp, Is.True);
            Assert.That(manager.NetworkConfig.NetworkTransport, Is.SameAs(transport));
            Assert.That(transport.ConnectionData.Address, Is.EqualTo(NetLaunchOptions.DefaultAddress));
            Assert.That(transport.ConnectionData.Port, Is.EqualTo(NetLaunchOptions.DefaultPort));
        }

        [Test]
        public void SendsTheVersionAndDeviceKeyAndApprovesThroughConnectionKeys()
        {
            var target = Create("Net");
            var options = new NetLaunchOptions(NetLaunchMode.Client, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort);

            var manager = NetworkSetup.Configure(target, options, CreatePlayerPrefab());

            Assert.That(NetworkSetup.TryGetLocalVersion(out var localVersion, out _), Is.True);
            var keys = target.GetComponent<NetConnectionKeys>();
            Assert.That(keys, Is.Not.Null);
            Assert.That(keys.ServerVersion, Is.EqualTo(localVersion));
            Assert.That(manager.ConnectionApprovalCallback, Is.Not.Null);
            Assert.That(ConnectionPayload.TryDecode(manager.NetworkConfig.ConnectionData, out var version, out var key), Is.True);
            Assert.That(version, Is.EqualTo(localVersion));
            Assert.That(key, Is.EqualTo(NetConnectionKeys.DeviceKey));
        }

        private NetConnectionKeys CreateKeys(string serverVersion)
        {
            var keys = Create("Keys").AddComponent<NetConnectionKeys>();
            keys.ServerVersion = serverVersion;
            return keys;
        }

        [Test]
        public void ConnectionKeysRememberTheKeyOfAClientOnTheSameVersion()
        {
            var keys = CreateKeys("0.2.0");
            var response = new NetworkManager.ConnectionApprovalResponse();

            keys.Approve(
                new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = 3, Payload = ConnectionPayload.Encode("0.2.0", ServerId) },
                response);

            Assert.That(response.Approved, Is.True);
            Assert.That(response.CreatePlayerObject, Is.True);
            Assert.That(response.Pending, Is.False);
            Assert.That(keys.TryGet(3, out var key), Is.True);
            Assert.That(key, Is.EqualTo(ServerId));
            Assert.That(keys.TryGet(4, out _), Is.False);
        }

        [Test]
        public void ConnectionKeysRejectAnotherVersionWithTheReason()
        {
            var keys = CreateKeys("0.2.0");
            var response = new NetworkManager.ConnectionApprovalResponse();
            LogAssert.Expect(LogType.Warning, new Regex(@"rejected client 6: version mismatch \(client 0\.1\.0, server 0\.2\.0\)"));

            keys.Approve(
                new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = 6, Payload = ConnectionPayload.Encode("0.1.0", ServerId) },
                response);

            Assert.That(response.Approved, Is.False);
            Assert.That(response.CreatePlayerObject, Is.False);
            Assert.That(response.Pending, Is.False);
            Assert.That(response.Reason, Is.EqualTo(DisconnectNotice.VersionMismatchReason("0.2.0")));
            Assert.That(keys.TryGet(6, out _), Is.False);
            Assert.That(DisconnectNotice.Classify(response.Reason, false, "0.1.0").Kind, Is.EqualTo(DisconnectKind.VersionMismatch));
        }

        [Test]
        public void ConnectionKeysRejectAnUnreadablePayload()
        {
            var keys = CreateKeys("0.2.0");
            var response = new NetworkManager.ConnectionApprovalResponse();
            LogAssert.Expect(LogType.Warning, new Regex("rejected client 5: unreadable connection payload"));

            keys.Approve(
                new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = 5, Payload = RejoinKey.Encode(ServerId) },
                response);

            Assert.That(response.Approved, Is.False);
            Assert.That(response.Reason, Is.EqualTo(DisconnectNotice.RejectedReason(ConnectionApproval.InvalidPayloadDetail)));
            Assert.That(keys.TryGet(5, out _), Is.False);
            Assert.That(keys.Count, Is.Zero);
        }

        [Test]
        public void ConnectionKeysRejectEvenTheSameVersionWhileDraining()
        {
            var keys = CreateKeys("0.2.0");
            keys.Draining = true;
            var response = new NetworkManager.ConnectionApprovalResponse();
            LogAssert.Expect(LogType.Warning, new Regex("rejected client 7: the server is draining"));

            keys.Approve(
                new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = 7, Payload = ConnectionPayload.Encode("0.2.0", ServerId) },
                response);

            Assert.That(response.Approved, Is.False);
            Assert.That(response.CreatePlayerObject, Is.False);
            Assert.That(response.Pending, Is.False);
            Assert.That(response.Reason, Is.EqualTo(DisconnectNotice.RejectedReason(DisconnectNotice.ServerDrainingDetail)));
            Assert.That(keys.TryGet(7, out _), Is.False);
            Assert.That(
                DisconnectNotice.Classify(response.Reason, false, "0.2.0").Message, Is.EqualTo(DisconnectNotice.ServerDrainingMessage));
        }

        [Test]
        public void ConnectionKeysNeedAServerVersion()
        {
            var keys = Create("Keys").AddComponent<NetConnectionKeys>();

            Assert.Throws<ArgumentException>(() => keys.ServerVersion = "v0.2.0");
            Assert.Throws<InvalidOperationException>(() => keys.Approve(
                new NetworkManager.ConnectionApprovalRequest { ClientNetworkId = 1, Payload = ConnectionPayload.Encode("0.2.0", ServerId) },
                new NetworkManager.ConnectionApprovalResponse()));
        }

        [Test]
        public void DisconnectHelpersIgnoreAManagerThatIsNotServing()
        {
            var manager = NetworkSetup.Configure(
                Create("Net"),
                new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort),
                CreatePlayerPrefab());

            Assert.That(NetworkSetup.DisconnectRemoteClients(manager, DisconnectNotice.ServerStoppedReason), Is.Zero);
            Assert.Throws<ArgumentNullException>(() => NetworkSetup.ClassifyDisconnect(null, true));
            Assert.Throws<ArgumentNullException>(() => NetworkSetup.DisconnectRemoteClients(null, DisconnectNotice.ServerStoppedReason));
        }
    }
}
