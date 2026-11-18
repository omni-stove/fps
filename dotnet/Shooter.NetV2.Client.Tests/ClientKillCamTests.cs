using System;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientKillCamTests
    {
        private const int _self = 1;
        private const int _killer = 2;
        private const int _health = 100;
        private const string _rifleId = "weapon.rifle";
        private const float _tolerance = 1e-4f;

        private ClientKillCam _killCam;

        [SetUp]
        public void SetUp()
        {
            _killCam = new ClientKillCam(_self);
        }

        [Test]
        public void AClipForMeStartsAReplayFromTheKillersEyeWithTheWeaponThatKilledMe()
        {
            _killCam.Apply(Killed(_killer, _self, _rifleId));

            Assert.That(_killCam.Apply(Clip(_killer, _self, 2f)), Is.True);

            Assert.That(_killCam.IsPlaying, Is.True);
            Assert.That(_killCam.KillerId, Is.EqualTo(_killer));
            Assert.That(_killCam.Status, Is.EqualTo(new KillCamStatus("P2", _rifleId, 2f, 0f, 1f)));
            Assert.That(_killCam.TryGetView(out var eye, out var yaw), Is.True);
            Assert.That(eye, Is.EqualTo(new Vector3(0f, 1.6f, 0f)));
            Assert.That(yaw, Is.EqualTo(90f));
        }

        [Test]
        public void AClipForSomeoneElseIsIgnored()
        {
            Assert.That(_killCam.Apply(Clip(_killer, 5, 2f)), Is.False);

            Assert.That(_killCam.IsPlaying, Is.False);
            Assert.That(_killCam.Status, Is.Null);
            Assert.That(_killCam.TryGetView(out _, out _), Is.False);
        }

        [Test]
        public void AKillOfSomeoneElseDoesNotNameTheWeapon()
        {
            _killCam.Apply(Killed(_killer, 5, _rifleId));
            _killCam.Apply(Killed(3, _self, "weapon.knife"));

            _killCam.Apply(Clip(_killer, _self, 2f));

            Assert.That(_killCam.Status.Value.WeaponId, Is.Empty, "the last kill of me was by someone else");
        }

        [Test]
        public void TheReplayMovesAtTheGivenSpeedAndEndsAtTheClipsLength()
        {
            _killCam = new ClientKillCam(_self, 0.5f);
            _killCam.Apply(Clip(_killer, _self, 2f));

            Assert.That(_killCam.Update(1f), Is.True);
            Assert.That(_killCam.Status.Value.ElapsedSeconds, Is.EqualTo(0.5f).Within(_tolerance));
            Assert.That(_killCam.Status.Value.RemainingSeconds, Is.EqualTo(3f).Within(_tolerance));
            _killCam.TryGetView(out var eye, out _);
            Assert.That(eye.X, Is.EqualTo(2.5f).Within(_tolerance), "a quarter of the way from 0 to 10");

            Assert.That(_killCam.Update(2.9f), Is.True);
            Assert.That(_killCam.IsPlaying, Is.True);
            Assert.That(_killCam.Update(0.2f), Is.True, "reaching the end clears the status");
            Assert.That(_killCam.IsPlaying, Is.False);
            Assert.That(_killCam.Status, Is.Null);
            Assert.That(_killCam.Update(1f), Is.False);
        }

        [Test]
        public void AnEmptyClipPlaysNothing()
        {
            Assert.That(_killCam.Apply(new KillCamClipMessage { KillerId = _killer, VictimId = _self }), Is.False);

            Assert.That(_killCam.IsPlaying, Is.False);
            Assert.That(_killCam.Status, Is.Null);
        }

        [Test]
        public void RespawningStopsTheReplayOnlyAfterBeingSeenDead()
        {
            _killCam.Apply(Clip(_killer, _self, 2f));

            Assert.That(_killCam.Apply(Snapshot(10, Row(_self, _health))), Is.False, "a late snapshot from before the kill");
            Assert.That(_killCam.IsPlaying, Is.True);

            Assert.That(_killCam.Apply(Snapshot(11, Row(_self, 0))), Is.False);
            Assert.That(_killCam.Apply(Snapshot(9, Row(_self, _health))), Is.False, "older than the newest");
            Assert.That(_killCam.IsPlaying, Is.True);

            Assert.That(_killCam.Apply(Snapshot(12, Row(_self, _health))), Is.True);
            Assert.That(_killCam.IsPlaying, Is.False);
            Assert.That(_killCam.Status, Is.Null);
        }

        [Test]
        public void ANewClipReplacesTheOneThatIsPlaying()
        {
            _killCam.Apply(Clip(_killer, _self, 2f));
            _killCam.Update(1f);

            _killCam.Apply(Clip(3, _self, 3f));

            Assert.That(_killCam.KillerId, Is.EqualTo(3));
            Assert.That(_killCam.Status.Value.DurationSeconds, Is.EqualTo(3f));
            Assert.That(_killCam.Status.Value.ElapsedSeconds, Is.EqualTo(0f));
        }

        [Test]
        public void ResetForgetsTheReplayAndTheSnapshots()
        {
            _killCam.Apply(Killed(_killer, _self, _rifleId));
            _killCam.Apply(Clip(_killer, _self, 2f));
            _killCam.Apply(Snapshot(20, Row(_self, 0)));

            Assert.That(_killCam.Reset(), Is.True);
            Assert.That(_killCam.Status, Is.Null);

            _killCam.Apply(Clip(_killer, _self, 2f));
            Assert.That(_killCam.Status.Value.WeaponId, Is.Empty, "the weapon was forgotten");
            Assert.That(_killCam.Apply(Snapshot(1, Row(_self, 0))), Is.False);
            Assert.That(_killCam.Apply(Snapshot(2, Row(_self, _health))), Is.True, "ticks start over after a reset");
        }

        [Test]
        public void InvalidArgumentsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientKillCam(_self, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientKillCam(_self, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientKillCam(_self, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => _killCam.Apply((KillCamClipMessage)null));
            Assert.Throws<ArgumentNullException>(() => _killCam.Apply((KilledMessage)null));
            Assert.Throws<ArgumentNullException>(() => _killCam.Apply((SnapshotMessage)null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _killCam.Update(-1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => _killCam.Update(float.NaN));
        }

        private static KilledMessage Killed(int killerId, int victimId, string weaponId)
        {
            return new KilledMessage { KillerId = killerId, VictimId = victimId, WeaponId = weaponId };
        }

        // 目は x を 0 から 10 へ一定の速さで動かし、向きは 90° のまま
        private static KillCamClipMessage Clip(int killerId, int victimId, float seconds)
        {
            var clip = new KillCamClipMessage { KillerId = killerId, VictimId = victimId };
            clip.Add(0f, new KillCamPose(new Vector3(0f, 1.6f, 0f), 90f));
            clip.Add(seconds, new KillCamPose(new Vector3(10f, 1.6f, 0f), 90f));
            return clip;
        }

        private static SnapshotMessage Snapshot(int tick, params PlayerSnapshot[] rows)
        {
            var snapshot = new SnapshotMessage { Tick = tick };
            foreach (var source in rows)
            {
                ref var row = ref snapshot.Players.Add();
                row = source;
            }

            return snapshot;
        }

        private static PlayerSnapshot Row(int id, int health)
        {
            return new PlayerSnapshot
            {
                PlayerId = id,
                Team = Team.Alpha,
                Health = health,
                WeaponId = "",
            };
        }
    }
}
