using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Net.Tests
{
    /// <summary>
    /// <see cref="NetCombatFeed"/> の宛先の選び方 (撃った本人以外・本人だけ・全員、専用サーバー自身は入れない) と、
    /// <see cref="NetTeamDeathmatch.KillFeedEntryFor"/> がキルフィードに載せる武器と頭を確かめる。
    /// </summary>
    public sealed class NetCombatFeedTests
    {
        private const ulong _server = 0;
        private const ulong _clientA = 1;
        private const ulong _clientB = 2;
        private const ulong _clientC = 3;

        private readonly List<ulong> _recipients = new List<ulong>();

        [Test]
        public void ImpactZoneRoundTripsThroughTheWire()
        {
            foreach (HitZone zone in System.Enum.GetValues(typeof(HitZone)))
            {
                var impact = NetCombatFeed.ReadImpact(
                    Vector3.zero, Vector3.up, null, "flesh", true, NetCombatFeed.ToWireZone(zone), out var problem);

                Assert.That(problem, Is.Null);
                Assert.That(impact.IsCharacter, Is.True);
                Assert.That(impact.Zone, Is.EqualTo(zone));
                Assert.That(impact.MaterialId, Is.EqualTo("flesh"));
            }

            Assert.That(NetCombatFeed.ToWireZone(null), Is.EqualTo(NetCombatFeed.NoZone));
        }

        [Test]
        public void WallImpactFromTheWireHasNoZoneAndKeepsTheMaterial()
        {
            var impact = NetCombatFeed.ReadImpact(Vector3.zero, Vector3.up, null, "metal", false, NetCombatFeed.NoZone, out var problem);

            Assert.That(problem, Is.Null);
            Assert.That(impact.IsCharacter, Is.False);
            Assert.That(impact.Zone, Is.Null);
            Assert.That(impact.MaterialId, Is.EqualTo("metal"));
        }

        [Test]
        public void EmptyMaterialFromTheWireIsTheDefault()
        {
            var impact = NetCombatFeed.ReadImpact(Vector3.zero, Vector3.up, null, string.Empty, false, NetCombatFeed.NoZone, out _);

            Assert.That(impact.MaterialId, Is.EqualTo(Fps.Runtime.SurfaceMaterial.DefaultId));
        }

        [Test]
        public void BadZonesFromTheWireAreDroppedButTheImpactIsKept()
        {
            var unknown = NetCombatFeed.ReadImpact(Vector3.zero, Vector3.up, null, "flesh", true, 200, out var unknownProblem);
            Assert.That(unknownProblem, Does.Contain("unknown hit zone"));
            Assert.That(unknown.IsCharacter, Is.True);
            Assert.That(unknown.Zone, Is.Null);

            var onWall = NetCombatFeed.ReadImpact(
                Vector3.zero, Vector3.up, null, "metal", false, NetCombatFeed.ToWireZone(HitZone.Head), out var wallProblem);
            Assert.That(wallProblem, Does.Contain("non-character"));
            Assert.That(onWall.IsCharacter, Is.False);
            Assert.That(onWall.Zone, Is.Null);
        }

        [Test]
        public void ShotGoesToEveryoneButTheShooterOnADedicatedServer()
        {
            NetCombatFeed.CollectRecipients(new[] { _clientA, _clientB, _clientC }, _server, serverIsClient: false, _clientA, _recipients);

            Assert.That(_recipients, Is.EqualTo(new[] { _clientB, _clientC }));
        }

        [Test]
        public void ShotFromAClientReachesTheHost()
        {
            NetCombatFeed.CollectRecipients(new[] { _server, _clientA, _clientB }, _server, serverIsClient: true, _clientA, _recipients);

            Assert.That(_recipients, Is.EqualTo(new[] { _server, _clientB }));
        }

        [Test]
        public void ShotFromTheHostSkipsTheHost()
        {
            NetCombatFeed.CollectRecipients(new[] { _server, _clientA, _clientB }, _server, serverIsClient: true, _server, _recipients);

            Assert.That(_recipients, Is.EqualTo(new[] { _clientA, _clientB }));
        }

        [Test]
        public void DedicatedServerIsNeverARecipientEvenIfListed()
        {
            NetCombatFeed.CollectRecipients(new[] { _server, _clientA, _clientB }, _server, serverIsClient: false, null, _recipients);

            Assert.That(_recipients, Is.EqualTo(new[] { _clientA, _clientB }));
        }

        [Test]
        public void ShotWithOnlyTheShooterConnectedHasNoRecipient()
        {
            NetCombatFeed.CollectRecipients(new[] { _clientA }, _server, serverIsClient: false, _clientA, _recipients);

            Assert.That(_recipients, Is.Empty);
        }

        [Test]
        public void CollectingAgainReplacesThePreviousRecipientsAndSkipsDuplicates()
        {
            _recipients.Add(99);

            NetCombatFeed.CollectRecipients(new[] { _clientA, _clientB, _clientA }, _server, serverIsClient: false, null, _recipients);

            Assert.That(_recipients, Is.EqualTo(new[] { _clientA, _clientB }));
        }

        [Test]
        public void HitConfirmAndDamageGoOnlyToAConnectedClient()
        {
            var connected = new[] { _clientA, _clientB };

            Assert.That(NetCombatFeed.CanReceive(_clientA, connected, _server, serverIsClient: false), Is.True);
            Assert.That(NetCombatFeed.CanReceive(_clientB, connected, _server, serverIsClient: false), Is.True);
            Assert.That(NetCombatFeed.CanReceive(_clientC, connected, _server, serverIsClient: false), Is.False, "disconnected");
        }

        [Test]
        public void HostCanReceiveItsOwnHitConfirmButADedicatedServerCannot()
        {
            var connected = new[] { _server, _clientA };

            Assert.That(NetCombatFeed.CanReceive(_server, connected, _server, serverIsClient: true), Is.True);
            Assert.That(NetCombatFeed.CanReceive(_server, connected, _server, serverIsClient: false), Is.False);
        }

        [Test]
        public void KillByTheShotCarriesTheWeaponAndHeadshot()
        {
            var shot = new NetTeamDeathmatch.KillShot(shooterId: 1, targetId: 2, "rifle", HitZone.Head);

            var entry = NetTeamDeathmatch.KillFeedEntryFor(1, 2, friendly: false, shot);

            Assert.That(entry, Is.EqualTo(new KillFeedEntry(1, 2, "rifle", headshot: true, friendly: false)));
        }

        [Test]
        public void BodyShotKillIsNotAHeadshot()
        {
            var shot = new NetTeamDeathmatch.KillShot(1, 2, "rifle", HitZone.Body);

            var entry = NetTeamDeathmatch.KillFeedEntryFor(1, 2, friendly: true, shot);

            Assert.That(entry, Is.EqualTo(new KillFeedEntry(1, 2, "rifle", headshot: false, friendly: true)));
        }

        [Test]
        public void ReflectedShotKillsTheShooterWithTheWeaponButNoHeadshot()
        {
            var shot = new NetTeamDeathmatch.KillShot(1, 2, "rifle", HitZone.Head);

            var entry = NetTeamDeathmatch.KillFeedEntryFor(1, 1, friendly: false, shot);

            Assert.That(entry, Is.EqualTo(new KillFeedEntry(1, 1, "rifle", headshot: false, friendly: false)));
        }

        [Test]
        public void KillCreditedToAnotherEnemyHasNoWeapon()
        {
            // ダウン中の人に味方 (1) がとどめを刺し、キルは最後に当てた敵 (3) に付く
            var shot = new NetTeamDeathmatch.KillShot(1, 2, "rifle", HitZone.Head);

            var entry = NetTeamDeathmatch.KillFeedEntryFor(3, 2, friendly: false, shot);

            Assert.That(entry, Is.EqualTo(new KillFeedEntry(3, 2, null, headshot: false, friendly: false)));
        }

        [Test]
        public void OtherPlayerFallingWithTheShotHasNoWeapon()
        {
            // 撃たれた人の全滅で、同じチームのダウン中の人 (4) も倒れる
            var shot = new NetTeamDeathmatch.KillShot(1, 2, "rifle", HitZone.Head);

            var entry = NetTeamDeathmatch.KillFeedEntryFor(1, 4, friendly: false, shot);

            Assert.That(entry, Is.EqualTo(new KillFeedEntry(1, 4, null, headshot: false, friendly: false)));
        }

        [Test]
        public void KillOutsideAShotHasNoWeapon()
        {
            var entry = NetTeamDeathmatch.KillFeedEntryFor(1, 2, friendly: false, null);

            Assert.That(entry, Is.EqualTo(new KillFeedEntry(1, 2, null, headshot: false, friendly: false)));
        }

        [Test]
        public void LogsAreTaggedForTheCombatCheck()
        {
            Assert.That(
                NetCombatFeed.HitConfirmedLog(new HitConfirm(25, HitZone.Head, killed: false, downed: false)),
                Is.EqualTo("[Combat] HitConfirmed damage=25 zone=Head killed=False downed=False"));
            Assert.That(NetCombatFeed.DamageTakenLog(null, 25), Is.EqualTo("[Combat] DamageTaken amount=25 source=none"));
            Assert.That(
                NetCombatFeed.DamageTakenLog(new Vector3(1f, 0f, -22f), 25),
                Is.EqualTo("[Combat] DamageTaken amount=25 source=(1.00, 0.00, -22.00)"));
            Assert.That(
                NetCombatFeed.KilledLog(new KillFeedEntry(1, 2, "rifle", headshot: true, friendly: false)),
                Is.EqualTo("[Combat] Killed killer=1 victim=2 weapon=rifle headshot=True friendly=False"));
        }
    }
}
