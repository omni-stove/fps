using System;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class PlayerActionsTests
    {
        private GameObject _player;

        private static GameRules Rules(Cover cover, BlindFire blindFire, Dodge dodge = Dodge.Off)
        {
            var actions = new ActionRules(
                Downed.Off,
                ActionRules.DefaultDownedBleedoutSeconds,
                ActionRules.DefaultDownedHealthFraction,
                ActionRules.DefaultReviveSeconds,
                cover,
                blindFire,
                ActiveReload.Off,
                dodge);
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
                actions);
        }

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("Player");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_player);
        }

        [Test]
        public void DefaultRulesAttachBothActionsDisabled()
        {
            LogAssert.Expect(LogType.Log, "[Actions] cover=Off blindFire=Off activeReload=Off downed=Off dodge=Off");

            PlayerActions.Attach(_player, GameRules.Default);

            var cover = _player.GetComponent<CoverController>();
            var blindFire = _player.GetComponent<BlindFireController>();
            Assert.That(cover, Is.Not.Null);
            Assert.That(blindFire, Is.Not.Null);
            Assert.That(cover.enabled, Is.False);
            Assert.That(blindFire.enabled, Is.False);
        }

        [Test]
        public void CoverOnEnablesOnlyTheCover()
        {
            LogAssert.Expect(LogType.Log, "[Actions] cover=On blindFire=Off activeReload=Off downed=Off dodge=Off");

            PlayerActions.Attach(_player, Rules(Cover.On, BlindFire.Off));

            Assert.That(_player.GetComponent<CoverController>().enabled, Is.True);
            Assert.That(_player.GetComponent<BlindFireController>().enabled, Is.False);
        }

        [Test]
        public void BlindFireOnEnablesBoth()
        {
            LogAssert.Expect(LogType.Log, "[Actions] cover=On blindFire=On activeReload=Off downed=Off dodge=Off");

            PlayerActions.Attach(_player, Rules(Cover.On, BlindFire.On));

            Assert.That(_player.GetComponent<CoverController>().enabled, Is.True);
            Assert.That(_player.GetComponent<BlindFireController>().enabled, Is.True);
        }

        [Test]
        public void AttachingTwiceDoesNotAddComponentsAndFollowsTheLastRules()
        {
            PlayerActions.Attach(_player, Rules(Cover.On, BlindFire.On));
            PlayerActions.Attach(_player, GameRules.Default);

            Assert.That(_player.GetComponents<CoverController>(), Has.Length.EqualTo(1));
            Assert.That(_player.GetComponents<BlindFireController>(), Has.Length.EqualTo(1));
            Assert.That(_player.GetComponent<CoverController>().enabled, Is.False);
            Assert.That(_player.GetComponent<BlindFireController>().enabled, Is.False);
        }

        [Test]
        public void CoverStartsOutOfCover()
        {
            PlayerActions.Attach(_player, Rules(Cover.On, BlindFire.Off));
            var cover = _player.GetComponent<CoverController>();

            Assert.That(cover.IsInCover, Is.False);
            Assert.That(cover.IsPeeking, Is.False);
        }

        [Test]
        public void DodgeFollowsTheRulesOnThePlayerController()
        {
            var mover = _player.AddComponent<PlayerController>();
            Assert.That(mover.DodgeRule, Is.EqualTo(Dodge.Off));

            LogAssert.Expect(LogType.Log, "[Actions] cover=Off blindFire=Off activeReload=Off downed=Off dodge=On");
            PlayerActions.Attach(_player, Rules(Cover.Off, BlindFire.Off, Dodge.On));
            Assert.That(mover.DodgeRule, Is.EqualTo(Dodge.On));

            PlayerActions.Attach(_player, GameRules.Default);
            Assert.That(mover.DodgeRule, Is.EqualTo(Dodge.Off));
        }

        [Test]
        public void DodgeIsBlockedOnlyWhileInCover()
        {
            var mover = _player.AddComponent<PlayerController>();

            PlayerActions.Attach(_player, Rules(Cover.On, BlindFire.Off, Dodge.On));

            Assert.That(mover.DodgeBlocker, Is.Not.Null);
            Assert.That(mover.DodgeBlocker(), Is.False, "out of cover");
        }

        [Test]
        public void AttachWithoutAPlayerControllerStillWorks()
        {
            PlayerActions.Attach(_player, Rules(Cover.Off, BlindFire.Off, Dodge.On));

            Assert.That(_player.GetComponent<PlayerController>(), Is.Null);
        }

        [Test]
        public void AttachRejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => PlayerActions.Attach(null, GameRules.Default));
            Assert.Throws<ArgumentNullException>(() => PlayerActions.Attach(_player, null));
            Assert.That(_player.GetComponent<CoverController>(), Is.Null);
        }

        [Test]
        public void ConfigureRejectsMissingRules()
        {
            var cover = _player.AddComponent<CoverController>();
            var blindFire = _player.AddComponent<BlindFireController>();

            Assert.Throws<ArgumentNullException>(() => cover.Configure(null));
            Assert.Throws<ArgumentNullException>(() => blindFire.Configure(null));
        }
    }
}
