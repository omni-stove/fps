using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Core.Navigation;

namespace Shooter.Server.Tests
{
    public sealed class ServerBotNavigationTests
    {
        private static readonly Vector3 _below = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _above = new Vector3(0f, BotTestMaps.PlatformY + 1f, 15f);

        [Test]
        public void ThePathAcrossTheLadderStartsTheLadderLinkAtTheBottom()
        {
            var map = BotTestMaps.Ladder(null);
            var navigation = Navigation(map, _ => false);
            var path = new NavPath();

            Assert.That(navigation.UnmatchedLadderLinks, Is.Zero);
            Assert.That(navigation.TryPath(_below, _above, path), Is.True);
            var link = path.Points.Single(point => point.StartsLink);
            Assert.That(navigation.LadderOf(link.LinkId), Is.EqualTo(0));
            Assert.That(link.Position.Y, Is.EqualTo(BotTestMaps.FloorY).Within(0.01f));
            Assert.That(path.Points[path.Points.Count - 1].Position.Y, Is.EqualTo(BotTestMaps.PlatformY).Within(0.01f));
        }

        [Test]
        public void WithoutTheLadderRuleThePathAcrossTheLadderIsRefused()
        {
            var map = BotTestMaps.Ladder(null);
            var navigation = Navigation(map, _ => false, laddersAllowed: false);

            Assert.That(navigation.TryPath(_below, _above, new NavPath()), Is.False);
            Assert.That(navigation.TryPath(_below, new Vector3(5f, 1f, -5f), new NavPath()), Is.True);
        }

        [Test]
        public void ALadderLinkFarFromEveryLadderIsCountedAndWalked()
        {
            var map = BotTestMaps.Ladder(null);
            var links = new[] { new NavLink(new Vector3(8f, 0f, 9f), new Vector3(8f, 3f, 11f), true, 0, NavLinkKind.Ladder) };
            var navigation = new ServerBotNavigation(
                map.Navigation, links, map.Ladders.Ladders, map.World, _ => false, NetBotSpec.DefaultNavSampleRadius, 1f);

            Assert.That(navigation.UnmatchedLadderLinks, Is.EqualTo(1));
            Assert.That(navigation.LadderOf(0), Is.EqualTo(ServerBotNavigation.NoLadder));
        }

        [Test]
        public void AnIntactBreakablePieceBlocksThePathAndABrokenOneDoesNot()
        {
            const int wallId = 50;
            var wall = WorldCollider.Box(wallId, 0, MapColliderGroups.Piece(0, 0), new Vector3(0f, 2f, 0f), new Vector3(30f, 2f, 0.25f), Quaternion.Identity);
            var map = BotTestMaps.Flat(null, null, new[] { wall });
            var navigation = Navigation(map, id => id == wallId);
            var from = new Vector3(0f, 1f, -10f);
            var to = new Vector3(0f, 1f, 10f);

            Assert.That(navigation.TryPath(from, to, new NavPath()), Is.False);
            Assert.That(navigation.TryPath(from, new Vector3(10f, 1f, -10f), new NavPath()), Is.True);

            map.Collision.SetGroupEnabled(MapColliderGroups.Piece(0, 0), false);

            Assert.That(navigation.TryPath(from, to, new NavPath()), Is.True);
        }

        [Test]
        public void AWallThatIsNotABreakablePieceIsLeftToTheNavMesh()
        {
            var wall = WorldCollider.Box(50, 0, WorldCollider.StaticGroup, new Vector3(0f, 2f, 0f), new Vector3(30f, 2f, 0.25f), Quaternion.Identity);
            var map = BotTestMaps.Flat(null, null, new[] { wall });
            var navigation = Navigation(map, _ => false);

            Assert.That(navigation.TryPath(new Vector3(0f, 1f, -10f), new Vector3(0f, 1f, 10f), new NavPath()), Is.True);
        }

        [Test]
        public void APositionOffTheNavMeshHasNoPath()
        {
            var navigation = Navigation(BotTestMaps.Flat(null), _ => false);

            Assert.That(navigation.TryPath(new Vector3(100f, 1f, 0f), Vector3.Zero, new NavPath()), Is.False);
            Assert.That(navigation.CanSample(new Vector3(100f, 1f, 0f)), Is.False);
            Assert.That(navigation.CanSample(new Vector3(0f, 1f, 0f)), Is.True);
        }

        private static ServerBotNavigation Navigation(ServerMapData map, Func<int, bool> isPiece, bool laddersAllowed = true)
        {
            return new ServerBotNavigation(
                map.Navigation, map.NavMesh.Links, map.Ladders.Ladders, map.World, isPiece, NetBotSpec.DefaultNavSampleRadius, 1f, laddersAllowed);
        }
    }
}
