using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class MatchLogTests
    {
        [Test]
        public void FormatsScore()
        {
            Assert.That(MatchLog.Score(3, 1), Is.EqualTo("[Match] score A=3 B=1"));
        }

        [Test]
        public void FormatsEndedWithWinner()
        {
            Assert.That(MatchLog.Ended(Team.Alpha), Is.EqualTo("[Match] ended winner=Alpha"));
            Assert.That(MatchLog.Ended(Team.Bravo), Is.EqualTo("[Match] ended winner=Bravo"));
        }

        [Test]
        public void FormatsEndedInDraw()
        {
            Assert.That(MatchLog.Ended(null), Is.EqualTo("[Match] ended winner=draw"));
        }

        [Test]
        public void FormatsJoinKillAndSpawn()
        {
            Assert.That(MatchLog.Joined(2, Team.Bravo), Is.EqualTo("[Match] joined player=2 team=Bravo"));
            Assert.That(MatchLog.Kill(1, 2), Is.EqualTo("[Match] kill killer=1 victim=2"));
            Assert.That(MatchLog.Spawn(2, new SpawnPoint(new Vector3(1f, 0f, -20.5f))),
                Is.EqualTo("[Match] spawn player=2 at=(1.00, 0.00, -20.50)"));
        }

        [Test]
        public void FormatsSoloStateWhileWaitingAndPlaying()
        {
            var match = new Match(60f, 3, 0);
            Assert.That(MatchLog.SoloState(match), Is.EqualTo("[Match] state=Waiting kills=0 remaining=60.0"));

            match.Start();
            match.Tick(12.34f);
            match.ReportKill(0, 1);
            Assert.That(MatchLog.SoloState(match), Is.EqualTo("[Match] state=Playing kills=1 remaining=47.7"));
        }

        [Test]
        public void FormatsSoloStateWithTheResultOnceWon()
        {
            var match = new Match(30f, 2, 0);
            match.Start();
            match.Tick(10f);
            match.ReportKill(0, 1);
            match.ReportKill(0, 2);

            Assert.That(MatchLog.SoloState(match), Is.EqualTo("[Match] state=Won kills=2 remaining=20.0"));
        }

        [Test]
        public void FormatsSoloStateWithTheResultOnceLost()
        {
            var timedOut = new Match(5f, 2, 0);
            timedOut.Start();
            timedOut.ReportKill(0, 1);
            timedOut.Tick(6f);
            Assert.That(MatchLog.SoloState(timedOut), Is.EqualTo("[Match] state=Lost kills=1 remaining=0.0"));

            var died = new Match(5f, 2, 0);
            died.Start();
            died.Tick(1f);
            died.ReportDeath(0);
            Assert.That(MatchLog.SoloState(died), Is.EqualTo("[Match] state=Lost kills=0 remaining=4.0"));
        }

        [Test]
        public void FormatsRestart()
        {
            Assert.That(MatchLog.Restart(), Is.EqualTo("[Match] restart"));
        }

        [Test]
        public void FormatsNextMatch()
        {
            Assert.That(MatchLog.NextMatch(10f), Is.EqualTo("[Match] next match in=10.0s"));
            Assert.That(MatchLog.NextMatch(0f), Is.EqualTo("[Match] next match in=0.0s"));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchLog.NextMatch(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchLog.NextMatch(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchLog.NextMatch(float.PositiveInfinity));
        }

        [Test]
        public void FormatsTheLoadoutAndEachWeapon()
        {
            var rifle = new WeaponSpec(
                "00000000-0000-4000-8000-000000000001", "Rifle", WeaponCategory.AssaultRifle, FireMode.Auto,
                30, 90, 0.1f, 2f, 25, 100f, new DamageFalloff(20f, 60f, 0.5f));
            var handgun = new WeaponSpec(
                "00000000-0000-4000-8000-000000000002", "Handgun", WeaponCategory.Handgun, FireMode.Semi,
                12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None);
            var rule = new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle }, new[] { WeaponCategory.Handgun } });
            var loadout = new ServerLoadout(rule, new[] { rifle, handgun }, 0.4f);

            Assert.That(MatchLog.Loadout("DefaultLoadout", loadout), Is.EqualTo("[Match] loadout name=DefaultLoadout slots=2 switch=0.40s"));
            Assert.That(MatchLog.Loadout(null, loadout), Is.EqualTo("[Match] loadout name=- slots=2 switch=0.40s"));
            Assert.That(MatchLog.Weapon(0, rifle), Is.EqualTo(
                "[Match] weapon slot=0 name=Rifle id=00000000-0000-4000-8000-000000000001 category=AssaultRifle mode=Auto"
                + " damage=25 range=100.0m falloff=20.0-60.0m*0.50 fireInterval=0.10s magazine=30 reserve=90 reload=2.00s"));
            Assert.That(MatchLog.Weapon(1, handgun), Is.EqualTo(
                "[Match] weapon slot=1 name=Handgun id=00000000-0000-4000-8000-000000000002 category=Handgun mode=Semi"
                + " damage=34 range=60.0m falloff=none fireInterval=0.20s magazine=12 reserve=48 reload=1.50s"));
        }

        [Test]
        public void RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => MatchLog.Ended((Team)99));
            Assert.Throws<ArgumentNullException>(() => MatchLog.Spawn(1, null));
            Assert.Throws<ArgumentNullException>(() => MatchLog.SoloState(null));
            Assert.Throws<ArgumentNullException>(() => MatchLog.Loadout("DefaultLoadout", null));
            Assert.Throws<ArgumentNullException>(() => MatchLog.Weapon(0, null));
        }
    }
}
