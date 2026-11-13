using System;
using System.Linq;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CampaignSaveTests
    {
        private const string _rifleId = CampaignSave.LegacyWeaponId;
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private const string _valid =
            "{'version':2,'missionId':'m01','checkpointId':'cp2','health':75,'equippedWeaponId':'h'," +
            "'weapons':[{'id':'r','ammoInMagazine':12,'reserveAmmo':60},{'id':'h','ammoInMagazine':5,'reserveAmmo':24}]," +
            "'objectives':[{'id':'kills','state':'active','count':2},{'id':'exit','state':'locked','count':0}]}";

        private const string _validLegacy =
            "{'version':1,'missionId':'m01','checkpointId':'cp2','health':75,'ammoInMagazine':12,'reserveAmmo':60," +
            "'objectives':[{'id':'kills','state':'active','count':2},{'id':'exit','state':'locked','count':0}]}";

        private static readonly string[] _killsAndExit = { "kills", "exit" };
        private static readonly string[] _killsOnly = { "kills" };
        private static readonly string[] _sampleWeapons = { _rifleId + ":12/60", _handgunId + ":5/24" };

        [Test]
        public void ConstructorKeepsValues()
        {
            var save = Sample();

            Assert.That(save.Version, Is.EqualTo(CampaignSave.CurrentVersion));
            Assert.That(save.MissionId, Is.EqualTo("m01"));
            Assert.That(save.CheckpointId, Is.EqualTo("cp2"));
            Assert.That(save.Health, Is.EqualTo(75));
            Assert.That(save.EquippedWeaponId, Is.EqualTo(_handgunId));
            Assert.That(save.Weapons.Select(w => w.WeaponId + ":" + w.AmmoInMagazine + "/" + w.ReserveAmmo),
                Is.EqualTo(_sampleWeapons));
            Assert.That(save.FindWeapon(_handgunId).AmmoInMagazine, Is.EqualTo(5));
            Assert.That(save.FindWeapon("missing"), Is.Null);
            Assert.That(save.Objectives.Select(o => o.Id), Is.EqualTo(_killsAndExit));
        }

        [Test]
        public void WritesVersionFirst()
        {
            Assert.That(Sample().ToJson(), Does.StartWith("{\"version\":2,"));
        }

        [Test]
        public void RoundTripsThroughJson()
        {
            var save = Sample();

            AssertSame(CampaignSave.FromJson(save.ToJson()), save);
        }

        [Test]
        public void RoundTripsWithoutWeaponsOrEquippedWeapon()
        {
            var save = new CampaignSave("m", "", 1, Array.Empty<WeaponAmmo>(), null, Array.Empty<ObjectiveProgress>());

            Assert.That(save.EquippedWeaponId, Is.Empty);
            AssertSame(CampaignSave.FromJson(save.ToJson()), save);
        }

        [Test]
        public void RoundTripsAwkwardStrings()
        {
            var save = new CampaignSave(
                "m \"quoted\" \\ back/slash",
                "改行\nタブ\t制御\u0001\u001f絵文字😀",
                1,
                new[] { new WeaponAmmo("武器 \"1\"", 0, int.MaxValue) },
                "武器 \"1\"",
                new[] { new ObjectiveProgress("目標 \"1\"", ObjectiveState.Failed, 0) });

            AssertSame(CampaignSave.FromJson(save.ToJson()), save);
        }

        [Test]
        public void ReadsHandWrittenJsonWithWhitespaceAndEscapes()
        {
            var json = "\n{ \"version\" : 2 ,\r\n \"missionId\" : \"m\\u0030\\/1\", \"checkpointId\": \"\",\t" +
                "\"health\": 100, \"equippedWeaponId\": \"\", \"weapons\": [ ], \"objectives\": [ ] }\n";

            var save = CampaignSave.FromJson(json);

            Assert.That(save.MissionId, Is.EqualTo("m0/1"));
            Assert.That(save.CheckpointId, Is.Empty);
            Assert.That(save.EquippedWeaponId, Is.Empty);
            Assert.That(save.Weapons, Is.Empty);
            Assert.That(save.Objectives, Is.Empty);
        }

        [Test]
        public void NullCheckpointMeansNone()
        {
            var save = new CampaignSave("m", null, 1, Array.Empty<WeaponAmmo>(), "", Array.Empty<ObjectiveProgress>());

            Assert.That(save.CheckpointId, Is.Empty);
            Assert.That(CampaignSave.FromJson(save.ToJson()).CheckpointId, Is.Empty);
        }

        [Test]
        public void MissionSurvivesSaveAndLoad()
        {
            var objectives = new[]
            {
                new Objective("kills", "", 3),
                new Objective("exit", "", prerequisites: _killsOnly),
                new Objective("bonus", "", optional: true),
            };
            var mission = new Mission(objectives);
            mission.Progress("kills", 3);
            mission.Fail("bonus");
            var json = new CampaignSave("m01", "cp1", 50, new[] { new WeaponAmmo(_rifleId, 3, 30) }, _rifleId, mission.Snapshot()).ToJson();

            var loaded = new Mission(objectives);
            loaded.Restore(CampaignSave.FromJson(json).Objectives);

            Assert.That(loaded.GetState("kills"), Is.EqualTo(ObjectiveState.Completed));
            Assert.That(loaded.GetCount("kills"), Is.EqualTo(3));
            Assert.That(loaded.GetState("exit"), Is.EqualTo(ObjectiveState.Active));
            Assert.That(loaded.GetState("bonus"), Is.EqualTo(ObjectiveState.Failed));
        }

        [Test]
        public void ReadsTheLegacySingleWeaponFormatAsTheRiflesAmmo()
        {
            var save = CampaignSave.FromJson(Json(_validLegacy));

            Assert.That(save.Version, Is.EqualTo(CampaignSave.CurrentVersion), "a loaded legacy save is upgraded");
            Assert.That(save.MissionId, Is.EqualTo("m01"));
            Assert.That(save.CheckpointId, Is.EqualTo("cp2"));
            Assert.That(save.Health, Is.EqualTo(75));
            Assert.That(save.Weapons, Has.Count.EqualTo(1));
            Assert.That(save.Weapons[0].WeaponId, Is.EqualTo(CampaignSave.LegacyWeaponId));
            Assert.That(save.Weapons[0].AmmoInMagazine, Is.EqualTo(12));
            Assert.That(save.Weapons[0].ReserveAmmo, Is.EqualTo(60));
            Assert.That(save.EquippedWeaponId, Is.EqualTo(CampaignSave.LegacyWeaponId));
            Assert.That(save.Objectives.Select(o => o.Id), Is.EqualTo(_killsAndExit));
        }

        [Test]
        public void ALoadedLegacySaveIsWrittenBackInTheCurrentFormat()
        {
            var json = CampaignSave.FromJson(Json(_validLegacy)).ToJson();

            Assert.That(json, Does.StartWith("{\"version\":2,"));
            Assert.That(json, Does.Contain("\"weapons\":[{\"id\":\"" + CampaignSave.LegacyWeaponId + "\",\"ammoInMagazine\":12,\"reserveAmmo\":60}]"));
            Assert.That(CampaignSave.FromJson(json).FindWeapon(CampaignSave.LegacyWeaponId).ReserveAmmo, Is.EqualTo(60));
        }

        [TestCase("'ammoInMagazine':12,", "")]
        [TestCase("'ammoInMagazine':12", "'ammoInMagazine':-1")]
        [TestCase("'reserveAmmo':60", "'reserveAmmo':'60'")]
        [TestCase("'reserveAmmo':60,", "'reserveAmmo':60,'weapons':[],")]
        [TestCase("'reserveAmmo':60,", "'reserveAmmo':60,'equippedWeaponId':'',")]
        public void RejectsInvalidLegacyFields(string from, string to)
        {
            Assert.That(_validLegacy.Contains(from, StringComparison.Ordinal), Is.True);
            var json = Json(_validLegacy.Replace(from, to, StringComparison.Ordinal));

            Assert.Throws<FormatException>(() => CampaignSave.FromJson(json));
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(-1)]
        public void RejectsOtherVersions(int version)
        {
            var json = Json(_valid.Replace("'version':2", "'version':" + version, StringComparison.Ordinal));

            var e = Assert.Throws<FormatException>(() => CampaignSave.FromJson(json));
            Assert.That(e.Message, Does.Contain("version"));
        }

        [Test]
        public void ChecksVersionBeforeOtherFields()
        {
            var e = Assert.Throws<FormatException>(() => CampaignSave.FromJson(Json("{'version':99,'slots':[]}")));
            Assert.That(e.Message, Does.Contain("version"));
        }

        [Test]
        public void TheCurrentFormatDoesNotAcceptTheLegacyFields()
        {
            var json = Json(_valid.Replace("'health':75,", "'health':75,'ammoInMagazine':1,", StringComparison.Ordinal));

            Assert.Throws<FormatException>(() => CampaignSave.FromJson(json));
        }

        [Test]
        public void ValidSampleParses()
        {
            var save = CampaignSave.FromJson(Json(_valid));

            Assert.That(save.Objectives, Has.Count.EqualTo(2));
            Assert.That(save.Weapons, Has.Count.EqualTo(2));
            Assert.That(save.EquippedWeaponId, Is.EqualTo("h"));
        }

        // JSON として壊れている
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{")]
        [TestCase("{'version':2")]
        [TestCase("{'version':2,}")]
        [TestCase("{version:2}")]
        [TestCase("{'version' 2}")]
        [TestCase("{'version':02}")]
        [TestCase("{'version':2.0}")]
        [TestCase("{'version':2e0}")]
        [TestCase("{'version':-}")]
        [TestCase("{'version':tru}")]
        [TestCase("{'version':2}{}")]
        [TestCase("{'version':2} x")]
        [TestCase("{'version':2,'missionId':'a\\x'}")]
        [TestCase("{'version':2,'missionId':'a\\u12'}")]
        [TestCase("{'version':2,'missionId':'a\\u12zz'}")]
        [TestCase("{'version':2,'missionId':'unterminated}")]
        [TestCase("{'version':2,'missionId':'raw\nnewline'}")]
        [TestCase("{'version':2,'version':2}")]
        [TestCase("{'version':99999999999999999999}")]
        // 形が違う
        [TestCase("[]")]
        [TestCase("null")]
        [TestCase("1")]
        [TestCase("{}")]
        [TestCase("{'version':'2'}")]
        [TestCase("{'version':4294967298}")]
        public void RejectsBrokenData(string text)
        {
            var json = Json(text);

            Assert.Throws<FormatException>(() => CampaignSave.FromJson(json));
        }

        // 項目が欠けている・余分・型違い・範囲外
        [TestCase("'missionId':'m01',", "")]
        [TestCase("'missionId':'m01'", "'missionId':''")]
        [TestCase("'missionId':'m01'", "'missionId':null")]
        [TestCase("'checkpointId':'cp2'", "'checkpointId':2")]
        [TestCase("'health':75", "'health':'75'")]
        [TestCase("'health':75", "'health':0")]
        [TestCase("'health':75", "'health':true")]
        [TestCase("'equippedWeaponId':'h',", "")]
        [TestCase("'equippedWeaponId':'h'", "'equippedWeaponId':null")]
        [TestCase("'equippedWeaponId':'h'", "'equippedWeaponId':'knife'")]
        [TestCase("'weapons':[{'id':'r','ammoInMagazine':12,'reserveAmmo':60},{'id':'h','ammoInMagazine':5,'reserveAmmo':24}],", "")]
        [TestCase("'weapons':[", "'weapons':{},'w':[")]
        [TestCase("[{'id':'r'", "[1,{'id':'r'")]
        [TestCase("{'id':'h','ammoInMagazine':5", "{'id':'r','ammoInMagazine':5")]
        [TestCase("{'id':'r'", "{'id':''")]
        [TestCase("{'id':'r'", "{'id':3")]
        [TestCase("'ammoInMagazine':12", "'ammoInMagazine':-1")]
        [TestCase("'reserveAmmo':60", "'reserveAmmo':2147483648")]
        [TestCase("'reserveAmmo':60", "'reserveAmmo':60,'extra':1")]
        [TestCase("{'id':'h','ammoInMagazine':5,'reserveAmmo':24}", "{'id':'h','ammoInMagazine':5}")]
        [TestCase("'health':75,", "'health':75,'extra':1,")]
        [TestCase("'objectives':[", "'objectives':{},'x':[")]
        [TestCase("[{'id':'kills'", "[1,{'id':'kills'")]
        [TestCase("'state':'active'", "'state':'done'")]
        [TestCase("'state':'active'", "'state':'Active'")]
        [TestCase("'state':'active'", "'state':1")]
        [TestCase("'count':2", "'count':-2")]
        [TestCase("'count':2", "'count':2,'note':''")]
        [TestCase("'id':'exit'", "'id':'kills'")]
        [TestCase("'id':'exit'", "'id':''")]
        [TestCase("{'id':'exit','state':'locked','count':0}", "{'id':'exit','state':'locked'}")]
        public void RejectsInvalidFields(string from, string to)
        {
            Assert.That(_valid.Contains(from, StringComparison.Ordinal), Is.True);
            var json = Json(_valid.Replace(from, to, StringComparison.Ordinal));

            Assert.Throws<FormatException>(() => CampaignSave.FromJson(json));
        }

        [Test]
        public void RejectsDeepNesting()
        {
            var json = "{\"version\":2,\"x\":" + new string('[', 10000) + new string(']', 10000) + "}";

            Assert.Throws<FormatException>(() => CampaignSave.FromJson(json));
        }

        [Test]
        public void RejectsNullInput()
        {
            Assert.Throws<ArgumentNullException>(() => CampaignSave.FromJson(null));
        }

        [Test]
        public void ConstructorRejectsInvalidValues()
        {
            var none = Array.Empty<ObjectiveProgress>();
            var noWeapons = Array.Empty<WeaponAmmo>();
            var rifle = new[] { new WeaponAmmo(_rifleId, 1, 1) };

            Assert.Throws<ArgumentNullException>(() => new CampaignSave(null, "", 1, noWeapons, "", none));
            Assert.Throws<ArgumentException>(() => new CampaignSave("", "", 1, noWeapons, "", none));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CampaignSave("m", "", 0, noWeapons, "", none));
            Assert.Throws<ArgumentNullException>(() => new CampaignSave("m", "", 1, null, "", none));
            Assert.Throws<ArgumentException>(() => new CampaignSave("m", "", 1, new WeaponAmmo[] { null }, "", none));
            Assert.Throws<ArgumentException>(() => new CampaignSave("m", "", 1, new[]
            {
                new WeaponAmmo(_rifleId, 1, 1),
                new WeaponAmmo(_rifleId, 2, 2),
            }, "", none));
            Assert.Throws<ArgumentException>(() => new CampaignSave("m", "", 1, rifle, _handgunId, none), "equipped weapon is not saved");
            Assert.Throws<ArgumentNullException>(() => new CampaignSave("m", "", 1, rifle, _rifleId, null));
            Assert.Throws<ArgumentException>(() => new CampaignSave("m", "", 1, rifle, _rifleId, new ObjectiveProgress[] { null }));
            Assert.Throws<ArgumentException>(() => new CampaignSave("m", "", 1, rifle, _rifleId, new[]
            {
                new ObjectiveProgress("a", ObjectiveState.Active, 0),
                new ObjectiveProgress("a", ObjectiveState.Active, 0),
            }));
        }

        [Test]
        public void WeaponAmmoRejectsInvalidValues()
        {
            Assert.Throws<ArgumentNullException>(() => new WeaponAmmo(null, 0, 0));
            Assert.Throws<ArgumentException>(() => new WeaponAmmo("", 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponAmmo(_rifleId, -1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new WeaponAmmo(_rifleId, 0, -1));
            Assert.Throws<ArgumentNullException>(() => WeaponAmmo.Of(null));
        }

        private static CampaignSave Sample()
        {
            return new CampaignSave(
                "m01",
                "cp2",
                75,
                new[] { new WeaponAmmo(_rifleId, 12, 60), new WeaponAmmo(_handgunId, 5, 24) },
                _handgunId,
                new[]
                {
                    new ObjectiveProgress("kills", ObjectiveState.Active, 2),
                    new ObjectiveProgress("exit", ObjectiveState.Locked, 0),
                });
        }

        /// <summary>読みやすさのため ' で書いた JSON を " に直す。</summary>
        private static string Json(string text)
        {
            return text.Replace('\'', '"');
        }

        private static void AssertSame(CampaignSave actual, CampaignSave expected)
        {
            Assert.That(actual.Version, Is.EqualTo(expected.Version));
            Assert.That(actual.MissionId, Is.EqualTo(expected.MissionId));
            Assert.That(actual.CheckpointId, Is.EqualTo(expected.CheckpointId));
            Assert.That(actual.Health, Is.EqualTo(expected.Health));
            Assert.That(actual.EquippedWeaponId, Is.EqualTo(expected.EquippedWeaponId));
            Assert.That(
                actual.Weapons.Select(w => w.WeaponId + ":" + w.AmmoInMagazine + ":" + w.ReserveAmmo),
                Is.EqualTo(expected.Weapons.Select(w => w.WeaponId + ":" + w.AmmoInMagazine + ":" + w.ReserveAmmo)));
            Assert.That(
                actual.Objectives.Select(o => o.Id + ":" + o.State + ":" + o.Count),
                Is.EqualTo(expected.Objectives.Select(o => o.Id + ":" + o.State + ":" + o.Count)));
        }
    }
}
