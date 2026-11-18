using System;
using System.IO;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // server-data/rules の例を、make server-dotnet が読むのと同じ口 (ServerRulesData.TryLoad) で 1 つずつ読み、
    // 専用サーバーがその目的を作れて、例のマップで遊べるかを確かめる
    public sealed class ServerRulesExamplesTests
    {
        private const string _map = "NetArena01";

        [TestCase("TeamDeathmatch", MatchObjective.None)]
        [TestCase("Domination", MatchObjective.Domination)]
        [TestCase("Elimination", MatchObjective.Elimination)]
        [TestCase("CaptureTheFlag", MatchObjective.CaptureTheFlag)]
        [TestCase("Hardpoint", MatchObjective.Hardpoint)]
        [TestCase("SearchAndDestroy", MatchObjective.SearchAndDestroy)]
        [TestCase("FreeForAll", MatchObjective.FreeForAll)]
        [TestCase("KillConfirmed", MatchObjective.KillConfirmed)]
        [TestCase("GunGame", MatchObjective.GunGame)]
        public void EachExampleRulesFileLoadsAndPicksItsObjective(string name, MatchObjective objective)
        {
            Assert.That(ServerRulesData.TryLoad(name, Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Rules.Objective, Is.EqualTo(objective));
            Assert.That(data.File.Name, Is.EqualTo(name));
            var rules = ServerObjectiveFactory.Create(objective, data.Settings, new NoSessionHost());
            Assert.That(rules.Objective, Is.EqualTo(objective));
        }

        [TestCase("CaptureTheFlag")]
        [TestCase("Hardpoint")]
        [TestCase("SearchAndDestroy")]
        [TestCase("FreeForAll")]
        [TestCase("KillConfirmed")]
        [TestCase("GunGame")]
        public void TheOnlineModeExamplesTurnOnMeleeAndTheKillCam(string name)
        {
            Assert.That(ServerRulesData.TryLoad(name, Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Rules.Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(data.Rules.DeathCamera, Is.EqualTo(DeathCamera.KillCam));
            Assert.That(data.Rules.Streaks, Is.EqualTo(name == "GunGame" ? Streaks.Off : Streaks.On), "a gun game climbs by kills, not by streak rewards");
        }

        [TestCase("Hardpoint")]
        [TestCase("SearchAndDestroy")]
        [TestCase("FreeForAll")]
        [TestCase("KillConfirmed")]
        [TestCase("GunGame")]
        public void TheExampleMapCanBePlayedWithTheExampleRules(string name)
        {
            Assert.That(ServerRulesData.TryLoad(name, Roots(), out var data, out var error), Is.True, error);
            Assert.That(ServerMapData.TryLoad(_map, Roots(), out var map, out error), Is.True, error);
            var rules = ServerObjectiveFactory.Create(data.Rules.Objective, data.Settings, new NoSessionHost());

            Assert.DoesNotThrow(() => rules.CheckMap(map, map.Points));
        }

        // 例のマップには旗の台が無いので、旗取りは台を置いて書き出したマップでしか始められない
        [Test]
        public void CaptureTheFlagNeedsAMapWithFlagBases()
        {
            Assert.That(ServerRulesData.TryLoad("CaptureTheFlag", Roots(), out var data, out var error), Is.True, error);
            Assert.That(ServerMapData.TryLoad(_map, Roots(), out var map, out error), Is.True, error);
            var rules = ServerObjectiveFactory.Create(data.Rules.Objective, data.Settings, new NoSessionHost());

            var e = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(map, map.Points));

            Assert.That(e.Message, Does.StartWith("cannot change to map NetArena01 for the CaptureTheFlag match: it has no flag base for team "));
        }

        [Test]
        public void TheGunGameExampleClimbsTheLoadoutWeapons()
        {
            Assert.That(ServerRulesData.TryLoad("GunGame", Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Settings.GunGame.TierWeaponIds, Is.EqualTo(new[] { TestRules.RifleId, TestRules.HandgunId }));
            foreach (var id in data.Settings.GunGame.TierWeaponIds)
            {
                Assert.That(data.File.CreateLoadout().Contains(id), Is.True, id);
            }
        }

        private static string[] Roots()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, ServerMapData.DataDirectoryName, ServerRulesData.RulesDirectoryName)))
                {
                    return new[] { directory.FullName };
                }
            }

            Assert.Fail(ServerMapData.DataDirectoryName + " is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }

        // 目的を作るだけで試合は始めないので、Session と体は持たない
        private sealed class NoSessionHost : IMatchObjectiveHost
        {
            public TeamDeathmatchSession Session => null;

            public ServerBodies Bodies => null;

            public TextWriter Log { get; } = TextWriter.Null;

            public void SetMatchScores(int alpha, int bravo)
            {
            }

            public void ClearDowned()
            {
            }
        }
    }
}
