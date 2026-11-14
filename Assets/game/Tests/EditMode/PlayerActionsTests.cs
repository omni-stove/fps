using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
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
        private const string NewActionsOff =
            " melee=Off stagger=Off slide=Off mantle=Off prone=Off ladder=Off zipline=Off parachute=Off stealth=Off suppression=Off ping=Off abilities=Off slowMotion=Off";

        private static readonly List<string> _calls = new List<string>();

        private GameObject _player;

        private static GameRules Rules(Cover cover, BlindFire blindFire, Dodge dodge = Dodge.Off, Vault vault = Vault.Off)
        {
            var actions = new ActionRules(
                Downed.Off,
                ActionRules.DefaultDownedBleedoutSeconds,
                ActionRules.DefaultDownedHealthFraction,
                ActionRules.DefaultReviveSeconds,
                cover,
                blindFire,
                ActiveReload.Off,
                dodge,
                vault);
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
            PlayerActions.Unregister(new EarlyInstaller());
            PlayerActions.Unregister(new LateInstaller());
            PlayerActions.Unregister(new ThrowingInstaller());
            _calls.Clear();
            Object.DestroyImmediate(_player);
        }

        [Test]
        public void TheBuiltInActionsAreInstalledInOrder()
        {
            var builtIn = new[]
            {
                typeof(CoverActionInstaller),
                typeof(BlindFireActionInstaller),
                typeof(MoveActionInstaller),
                typeof(ProneActionInstaller),
                typeof(GrenadeActionInstaller),
            };

            // 後から足したアクションが登録されていても確かめられるよう、組み込みだけを取り出して並びを見る。
            var types = PlayerActions.Installers.Select(i => i.GetType()).Where(t => builtIn.Contains(t)).ToArray();

            Assert.That(types, Is.EqualTo(builtIn));
        }

        [Test]
        public void RegisteredInstallersRunByOrderAroundTheBuiltIns()
        {
            PlayerActions.Register(new LateInstaller());
            PlayerActions.Register(new EarlyInstaller());

            var installers = PlayerActions.Installers;
            Assert.That(installers.First(), Is.InstanceOf<EarlyInstaller>());
            Assert.That(installers.Last(), Is.InstanceOf<LateInstaller>());

            LogAssert.Expect(LogType.Log, "[Actions] cover=Off blindFire=Off activeReload=Off downed=Off dodge=Off vault=Off" + NewActionsOff);
            PlayerActions.Attach(_player, GameRules.Default);

            Assert.That(_calls, Is.EqualTo(new[] { "early:Off", "late:Off" }));
            Assert.That(_player.GetComponent<CoverController>(), Is.Not.Null, "the built-ins still run");
        }

        [Test]
        public void InstallersReceiveTheRules()
        {
            PlayerActions.Register(new LateInstaller());

            PlayerActions.Attach(_player, GameRules.Default.WithActions(ActionRules.Default.WithMelee(Melee.On)));

            Assert.That(_calls, Is.EqualTo(new[] { "late:On" }));
        }

        [Test]
        public void RegisteringTheSameTypeAgainReplacesIt()
        {
            PlayerActions.Register(new LateInstaller());
            PlayerActions.Register(new LateInstaller());

            PlayerActions.Attach(_player, GameRules.Default);

            Assert.That(PlayerActions.Installers.Count(i => i is LateInstaller), Is.EqualTo(1));
            Assert.That(_calls, Is.EqualTo(new[] { "late:Off" }));
        }

        [Test]
        public void UnregisterRemovesTheInstaller()
        {
            PlayerActions.Register(new LateInstaller());

            Assert.That(PlayerActions.Unregister(new LateInstaller()), Is.True);
            Assert.That(PlayerActions.Unregister(new LateInstaller()), Is.False);

            PlayerActions.Attach(_player, GameRules.Default);
            Assert.That(_calls, Is.Empty);
        }

        [Test]
        public void AFailingInstallerIsReportedAndTheOthersStillRun()
        {
            PlayerActions.Register(new ThrowingInstaller());
            PlayerActions.Register(new LateInstaller());

            LogAssert.Expect(LogType.Error, new Regex(@"^\[Actions\] ThrowingInstaller failed to install on 'Player': broken$"));
            LogAssert.Expect(LogType.Exception, new Regex("broken"));
            PlayerActions.Attach(_player, GameRules.Default);

            Assert.That(_calls, Is.EqualTo(new[] { "late:Off" }));
            Assert.That(_player.GetComponent<BlindFireController>(), Is.Not.Null);
        }

        [Test]
        public void RegisterAndUnregisterRejectNull()
        {
            Assert.Throws<ArgumentNullException>(() => PlayerActions.Register(null));
            Assert.Throws<ArgumentNullException>(() => PlayerActions.Unregister(null));
        }

        [Test]
        public void TheLogEndsWithEveryNewActionInRuleOrder()
        {
            var actions = ActionRules.Default.WithSlide(Slide.On).WithSlowMotion(SlowMotion.On);

            Assert.That(
                PlayerActions.FormatLog(actions),
                Is.EqualTo("[Actions] cover=Off blindFire=Off activeReload=Off downed=Off dodge=Off vault=Off"
                    + " melee=Off stagger=Off slide=On mantle=Off prone=Off ladder=Off zipline=Off parachute=Off stealth=Off suppression=Off ping=Off abilities=Off slowMotion=On"));
        }

        [Test]
        public void DefaultRulesAttachBothActionsDisabled()
        {
            LogAssert.Expect(LogType.Log, "[Actions] cover=Off blindFire=Off activeReload=Off downed=Off dodge=Off vault=Off" + NewActionsOff);

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
            LogAssert.Expect(LogType.Log, "[Actions] cover=On blindFire=Off activeReload=Off downed=Off dodge=Off vault=Off" + NewActionsOff);

            PlayerActions.Attach(_player, Rules(Cover.On, BlindFire.Off));

            Assert.That(_player.GetComponent<CoverController>().enabled, Is.True);
            Assert.That(_player.GetComponent<BlindFireController>().enabled, Is.False);
        }

        [Test]
        public void BlindFireOnEnablesBoth()
        {
            LogAssert.Expect(LogType.Log, "[Actions] cover=On blindFire=On activeReload=Off downed=Off dodge=Off vault=Off" + NewActionsOff);

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

            LogAssert.Expect(LogType.Log, "[Actions] cover=Off blindFire=Off activeReload=Off downed=Off dodge=On vault=Off" + NewActionsOff);
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
        public void VaultFollowsTheRulesOnThePlayerController()
        {
            var mover = _player.AddComponent<PlayerController>();
            Assert.That(mover.VaultRule, Is.EqualTo(Vault.Off));

            LogAssert.Expect(LogType.Log, "[Actions] cover=Off blindFire=Off activeReload=Off downed=Off dodge=Off vault=On" + NewActionsOff);
            PlayerActions.Attach(_player, Rules(Cover.Off, BlindFire.Off, vault: Vault.On));
            Assert.That(mover.VaultRule, Is.EqualTo(Vault.On));

            PlayerActions.Attach(_player, GameRules.Default);
            Assert.That(mover.VaultRule, Is.EqualTo(Vault.Off));
        }

        [Test]
        public void ProneFollowsTheRulesOnThePlayerController()
        {
            var mover = _player.AddComponent<PlayerController>();
            Assert.That(mover.ProneRule, Is.EqualTo(Prone.Off));

            LogAssert.Expect(
                LogType.Log,
                "[Actions] cover=Off blindFire=Off activeReload=Off downed=Off dodge=Off vault=Off"
                    + NewActionsOff.Replace("prone=Off", "prone=On"));
            PlayerActions.Attach(_player, GameRules.Default.WithActions(ActionRules.Default.WithProne(Prone.On)));
            Assert.That(mover.ProneRule, Is.EqualTo(Prone.On));

            PlayerActions.Attach(_player, GameRules.Default);
            Assert.That(mover.ProneRule, Is.EqualTo(Prone.Off));
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

        // 型を探して自動で拾う仕組みではないので、テスト用の取り付けが本番の Attach に紛れ込むことはない。
        private sealed class EarlyInstaller : IPlayerActionInstaller
        {
            public int Order => CoverActionInstaller.InstallOrder - 1;

            public void Install(GameObject player, GameRules rules)
            {
                _calls.Add("early:" + rules.Actions.Melee);
            }
        }

        private sealed class LateInstaller : IPlayerActionInstaller
        {
            public int Order => GrenadeActionInstaller.InstallOrder + 1;

            public void Install(GameObject player, GameRules rules)
            {
                _calls.Add("late:" + rules.Actions.Melee);
            }
        }

        private sealed class ThrowingInstaller : IPlayerActionInstaller
        {
            public int Order => CoverActionInstaller.InstallOrder + 1;

            public void Install(GameObject player, GameRules rules)
            {
                throw new InvalidOperationException("broken");
            }
        }
    }
}
