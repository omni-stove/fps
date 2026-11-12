using System;
using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Fps.Net.Tests
{
    public sealed class NetworkSetupTests
    {
        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
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
            Assert.That(manager.NetworkConfig.ConnectionApproval, Is.False);
            Assert.That(manager.NetworkConfig.EnableSceneManagement, Is.False);
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
            Assert.Throws<ArgumentNullException>(() => NetworkSetup.Configure(target, null, prefab));
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
    }
}
