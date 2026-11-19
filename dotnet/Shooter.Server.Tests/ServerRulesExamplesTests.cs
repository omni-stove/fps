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
        private static readonly int[] _arenaCheckpoints = { 3 };

        [TestCase("TeamDeathmatch", MatchObjective.None)]
        [TestCase("Domination", MatchObjective.Domination)]
        [TestCase("Elimination", MatchObjective.Elimination)]
        [TestCase("CaptureTheFlag", MatchObjective.CaptureTheFlag)]
        [TestCase("Hardpoint", MatchObjective.Hardpoint)]
        [TestCase("SearchAndDestroy", MatchObjective.SearchAndDestroy)]
        [TestCase("FreeForAll", MatchObjective.FreeForAll)]
        [TestCase("KillConfirmed", MatchObjective.KillConfirmed)]
        [TestCase("GunGame", MatchObjective.GunGame)]
        [TestCase("Payload", MatchObjective.Payload)]
        public void EachExampleRulesFileLoadsAndPicksItsObjective(string name, MatchObjective objective)
        {
            Assert.That(ServerRulesData.TryLoad(name, Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Rules.Objective, Is.EqualTo(objective));
            Assert.That(data.File.Name, Is.EqualTo(name));
            var rules = ServerObjectiveFactory.Create(objective, data.Settings, new NoSessionHost());
            Assert.That(rules.Objective, Is.EqualTo(objective));
        }

        [TestCase("TeamDeathmatch")]
        [TestCase("Domination")]
        [TestCase("Elimination")]
        [TestCase("CaptureTheFlag")]
        [TestCase("Hardpoint")]
        [TestCase("SearchAndDestroy")]
        [TestCase("FreeForAll")]
        [TestCase("KillConfirmed")]
        [TestCase("GunGame")]
        [TestCase("Payload")]
        public void EveryExampleRulesFileTurnsOnMelee(string name)
        {
            Assert.That(ServerRulesData.TryLoad(name, Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Rules.Actions.Melee, Is.EqualTo(Melee.On));
        }

        [TestCase("CaptureTheFlag")]
        [TestCase("Hardpoint")]
        [TestCase("SearchAndDestroy")]
        [TestCase("FreeForAll")]
        [TestCase("KillConfirmed")]
        [TestCase("GunGame")]
        [TestCase("Payload")]
        public void TheOnlineModeExamplesTurnOnTheKillCam(string name)
        {
            Assert.That(ServerRulesData.TryLoad(name, Roots(), out var data, out var error), Is.True, error);

            Assert.That(data.Rules.DeathCamera, Is.EqualTo(DeathCamera.KillCam));
            Assert.That(data.Rules.Streaks, Is.EqualTo(name == "GunGame" ? Streaks.Off : Streaks.On), "a gun game climbs by kills, not by streak rewards");
        }

        [TestCase("CaptureTheFlag")]
        [TestCase("Hardpoint")]
        [TestCase("SearchAndDestroy")]
        [TestCase("FreeForAll")]
        [TestCase("KillConfirmed")]
        [TestCase("GunGame")]
        [TestCase("Payload")]
        public void TheExampleMapCanBePlayedWithTheExampleRules(string name)
        {
            Assert.That(ServerRulesData.TryLoad(name, Roots(), out var data, out var error), Is.True, error);
            Assert.That(ServerMapData.TryLoad(_map, Roots(), out var map, out error), Is.True, error);
            var rules = ServerObjectiveFactory.Create(data.Rules.Objective, data.Settings, new NoSessionHost());

            Assert.DoesNotThrow(() => rules.CheckMap(map, map.Points));
        }

        // 台の位置はシーンを作る ArenaSceneBuilder.FlagBases・NetSceneBuilder.FlagBases と同じ (足元の高さ y = -1、自陣の奥の中央)
        [TestCase("NetArena01", 27f)]
        [TestCase("NetTest", 26f)]
        public void EachExampleMapHasOneFlagBasePerTeamBehindItsSpawns(string mapName, float depth)
        {
            Assert.That(ServerRulesData.TryLoad("CaptureTheFlag", Roots(), out var data, out var error), Is.True, error);
            Assert.That(ServerMapData.TryLoad(mapName, Roots(), out var map, out error), Is.True, error);
            var rules = ServerObjectiveFactory.Create(data.Rules.Objective, data.Settings, new NoSessionHost());

            Assert.DoesNotThrow(() => rules.CheckMap(map, map.Points), "run make export-server-rules and commit server-data/maps");
            Assert.That(map.Points.FlagBases, Has.Count.EqualTo(2));
            Assert.That(map.Points.FlagBases[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(map.Points.FlagBases[0].Position, Is.EqualTo(new System.Numerics.Vector3(0f, -1f, -depth)));
            Assert.That(map.Points.FlagBases[1].Team, Is.EqualTo(Team.Bravo));
            Assert.That(map.Points.FlagBases[1].Position, Is.EqualTo(new System.Numerics.Vector3(0f, -1f, depth)));
        }

        // 道筋は ArenaSceneBuilder.PayloadRoutePoints と同じ (足元の高さ y = -1、Alpha の手前 z = -18 から Bravo の手前 z = 18、中継点は真ん中)。
        // 範囲は台車に ControlPointZone を置かないので、遊ぶときに足される ControlPointZone の既定 (半径 5・高さ 6)
        [Test]
        public void TheArenaExampleMapCarriesThePayloadRoute()
        {
            Assert.That(ServerMapData.TryLoad(_map, Roots(), out var map, out var error), Is.True, error);

            var payload = map.Points.Payload;
            Assert.That(payload, Is.Not.Null, "run make export-server-rules and commit server-data/maps");
            Assert.That(payload.Attackers, Is.EqualTo(Team.Alpha));
            Assert.That(payload.Route.PointCount, Is.EqualTo(7));
            Assert.That(payload.Route.Point(0), Is.EqualTo(new System.Numerics.Vector3(0f, -1f, -18f)));
            Assert.That(payload.Route.Point(3), Is.EqualTo(new System.Numerics.Vector3(4f, -1f, 0f)));
            Assert.That(payload.Route.Point(6), Is.EqualTo(new System.Numerics.Vector3(0f, -1f, 18f)));
            Assert.That(payload.CheckpointPoints, Is.EqualTo(_arenaCheckpoints));
            Assert.That(payload.Route.Length, Is.EqualTo(40d).Within(1e-4));
            Assert.That(payload.Radius, Is.EqualTo(5f));
            Assert.That(payload.Height, Is.EqualTo(6f));
            Assert.That(map.Points.ControlPoints, Has.Count.EqualTo(3), "the cart adds no control point");
        }

        [Test]
        public void AnExampleMapWithoutARouteIsRefusedForThePayloadExample()
        {
            Assert.That(ServerRulesData.TryLoad("Payload", Roots(), out var data, out var error), Is.True, error);
            Assert.That(ServerMapData.TryLoad("NetTest", Roots(), out var map, out error), Is.True, error);
            var rules = ServerObjectiveFactory.Create(data.Rules.Objective, data.Settings, new NoSessionHost());

            var e = Assert.Throws<InvalidOperationException>(() => rules.UseMap(map, map.Points));

            Assert.That(e.Message, Does.StartWith("cannot start the Payload match on map NetTest: it has no payload route"));
        }

        // BR は目的の工場にまだつないでいないので、ルールの例と調整値から直に作る。例のマップはエリアを書き出していないので断る
        [Test]
        public void TheBattleRoyaleExampleReadsItsRulesAndRefusesAMapWithoutAZone()
        {
            Assert.That(ServerRulesData.TryLoad("BattleRoyale", Roots(), out var data, out var error), Is.True, error);
            Assert.That(ServerMapData.TryLoad(_map, Roots(), out var map, out error), Is.True, error);

            Assert.That(data.Rules.Objective, Is.EqualTo(MatchObjective.BattleRoyale));
            Assert.That(data.File.Name, Is.EqualTo("BattleRoyale"));
            Assert.That(data.Rules.Actions.Downed, Is.EqualTo(Downed.Revive));
            Assert.That(data.Rules.Actions.Parachute, Is.EqualTo(Parachute.On));
            Assert.That(data.Rules.Actions.Melee, Is.EqualTo(Melee.On));
            Assert.That(data.BattleRoyalePath, Does.EndWith(ServerRulesData.BattleRoyaleTuningFileName), "the committed tuning sits beside the rules");
            var rules = new BattleRoyaleRules(new NoSessionHost(), data.BattleRoyale, new BattleRoyaleTestWorld(), 1);
            Assert.That(rules.Objective, Is.EqualTo(MatchObjective.BattleRoyale));

            var e = Assert.Throws<InvalidOperationException>(() => rules.CheckMap(map, map.Points));

            Assert.That(e.Message, Does.StartWith("cannot change to map " + _map + " for the BattleRoyale match: it has no zone"));
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
