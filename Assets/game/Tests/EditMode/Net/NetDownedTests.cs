using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;
using UnityEngine;
using NumericsVector3 = System.Numerics.Vector3;

namespace Fps.Net.Tests
{
    /// <summary>
    /// ネット対戦のダウンと蘇生。<see cref="NetTeamDeathmatch"/> がサーバーで使うのと同じ判定
    /// (<see cref="NetTeamDeathmatch.CanFire"/>・<see cref="ReviveReach.Check"/>・<see cref="NetTeamDeathmatch.AdvanceRevives"/>) を、
    /// 実際の <see cref="TeamDeathmatchSession"/> で確かめる。ネットワークは起動しない。
    /// </summary>
    public sealed class NetDownedTests
    {
        private const int _maxHealth = 100;
        private const float _bleedoutSeconds = 10f;
        private const float _reviveSeconds = 1f;
        private const float _healthFraction = 0.3f;
        private const float _frame = 0.1f;

        // Join の順に Alpha・Bravo・Alpha・Bravo
        private const int _alpha1 = 0;
        private const int _bravo1 = 1;
        private const int _alpha2 = 2;
        private const int _bravo2 = 3;

        private readonly Dictionary<int, NumericsVector3> _positions = new Dictionary<int, NumericsVector3>();
        private readonly List<string> _events = new List<string>();
        private double _now;

        [SetUp]
        public void SetUp()
        {
            _positions.Clear();
            _events.Clear();
            _now = 100d;
        }

        private static GameRules Rules(Downed downed)
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off,
                new ActionRules(downed, _bleedoutSeconds, _healthFraction, _reviveSeconds, Cover.Off, BlindFire.Off, ActiveReload.Off));
        }

        /// <summary>2 対 2 で始めた試合。全員を原点に置く。</summary>
        private TeamDeathmatchSession StartTwoOnTwo(Downed downed)
        {
            var points = new[]
            {
                new SpawnPoint(new NumericsVector3(0f, 0f, -20f), Team.Alpha),
                new SpawnPoint(new NumericsVector3(0f, 0f, 20f), Team.Bravo),
            };
            var session = new TeamDeathmatchSession(
                new TeamDeathmatchSpec(100, 300f, 3f, Rules(downed)), _maxHealth, points, new SpawnSelector(), PositionOf);
            session.Downed += (attacker, victim) => _events.Add(NetDownedLog.Downed(attacker, victim));
            session.Revived += (reviver, target) => _events.Add(NetDownedLog.Revived(reviver, target));

            foreach (var id in new[] { _alpha1, _bravo1, _alpha2, _bravo2 })
            {
                _positions[id] = NumericsVector3.Zero;
                session.Join(id);
            }

            Assert.That(session.State, Is.EqualTo(GameModeState.Playing));
            return session;
        }

        private NumericsVector3 PositionOf(int playerId)
        {
            return _positions.TryGetValue(playerId, out var position) ? position : NumericsVector3.Zero;
        }

        private NumericsVector3? JoinedPositionOf(int playerId)
        {
            return _positions.TryGetValue(playerId, out var position) ? position : (NumericsVector3?)null;
        }

        private ReviveRejection Check(TeamDeathmatchSession session, int reviverId, int targetId)
        {
            return ReviveReach.Check(session, reviverId, targetId, PositionOf(reviverId), PositionOf(targetId), ReviveReach.DefaultRange);
        }

        /// <summary>サーバーの 1 フレーム。届いた要求を覚え、蘇生を進めてから Session の時間を進める (NetTeamDeathmatch.Update と同じ順)。</summary>
        private int Frame(TeamDeathmatchSession session, ReviveHoldTable holds, List<ReviveRejection> rejected = null)
        {
            var advanced = NetTeamDeathmatch.AdvanceRevives(
                session, holds, JoinedPositionOf, _now, _frame, ReviveReach.DefaultRange, (r, t, reason) => rejected?.Add(reason));
            session.Tick(_frame);
            _now += _frame;
            return advanced;
        }

        private static TeamDeathmatchSession DownAlpha1(TeamDeathmatchSession session)
        {
            Assert.That(session.ApplyDamage(_bravo1, _alpha1, _maxHealth), Is.EqualTo(DamageOutcome.Downed));
            Assert.That(session.IsDowned(_alpha1), Is.True);
            return session;
        }

        [Test]
        public void DownedPlayersShotsAreDropped()
        {
            var session = DownAlpha1(StartTwoOnTwo(Downed.Revive));

            Assert.That(session.IsAlive(_alpha1), Is.True, "a downed player is still alive");
            Assert.That(NetTeamDeathmatch.CanFire(session, _alpha1), Is.False);
            Assert.That(NetTeamDeathmatch.CanFire(session, _alpha2), Is.True);
            Assert.That(NetTeamDeathmatch.CanFire(session, _bravo1), Is.True);
            Assert.That(_events, Is.EqualTo(new[] { "[Net] downed attacker=1 victim=0" }));
        }

        [Test]
        public void RevivedPlayerCanFireAgain()
        {
            var session = DownAlpha1(StartTwoOnTwo(Downed.Revive));
            var holds = new ReviveHoldTable();

            for (var i = 0; i < 20 && session.IsDowned(_alpha1); i++)
            {
                holds.Hold(_alpha2, _alpha1, _now);
                Frame(session, holds);
            }

            Assert.That(session.IsDowned(_alpha1), Is.False);
            Assert.That(NetTeamDeathmatch.CanFire(session, _alpha1), Is.True);
        }

        [Test]
        public void HoldingInteractNextToTheDownedAllyCompletesTheRevive()
        {
            var session = DownAlpha1(StartTwoOnTwo(Downed.Revive));
            _positions[_alpha2] = new NumericsVector3(1f, 0f, 0f);
            var holds = new ReviveHoldTable();
            Assert.That(Check(session, _alpha2, _alpha1), Is.EqualTo(ReviveRejection.None));

            // 1 秒の蘇生を 0.1 秒のフレームで進める。押している間、要求は毎フレーム届く
            var frames = 0;
            while (session.IsDowned(_alpha1) && frames < 20)
            {
                holds.Hold(_alpha2, _alpha1, _now);
                Assert.That(Frame(session, holds), Is.EqualTo(1), "frame " + frames);
                frames++;

                if (session.IsDowned(_alpha1))
                {
                    var synced = NetDownedState.From(session, _alpha1);
                    Assert.That(synced.IsDowned, Is.True);
                    Assert.That(synced.RevivePercent, Is.EqualTo(HudFormat.RevivePercent(session.GetReviveProgress(_alpha1))));
                    Assert.That(synced.RevivePercent, Is.GreaterThan(0));
                }
            }

            Assert.That(frames, Is.InRange(10, 11), "progress follows the server frames, not the number of requests");
            Assert.That(session.IsDowned(_alpha1), Is.False);
            Assert.That(session.GetHealth(_alpha1), Is.EqualTo(DownedTracker.HealthFor(_maxHealth, _healthFraction)));
            Assert.That(NetDownedState.From(session, _alpha1), Is.EqualTo(NetDownedState.None));
            Assert.That(_events, Has.Member("[Net] revived reviver=2 target=0"));
        }

        [Test]
        public void SendingRequestsFasterDoesNotReviveFaster()
        {
            var session = DownAlpha1(StartTwoOnTwo(Downed.Revive));
            var holds = new ReviveHoldTable();

            for (var i = 0; i < 5; i++)
            {
                // 1 フレームにまとめて 10 回届いても、進むのはそのフレームの秒数だけ
                for (var j = 0; j < 10; j++)
                {
                    holds.Hold(_alpha2, _alpha1, _now);
                }

                Frame(session, holds);
            }

            Assert.That(session.IsDowned(_alpha1), Is.True);
            Assert.That(session.GetReviveProgress(_alpha1), Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void ReleasingInteractStopsAndResetsTheRevive()
        {
            var session = DownAlpha1(StartTwoOnTwo(Downed.Revive));
            var holds = new ReviveHoldTable();
            for (var i = 0; i < 5; i++)
            {
                holds.Hold(_alpha2, _alpha1, _now);
                Frame(session, holds);
            }

            Assert.That(session.GetReviveProgress(_alpha1), Is.GreaterThan(0f));

            // 離すと要求が届かなくなり、猶予を過ぎたら進みが 0 に戻る
            for (var i = 0; i < 5; i++)
            {
                Frame(session, holds);
            }

            Assert.That(holds.Count, Is.Zero);
            Assert.That(session.GetReviveProgress(_alpha1), Is.Zero);
            Assert.That(session.IsDowned(_alpha1), Is.True);
        }

        [Test]
        public void ReviveFromTooFarIsRejected()
        {
            var session = DownAlpha1(StartTwoOnTwo(Downed.Revive));
            _positions[_alpha2] = new NumericsVector3(ReviveReach.DefaultRange + 0.01f, 0f, 0f);

            Assert.That(Check(session, _alpha2, _alpha1), Is.EqualTo(ReviveRejection.TooFar));

            // 覚えている要求も、フレームごとの確かめ直しで距離外なら進めずに捨てる
            var holds = new ReviveHoldTable();
            holds.Hold(_alpha2, _alpha1, _now);
            var rejected = new List<ReviveRejection>();
            Assert.That(Frame(session, holds, rejected), Is.Zero);
            Assert.That(rejected, Is.EqualTo(new[] { ReviveRejection.TooFar }));
            Assert.That(holds.Count, Is.Zero);
            Assert.That(session.GetReviveProgress(_alpha1), Is.Zero);
        }

        [Test]
        public void ReviveJustWithinTheRangeIsAccepted()
        {
            var session = DownAlpha1(StartTwoOnTwo(Downed.Revive));
            _positions[_alpha2] = new NumericsVector3(0f, 0f, ReviveReach.DefaultRange);

            Assert.That(Check(session, _alpha2, _alpha1), Is.EqualTo(ReviveRejection.None));
        }

        [Test]
        public void ReviveFromAnEnemyIsRejected()
        {
            var session = DownAlpha1(StartTwoOnTwo(Downed.Revive));
            Assert.That(Check(session, _bravo2, _alpha1), Is.EqualTo(ReviveRejection.Enemy));

            var holds = new ReviveHoldTable();
            var rejected = new List<ReviveRejection>();
            for (var i = 0; i < 20; i++)
            {
                holds.Hold(_bravo2, _alpha1, _now);
                Frame(session, holds, rejected);
            }

            Assert.That(rejected, Is.Not.Empty);
            Assert.That(rejected, Has.All.EqualTo(ReviveRejection.Enemy));
            Assert.That(session.IsDowned(_alpha1), Is.True, "an enemy never raises the player");
            Assert.That(session.GetReviveProgress(_alpha1), Is.Zero);
        }

        [Test]
        public void OtherInvalidRevivesAreRejected()
        {
            var session = DownAlpha1(StartTwoOnTwo(Downed.Revive));

            Assert.That(Check(session, _alpha1, _alpha1), Is.EqualTo(ReviveRejection.Self));
            Assert.That(Check(session, _bravo1, _bravo2), Is.EqualTo(ReviveRejection.TargetNotDowned));
            Assert.That(Check(session, _alpha1, _alpha2), Is.EqualTo(ReviveRejection.ReviverDown), "a downed player cannot revive");
            Assert.That(Check(session, _alpha2, 99), Is.EqualTo(ReviveRejection.NotJoined));
            Assert.That(
                ReviveReach.Check(session, _alpha2, _alpha1, new NumericsVector3(float.NaN, 0f, 0f), NumericsVector3.Zero, ReviveReach.DefaultRange),
                Is.EqualTo(ReviveRejection.InvalidPosition));
        }

        [Test]
        public void SyncedStateCarriesTheBleedoutSeconds()
        {
            var session = DownAlpha1(StartTwoOnTwo(Downed.Revive));
            Assert.That(NetDownedState.From(session, _alpha1), Is.EqualTo(new NetDownedState(true, 10, 0)));

            var holds = new ReviveHoldTable();
            for (var i = 0; i < 5; i++)
            {
                Frame(session, holds);
            }

            Assert.That(NetDownedState.From(session, _alpha1), Is.EqualTo(new NetDownedState(true, 10, 0)), "9.5 s rounds up to 10");

            for (var i = 0; i < 6; i++)
            {
                Frame(session, holds);
            }

            Assert.That(NetDownedState.From(session, _alpha1), Is.EqualTo(new NetDownedState(true, 9, 0)), "8.9 s rounds up to 9");
            Assert.That(NetDownedState.From(session, _alpha2), Is.EqualTo(NetDownedState.None));
        }

        [Test]
        public void OffRulesKeepTheCurrentBehaviour()
        {
            var session = StartTwoOnTwo(Downed.Off);

            Assert.That(session.ApplyDamage(_bravo1, _alpha1, _maxHealth), Is.EqualTo(DamageOutcome.Killed));
            Assert.That(session.IsDowned(_alpha1), Is.False);
            Assert.That(session.IsAlive(_alpha1), Is.False);
            Assert.That(NetTeamDeathmatch.CanFire(session, _alpha1), Is.False, "dead players still cannot fire");
            Assert.That(NetTeamDeathmatch.CanFire(session, _alpha2), Is.True);
            Assert.That(NetDownedState.From(session, _alpha1), Is.EqualTo(NetDownedState.None));
            Assert.That(Check(session, _alpha2, _alpha1), Is.EqualTo(ReviveRejection.DownedOff));
            Assert.That(_events, Is.Empty, "no downed or revived events with the Off rule");

            var holds = new ReviveHoldTable();
            holds.Hold(_alpha2, _alpha1, _now);
            Assert.That(Frame(session, holds), Is.Zero);
            Assert.That(holds.Count, Is.Zero);
        }

        [Test]
        public void DownedInputCannotMoveSprintOrJump()
        {
            var input = new MotorInput(new Vector2(0f, 1f), 1.25f, true, true, false, true);

            var restrained = input.Restrained();

            Assert.That(restrained.Move, Is.EqualTo(Vector2.zero));
            Assert.That(restrained.Sprint, Is.False);
            Assert.That(restrained.Jump, Is.False);
            Assert.That(restrained.Crouch, Is.True, "a downed body uses the low capsule");
            Assert.That(restrained.Yaw, Is.EqualTo(1.25f), "a downed player can still look around");
            Assert.That(restrained.Downed, Is.True);
        }

        [Test]
        public void StandingInputIsUnchanged()
        {
            var input = new MotorInput(new Vector2(0.5f, 0.5f), 0.5f, true, true, false);

            var restrained = input.Restrained();

            Assert.That(input.Downed, Is.False);
            Assert.That(restrained.Move, Is.EqualTo(input.Move));
            Assert.That(restrained.Sprint, Is.True);
            Assert.That(restrained.Jump, Is.True);
            Assert.That(restrained.Crouch, Is.False);
        }

        [TestCase(false, true, Team.Alpha, Team.Alpha, true)]
        [TestCase(false, true, Team.Bravo, Team.Alpha, false)]
        [TestCase(true, true, Team.Alpha, Team.Alpha, false)]
        [TestCase(false, false, Team.Alpha, Team.Alpha, false)]
        public void MarkerShowsOnlyOverDownedAllies(bool isOwner, bool isDowned, Team team, Team localTeam, bool expected)
        {
            Assert.That(NetPlayer.ShouldShowDownedMarker(isOwner, isDowned, team, localTeam), Is.EqualTo(expected));
        }

        [Test]
        public void MarkerIsHiddenWithoutALocalPlayer()
        {
            Assert.That(NetPlayer.ShouldShowDownedMarker(false, true, Team.Alpha, null), Is.False);
        }
    }
}
