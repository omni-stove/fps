using System.Collections.Generic;
using System.IO;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class NetMatchFileTests
    {
        private const string _rifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private const string _handgunJson = "{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
            + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}";

        private static WeaponSpec Rifle() => new WeaponSpec(
            _rifleId, "weapon.rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f,
            new DamageFalloff(20f, 60f, 0.5f), adsFieldOfView: 45f, recoilPitchDegrees: 0.5f);

        private static WeaponSpec Handgun() => new WeaponSpec(
            _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None);

        private static NetMatchFile File(GameRules rules = null, WeaponCatalog catalog = null)
        {
            return new NetMatchFile(
                "Domination",
                rules ?? GameRules.Default.WithObjective(MatchObjective.Domination),
                new NetMatchSettings(scoreLimit: 12, timeLimitSeconds: 240f, captureSeconds: 6.5f, maxCapturers: 2),
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { Rifle(), Handgun() },
                0.4f,
                catalog == null ? null : "WeaponCatalog",
                catalog);
        }

        [Test]
        public void WrittenFileReadsBackTheSameValues()
        {
            var original = File(catalog: new WeaponCatalog(new[] { Rifle(), Handgun() }));

            var text = NetMatchFile.Write(original);
            var read = NetMatchFile.Parse(text, "Domination.json");

            Assert.That(read.Name, Is.EqualTo("Domination"));
            Assert.That(read.Rules.Objective, Is.EqualTo(MatchObjective.Domination));
            Assert.That(GameRulesJson.Write(read.Rules), Is.EqualTo(GameRulesJson.Write(original.Rules)));
            Assert.That(read.Settings.ScoreLimit, Is.EqualTo(12));
            Assert.That(read.Settings.TimeLimitSeconds, Is.EqualTo(240f));
            Assert.That(read.Settings.CaptureSeconds, Is.EqualTo(6.5f));
            Assert.That(read.Settings.MaxCapturers, Is.EqualTo(2));
            Assert.That(read.Settings.RestartDelaySeconds, Is.EqualTo(NetMatchSettings.DefaultRestartDelaySeconds));
            Assert.That(read.LoadoutName, Is.EqualTo("DefaultLoadout"));
            Assert.That(read.SwitchSeconds, Is.EqualTo(0.4f));
            Assert.That(read.LoadoutRule.SlotCount, Is.EqualTo(2));
            Assert.That(read.LoadoutWeapons[0].Id, Is.EqualTo(_rifleId));
            Assert.That(read.LoadoutWeapons[0].Falloff.StartDistance, Is.EqualTo(20f));
            Assert.That(read.LoadoutWeapons[0].AdsFieldOfView, Is.EqualTo(45f));
            Assert.That(read.LoadoutWeapons[0].RecoilPitchDegrees, Is.EqualTo(0.5f));
            Assert.That(read.LoadoutWeapons[1].Falloff.StartDistance, Is.EqualTo(DamageFalloff.None.StartDistance));
            Assert.That(read.CatalogName, Is.EqualTo("WeaponCatalog"));
            Assert.That(read.Catalog.Count, Is.EqualTo(2));
            Assert.That(NetMatchFile.Write(read), Is.EqualTo(text));
        }

        [Test]
        public void MotorValuesReadBackAndDefaultWhenLeftOut()
        {
            var motor = new NetMotorSettings(
                new MotorSpec(6f, 9f, 1.5f, 18f, 0.2f, 3f, dodgeDistance: 4f),
                new StanceGeometry(1.8f, 0.9f, 1.6f, 1.1f, 0.4f),
                new CapsuleMoverSpec(50f, 0.35f, 0.05f, CapsuleMoverSpec.DefaultLayerMask));
            var original = new NetMatchFile(
                "TeamDeathmatch", GameRules.Default, NetMatchSettings.Default, "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } }),
                new[] { Rifle(), Handgun() }, 0.4f, null, null, motor);

            var read = NetMatchFile.Parse(NetMatchFile.Write(original), "motor.json").Motor;

            Assert.That(read.Motor.WalkSpeed, Is.EqualTo(6f));
            Assert.That(read.Motor.SprintSpeed, Is.EqualTo(9f));
            Assert.That(read.Motor.DodgeDistance, Is.EqualTo(4f));
            Assert.That(read.Stance.Standing.Height, Is.EqualTo(1.8f));
            Assert.That(read.Stance.Standing.CenterY, Is.EqualTo(0.9f));
            Assert.That(read.Stance.Standing.EyeY, Is.EqualTo(1.6f));
            Assert.That(read.Stance.Crouched.Height, Is.EqualTo(1.1f));
            Assert.That(read.Stance.Radius, Is.EqualTo(0.4f));
            Assert.That(read.Mover.SlopeLimitDegrees, Is.EqualTo(50f));
            Assert.That(read.Mover.StepOffset, Is.EqualTo(0.35f));
            Assert.That(read.Mover.SkinWidth, Is.EqualTo(0.05f));

            var bare = NetMatchFile.Parse(
                "{\"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]}, \"weapons\": [" + _handgunJson + "]}", "bare.json");
            Assert.That(bare.Motor, Is.SameAs(NetMotorSettings.Default));
        }

        [TestCase("{\"motor\": {\"radius\": -1}}", "motor.radius")]
        [TestCase("{\"motor\": {\"walkSpeed\": -1}}", "motor.walkSpeed")]
        [TestCase("{\"motor\": {\"crouchHeight\": 9}}", "motor.crouchHeight")]
        [TestCase("{\"motor\": {\"slopeLimitDegrees\": 120}}", "motor.slopeLimitDegrees")]
        public void BadMotorValuesNameTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void WithoutACatalogPlayersCannotChoose()
        {
            var read = NetMatchFile.Parse(NetMatchFile.Write(File()), "TeamDeathmatch.json");

            Assert.That(read.Catalog, Is.Null);
            Assert.That(read.CatalogName, Is.Null);
            Assert.That(read.CreateLoadout().TryChoose(1, new[] { _handgunId, _rifleId }, out _), Is.False);
        }

        [Test]
        public void EachLoadoutIsFresh()
        {
            var file = File(catalog: new WeaponCatalog(new[] { Rifle(), Handgun() }));
            var first = file.CreateLoadout();
            Assert.That(first.TryChoose(1, new[] { _rifleId, _handgunId }, out _), Is.True);

            Assert.That(file.CreateLoadout().HasChoice(1), Is.False);
        }

        [Test]
        public void SampleWeaponJsonCanBePastedIntoTheWeapons()
        {
            var text = "{\"rules\": {\"objective\": \"None\"}, \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";

            var read = NetMatchFile.Parse(text, "min.json");

            Assert.That(read.Settings.ScoreLimit, Is.EqualTo(NetMatchSettings.DefaultScoreLimit));
            Assert.That(read.LoadoutWeapons[0].Damage, Is.EqualTo(34));
            Assert.That(read.SwitchSeconds, Is.Zero);
        }

        [TestCase("{\"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"x\"]}}", "loadout.weapons[0]")]
        [TestCase("{\"match\": {\"scoreLimit\": 0}, \"loadout\": {}}", "match.scoreLimit")]
        [TestCase("{\"match\": {\"scoreLimits\": 1}}", "match.scoreLimits")]
        [TestCase("{\"rules\": {\"actions\": {\"grenadeCount\": -1}}}", "rules.actions.grenadeCount")]
        [TestCase("{\"name\": \"x\"}", "loadout")]
        public void ProblemsNameTheItem(string text, string item)
        {
            var e = Assert.Catch(() => NetMatchFile.Parse(text, "bad.json"));

            var named = e is ServerDataJsonException data ? data.Item : ((GameRulesJsonException)e).Item;
            Assert.That(named, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("bad.json"));
        }

        [Test]
        public void WeaponValuesAreCheckedWithTheirItemName()
        {
            var text = "{\"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 0,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";

            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo("weapons[0].magazineSize"));
        }

        [Test]
        public void LoadoutThatBreaksItsRuleIsRefused()
        {
            var text = NetMatchFile.Write(File()).Replace("\"AssaultRifle\"]", "\"Shotgun\"]");

            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(text, "bad.json"));

            Assert.That(e.Item, Is.EqualTo("loadout"));
        }

        [Test]
        public void MapPointsReadBackInTheSameOrder()
        {
            var points = new NetMapPoints(
                new List<SpawnPoint>
                {
                    new SpawnPoint(new Vector3(1f, 0.5f, -2.25f), Team.Alpha),
                    new SpawnPoint(new Vector3(-1f, 0.5f, 2f), Team.Bravo),
                    new SpawnPoint(new Vector3(0f, 1f, 0f)),
                },
                new List<ControlPointArea> { new ControlPointArea(1, new Vector3(0f, 0f, 15f), 4f, 3f), new ControlPointArea(0, Vector3.Zero, 4f, 3f) });

            var text = NetMapPoints.Write(points);
            var read = NetMapPoints.Parse(text, "NetArena01.points.json");

            Assert.That(read.SpawnPoints, Has.Count.EqualTo(3));
            Assert.That(read.SpawnPoints[0].Position, Is.EqualTo(new Vector3(1f, 0.5f, -2.25f)));
            Assert.That(read.SpawnPoints[0].Team, Is.EqualTo(Team.Alpha));
            Assert.That(read.SpawnPoints[2].Team, Is.Null);
            Assert.That(read.ControlPoints[0].Index, Is.EqualTo(1));
            Assert.That(read.ControlPoints[0].Center, Is.EqualTo(new Vector3(0f, 0f, 15f)));
            Assert.That(read.ControlPoints[1].Radius, Is.EqualTo(4f));
            Assert.That(NetMapPoints.Write(read), Is.EqualTo(text));
        }

        // 書き出したファイルを手で直したり書式を変えたりしたまま、サーバーが起動できなくなるのを防ぐ
        [Test]
        public void CommittedServerDataReadsAndIsWrittenTheSameWay()
        {
            var root = ServerDataRoot();
            var rules = Directory.GetFiles(Path.Combine(root, "rules"), "*" + NetMatchFile.Extension);
            var points = Directory.GetFiles(Path.Combine(root, "maps"), "*" + NetMapPoints.Extension);
            Assert.That(rules, Is.Not.Empty);
            Assert.That(points, Is.Not.Empty);

            foreach (var path in rules)
            {
                var text = System.IO.File.ReadAllText(path);
                Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, path)), Is.EqualTo(text), path);
            }

            foreach (var path in points)
            {
                var text = System.IO.File.ReadAllText(path);
                Assert.That(NetMapPoints.Write(NetMapPoints.Parse(text, path)), Is.EqualTo(text), path);
            }

            Assert.That(NetMatchFile.Parse(System.IO.File.ReadAllText(Path.Combine(root, "rules", "Domination.json")), "Domination").Rules.Objective,
                Is.EqualTo(MatchObjective.Domination));
        }

        [TestCase("{\"spawnPoints\": [{\"position\": [0, 1]}]}", "spawnPoints[0].position")]
        [TestCase("{\"spawnPoints\": [{\"position\": [0, 1, 2], \"team\": \"Charlie\"}]}", "spawnPoints[0].team")]
        [TestCase("{\"controlPoints\": [{\"index\": 0, \"center\": [0, 0, 0], \"radius\": 0, \"height\": 3}]}", "controlPoints[0].radius")]
        [TestCase("{\"controlPoints\": [{\"index\": 0, \"center\": [0, 0, 0], \"radius\": 1}]}", "controlPoints[0].height")]
        public void BadMapPointsNameTheItem(string text, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMapPoints.Parse(text, "bad.points.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        private static string ServerDataRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "server-data");
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            Assert.Fail("server-data is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}
