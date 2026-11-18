using System.Linq;
using System.Numerics;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;

namespace Shooter.Net.Tests
{
    public sealed class ClientReplayViewTests
    {
        private const int TickRate = 10;
        private const int Recorder = 3;

        [Test]
        public void TheViewStartsOnTheRecordedPlayersEyes()
        {
            var view = NewView();
            Assert.That(view.TargetId, Is.EqualTo(Recorder), "before any snapshot the view waits for the recorder");

            view.Apply(Snapshot(10, (1, new Vector3(1f, 0f, 1f)), (Recorder, new Vector3(5f, 0f, 7f))));
            Assert.That(view.Update(1f), Is.False);

            Assert.That(view.TryGetView(out var eye, out _), Is.True);
            Assert.That(eye.X, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(eye.Z, Is.EqualTo(7f).Within(1e-4f));
            Assert.That(eye.Y, Is.GreaterThan(0f), "the eye sits above the feet");
        }

        [Test]
        public void NextAndPreviousGoThroughEveryoneInIdOrderAndThenTheFreeCamera()
        {
            var view = NewView();
            view.Apply(Snapshot(10, (1, Vector3.Zero), (Recorder, Vector3.Zero), (8, Vector3.Zero)));
            view.Update(0f);

            Assert.That(view.Step(1), Is.True);
            Assert.That(view.TargetId, Is.EqualTo(8));
            Assert.That(view.Step(1), Is.True);
            Assert.That(view.IsFree, Is.True, "the free camera comes after the last player");
            Assert.That(view.TryGetView(out _, out _), Is.False, "the free camera is moved by input, not by a player");
            Assert.That(view.Step(1), Is.True);
            Assert.That(view.TargetId, Is.EqualTo(1), "and the list wraps around");
            Assert.That(view.Step(-1), Is.True);
            Assert.That(view.IsFree, Is.True);
            Assert.That(view.Step(0), Is.False);
            Assert.That(view.PlayerIds.ToArray(), Is.EqualTo(new[] { 1, Recorder, 8 }));
        }

        [Test]
        public void WhenTheWatchedPlayerLeavesTheViewMovesToTheNextOneOrTheFreeCamera()
        {
            var view = NewView();
            view.Apply(Snapshot(10, (1, Vector3.Zero), (Recorder, Vector3.Zero), (8, Vector3.Zero)));
            view.Update(0f);

            view.Apply(Snapshot(11, (1, Vector3.Zero), (8, Vector3.Zero)));
            Assert.That(view.Update(0.1f), Is.True);
            Assert.That(view.TargetId, Is.EqualTo(8));

            view.Apply(Snapshot(12, (1, Vector3.Zero)));
            Assert.That(view.Update(0.1f), Is.True);
            Assert.That(view.TargetId, Is.EqualTo(1), "past the last id it starts again from the first");

            view.Apply(Snapshot(13));
            Assert.That(view.Update(0.1f), Is.True);
            Assert.That(view.IsFree, Is.True, "with nobody left only the free camera remains");
            Assert.That(view.Update(0.1f), Is.False, "the free camera stays until someone is chosen");
        }

        [Test]
        public void FollowPicksOnlyAPlayerWhoIsInTheReplay()
        {
            var view = NewView();
            view.Apply(Snapshot(10, (1, Vector3.Zero), (Recorder, Vector3.Zero)));

            Assert.That(view.Follow(42), Is.False);
            Assert.That(view.Follow(1), Is.True);
            Assert.That(view.FreeCamera(), Is.True);
            Assert.That(view.FreeCamera(), Is.False);

            view.Reset();
            Assert.That(view.TargetId, Is.EqualTo(Recorder));
            Assert.That(view.PlayerCount, Is.EqualTo(0));
        }

        private static ClientReplayView NewView()
        {
            return new ClientReplayView(Recorder, TickRate, NetMotorSettings.Default.Stance);
        }

        private static SnapshotMessage Snapshot(int tick, params (int Id, Vector3 Position)[] players)
        {
            var snapshot = new SnapshotMessage { Tick = tick };
            snapshot.Match.State = GameModeState.Playing;
            foreach (var player in players)
            {
                ref var row = ref snapshot.Players.Add();
                row.PlayerId = player.Id;
                row.Team = Team.Alpha;
                row.Health = 100;
                row.Position = player.Position;
            }

            return snapshot;
        }
    }
}
