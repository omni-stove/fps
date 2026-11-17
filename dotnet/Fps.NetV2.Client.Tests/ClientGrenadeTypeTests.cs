using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;
using Fps.Protocol;
using NUnit.Framework;

namespace Fps.NetV2.Tests
{
    // 見た目の球を投げた種類の大きさにできるよう、クライアントがグレネードごとに種類の Id を覚えることを確かめる
    public sealed class ClientGrenadeTypeTests
    {
        private const string _smoke = "ecaf6e57-cb77-4707-9321-6ea5b659408f";

        [Test]
        public void TheWorldRemembersTheTypeOfEachGrenade()
        {
            var world = new ClientWorld("Test", new WorldMap(Array.Empty<WorldCollider>()), NetMapLadders.None);
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            snapshot.Tick = 10;
            ref var grenade = ref snapshot.Grenades.Add();
            grenade.GrenadeId = 5;
            grenade.TypeId = _smoke;
            grenade.Position = Vector3.Zero;
            grenade.Rotation = Quaternion.Identity;

            world.ApplyGrenades(snapshot, new List<int>());

            Assert.That(world.GrenadeTypeId(5), Is.EqualTo(_smoke));
            Assert.That(world.GrenadeTypeId(6), Is.Empty, "an unknown grenade has no type");

            world.ClearGrenades();
            Assert.That(world.GrenadeTypeId(5), Is.Empty);
        }
    }
}
