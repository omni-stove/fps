using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Net.Tests
{
    public sealed class NetV2PlayerStatusPartTests
    {
        private readonly List<StaminaStatus?> _heard = new List<StaminaStatus?>();
        private readonly RecordingPresenter _presenter = new RecordingPresenter();
        private GameObject _player;
        private FirstPersonLook _look;
        private NetV2PlayerStatusPart _part;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ResetAll();
            _heard.Clear();
            _presenter.Levels.Clear();
            GameSignals.Stamina.Changed += _heard.Add;
            _player = new GameObject("NetV2PlayerStatusPartTests");
            _look = _player.AddComponent<FirstPersonLook>();
            _part = new NetV2PlayerStatusPart(() => _presenter, () => _look, SuppressionSpec.Default, StaggerSpec.PlayerDefault);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            Object.DestroyImmediate(_player);
            GameSignals.ResetAll();
        }

        [Test]
        public void TheOwnersStaminaIsPublishedForTheHud()
        {
            _part.Apply(Snapshot(new StaminaStatus(40f, 100f, false), StaggerKind.None, 0f));

            Assert.That(GameSignals.Stamina.Value, Is.EqualTo(new StaminaStatus(40f, 100f, false)));
            Assert.That(_part.Stamina, Is.EqualTo(new StaminaStatus(40f, 100f, false)));
        }

        [Test]
        public void TheSameStaminaIsNotPublishedAgain()
        {
            _part.Apply(Snapshot(new StaminaStatus(40f, 100f, false), StaggerKind.None, 0f));
            _part.Apply(Snapshot(new StaminaStatus(40f, 100f, false), StaggerKind.None, 0f));

            Assert.That(_heard, Has.Count.EqualTo(1));
        }

        [Test]
        public void StaminaTurnedOffClearsTheSignal()
        {
            _part.Apply(Snapshot(new StaminaStatus(40f, 100f, false), StaggerKind.None, 0f));
            _part.Apply(Snapshot(null, StaggerKind.None, 0f));

            Assert.That(GameSignals.Stamina.HasValue, Is.False);
        }

        [Test]
        public void TheSuppressionLevelIsPresentedEveryTickAndZeroWhenDisposed()
        {
            _part.Apply(Snapshot(null, StaggerKind.None, 0.6f));

            _part.Tick(0.016f);
            _part.Tick(0.016f);
            _part.Dispose();

            Assert.That(_presenter.Levels, Is.EqualTo(new[] { 0.6f, 0.6f, 0f }));
            Assert.That(_presenter.Spec, Is.SameAs(SuppressionSpec.Default));
        }

        [Test]
        public void AStaggerSlowsTheLookUntilItEnds()
        {
            _part.Apply(Snapshot(null, StaggerKind.Stumble, 0f));
            _part.Tick(0.016f);

            Assert.That(_look.TurnMultiplier, Is.EqualTo(StaggerSpec.DefaultPlayerTurnMultiplier));

            _part.Apply(Snapshot(null, StaggerKind.None, 0f));
            _part.Tick(0.016f);

            Assert.That(_look.TurnMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void DisposingClearsTheStaminaAndRestoresTheLook()
        {
            _part.Apply(Snapshot(new StaminaStatus(1f, 100f, true), StaggerKind.Blast, 0f));
            _part.Tick(0.016f);

            _part.Dispose();

            Assert.That(GameSignals.Stamina.HasValue, Is.False);
            Assert.That(_look.TurnMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void ASnapshotWithoutAnOwnerChangesNothing()
        {
            _part.Apply(Snapshot(new StaminaStatus(40f, 100f, false), StaggerKind.None, 0f));
            var spectating = Snapshot(null, StaggerKind.None, 0f);
            spectating.HasOwner = false;

            _part.Apply(spectating);

            Assert.That(GameSignals.Stamina.Value, Is.EqualTo(new StaminaStatus(40f, 100f, false)));
        }

        private static SnapshotMessage Snapshot(StaminaStatus? stamina, StaggerKind stagger, float suppression)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Clear();
            snapshot.HasOwner = true;
            snapshot.Owner.Stamina = stamina;
            snapshot.Owner.Stagger = stagger;
            snapshot.Owner.SuppressionLevel = suppression;
            return snapshot;
        }

        private sealed class RecordingPresenter : ISuppressionPresenter
        {
            public List<float> Levels { get; } = new List<float>();

            public SuppressionSpec Spec { get; private set; }

            public void Present(float level, SuppressionSpec spec)
            {
                Levels.Add(level);
                Spec = spec;
            }
        }
    }
}
