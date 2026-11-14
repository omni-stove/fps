using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    public sealed class NetUiTests
    {
        private GameObject _playerObject;
        private NetPlayer _player;
        private INetHud _created;

        [SetUp]
        public void SetUp()
        {
            NetUi.HudFactory = null;
            _playerObject = new GameObject("NetUiTests.Player");
            _playerObject.AddComponent<NetworkObject>();
            _player = _playerObject.AddComponent<NetPlayer>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_created is NetHud netHud && netHud != null)
            {
                // Edit Mode では OnDestroy が走らないので、試合の差し替えの購読を他のテストへ持ち越さない
                netHud.UnbindAll();
                var settings = netHud.Hud.OwnedPanelSettings;
                Object.DestroyImmediate(netHud.gameObject);
                if (settings != null)
                {
                    Object.DestroyImmediate(settings);
                }
            }

            Object.DestroyImmediate(_playerObject);
            NetUi.HudFactory = null;
            NetUi.UseDefaultHudFactory(new NetHudFactory());
        }

        [Test]
        public void TheScreenAssemblyRegistersTheDefaultFactory()
        {
            Assert.That(NetUi.HudFactory, Is.InstanceOf<NetHudFactory>());
        }

        [Test]
        public void TheDefaultFactoryCreatesTheNetHud()
        {
            _created = NetUi.CreateHud(_player);

            Assert.That(_created, Is.InstanceOf<NetHud>());
            Assert.That(_created.gameObject.name, Is.EqualTo(NetHud.ObjectName));
            Assert.That(((NetHud)_created).Player, Is.SameAs(_player));
        }

        [Test]
        public void NoHudIsCreatedWhenNoFactoryIsRegistered()
        {
            NetUi.ClearDefaults();

            Assert.That(NetUi.HudFactory, Is.Null);
            Assert.That(NetUi.CreateHud(_player), Is.Null);
            Assert.That(GameObject.Find(NetHud.ObjectName), Is.Null);
        }

        [Test]
        public void AReplacedFactoryIsUsedUntilItIsCleared()
        {
            var factory = new RecordingFactory();
            NetUi.HudFactory = factory;
            NetUi.UseDefaultHudFactory(new NetHudFactory());

            _created = NetUi.CreateHud(_player);

            Assert.That(factory.Players, Is.EqualTo(new[] { _player }));
            Assert.That(_created, Is.SameAs(factory.Hud));

            NetUi.HudFactory = null;
            Assert.That(NetUi.HudFactory, Is.InstanceOf<NetHudFactory>());
        }

        [Test]
        public void ARegisteredFactoryThatReturnsNothingFails()
        {
            NetUi.HudFactory = new RecordingFactory { ReturnsNothing = true };

            Assert.Throws<InvalidOperationException>(() => NetUi.CreateHud(_player));
        }

        [Test]
        public void CreatingAHudNeedsAPlayer()
        {
            Assert.Throws<ArgumentNullException>(() => NetUi.CreateHud(null));
            Assert.Throws<ArgumentNullException>(() => NetUi.UseDefaultHudFactory(null));
        }

        private sealed class RecordingFactory : INetHudFactory
        {
            public readonly List<NetPlayer> Players = new List<NetPlayer>();

            public bool ReturnsNothing { get; set; }

            public INetHud Hud { get; } = new FakeHud();

            public INetHud Create(NetPlayer player)
            {
                Players.Add(player);
                return ReturnsNothing ? null : Hud;
            }
        }

        private sealed class FakeHud : INetHud
        {
            public GameObject gameObject => null;

            public void ShowIdleWarning(float remainingSeconds)
            {
            }

            public void ClearIdleWarning()
            {
            }
        }
    }
}
