using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class EmoteControllerTests
    {
        private const string _wave = "wave";
        private const string _dance = "dance";
        private const string _stand = "stand";

        private static readonly EmoteSpec[] _emotes =
        {
            new EmoteSpec(_wave, "emote.wave", durationSeconds: 2f),
            new EmoteSpec(_dance, "emote.dance", durationSeconds: 1f, loops: true),
            new EmoteSpec(_stand, "emote.stand", durationSeconds: 5f, stopsOnMove: false),
        };

        private readonly List<Object> _objects = new List<Object>();
        private float _now;

        private static GameRules EmoteRules(Emote emote)
        {
            return GameRules.Default.WithActions(GameRules.Default.Actions.WithEmote(emote));
        }

        [SetUp]
        public void SetUp()
        {
            _now = 0f;
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _objects)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _objects.Clear();
            CombatEvents.ClearSubscribers();
            GameSignals.ResetAll();
        }

        [Test]
        public void OffNeverOpensTheWheelNorPlays()
        {
            var emotes = Player(Emote.Off);

            Assert.That(emotes.UsesEmotes, Is.False);
            Assert.That(emotes.EmoteIds, Is.Empty);
            Assert.That(emotes.OpenWheel(), Is.False);
            Assert.That(GameSignals.Emotes.HasValue, Is.False);

            LogAssert.Expect(LogType.Log, EmoteLog.Refused(_wave, EmoteRefusal.RuleOff));
            Assert.That(emotes.Play(_wave), Is.False);
            Assert.That(emotes.LastRefusal, Is.EqualTo(EmoteRefusal.RuleOff));
            Assert.That(emotes.IsPlaying, Is.False);
            Assert.That(emotes.GetComponent<ViewRig>().Mode, Is.EqualTo(ViewMode.FirstPerson));
        }

        [Test]
        public void HoldingTheWheelPublishesTheControlAndReleasingItTakesItDown()
        {
            var emotes = Player(Emote.On);

            Assert.That(GameSignals.Emotes.HasValue, Is.False, "nothing is shown until the button is held");
            Assert.That(emotes.OpenWheel(), Is.True);
            Assert.That(GameSignals.Emotes.Value, Is.SameAs(emotes));
            Assert.That(GameSignals.Emotes.Value.EmoteIds, Is.EqualTo(new[] { _wave, _dance, _stand }));

            emotes.CloseWheel();
            Assert.That(emotes.IsWheelOpen, Is.False);
            Assert.That(GameSignals.Emotes.HasValue, Is.False);
        }

        [Test]
        public void PlayingAKnownEmoteMovesTheCameraBehindThePlayer()
        {
            var emotes = Player(Emote.On);

            LogAssert.Expect(LogType.Log, EmoteLog.Played(_emotes[0], hasAnimation: false));
            Assert.That(emotes.Play(_wave), Is.True);

            Assert.That(emotes.IsPlaying, Is.True);
            Assert.That(emotes.Current.Id, Is.EqualTo(_wave));
            Assert.That(emotes.LastRefusal, Is.EqualTo(EmoteRefusal.None));
            Assert.That(emotes.ChangedView, Is.True);
            Assert.That(emotes.GetComponent<ViewRig>().Mode, Is.EqualTo(ViewMode.ThirdPerson));
        }

        [Test]
        public void AnUnknownIdIsRefused()
        {
            var emotes = Player(Emote.On);

            LogAssert.Expect(LogType.Log, EmoteLog.Refused("moonwalk", EmoteRefusal.UnknownId));
            Assert.That(emotes.Play("moonwalk"), Is.False);
            Assert.That(emotes.Play(null), Is.False);
            Assert.That(emotes.Play("WAVE"), Is.False, "ids are matched exactly");

            Assert.That(emotes.LastRefusal, Is.EqualTo(EmoteRefusal.UnknownId));
            Assert.That(emotes.IsPlaying, Is.False);
            Assert.That(emotes.GetComponent<ViewRig>().Mode, Is.EqualTo(ViewMode.FirstPerson));
        }

        [Test]
        public void WalkingAwayStopsTheEmoteAndGivesTheFirstPersonViewBack()
        {
            var emotes = Player(Emote.On);
            Assert.That(emotes.Play(_wave), Is.True);

            emotes.transform.position += new Vector3(0f, 0.5f, 0f);
            emotes.Tick(_now);
            Assert.That(emotes.IsPlaying, Is.True, "falling or a step up is not walking");

            emotes.transform.position += new Vector3(0.04f, 0f, 0f);
            emotes.Tick(_now);
            Assert.That(emotes.IsPlaying, Is.True, "a nudge under the tolerance keeps it going");

            emotes.transform.position += new Vector3(0.5f, 0f, 0f);
            emotes.Tick(_now);
            Assert.That(emotes.IsPlaying, Is.False);
            Assert.That(emotes.LastStop, Is.EqualTo(EmoteStopReason.Moved));
            Assert.That(emotes.GetComponent<ViewRig>().Mode, Is.EqualTo(ViewMode.FirstPerson));
        }

        [Test]
        public void AnEmoteThatAllowsMovingKeepsGoingWhileWalking()
        {
            var emotes = Player(Emote.On);
            Assert.That(emotes.Play(_stand), Is.True);

            emotes.transform.position += new Vector3(3f, 0f, 0f);
            emotes.Tick(_now);

            Assert.That(emotes.IsPlaying, Is.True);
        }

        [Test]
        public void ShootingStopsOnlyTheShootersEmote()
        {
            var emotes = Player(Emote.On);
            var other = Track(new GameObject("Other"));
            Assert.That(emotes.Play(_stand), Is.True);

            CombatEvents.RaiseShotFired(other, Vector3.zero, Vector3.forward, "rifle");
            Assert.That(emotes.IsPlaying, Is.True);

            var gun = Track(new GameObject("Gun"));
            gun.transform.SetParent(emotes.transform, false);
            CombatEvents.RaiseShotFired(gun, Vector3.zero, Vector3.forward, "rifle");

            Assert.That(emotes.IsPlaying, Is.False);
            Assert.That(emotes.LastStop, Is.EqualTo(EmoteStopReason.Fired));
            Assert.That(emotes.GetComponent<ViewRig>().Mode, Is.EqualTo(ViewMode.FirstPerson));
        }

        [Test]
        public void BeingKilledStopsTheEmoteAndRefusesTheNext()
        {
            var emotes = Player(Emote.On);
            Assert.That(emotes.Play(_stand), Is.True);

            emotes.GetComponent<Damageable>().Kill();

            Assert.That(emotes.IsPlaying, Is.False);
            Assert.That(emotes.LastStop, Is.EqualTo(EmoteStopReason.Died));
            Assert.That(emotes.GetComponent<ViewRig>().Mode, Is.EqualTo(ViewMode.FirstPerson));

            LogAssert.Expect(LogType.Log, EmoteLog.Refused(_wave, EmoteRefusal.Dead));
            Assert.That(emotes.Play(_wave), Is.False);
            Assert.That(emotes.LastRefusal, Is.EqualTo(EmoteRefusal.Dead));
        }

        [Test]
        public void AOneShotEmoteEndsAfterItsLengthAndALoopingOneDoesNot()
        {
            var emotes = Player(Emote.On);
            Assert.That(emotes.Play(_wave), Is.True);

            _now = 1.9f;
            emotes.Tick(_now);
            Assert.That(emotes.IsPlaying, Is.True);

            _now = 2f;
            LogAssert.Expect(LogType.Log, EmoteLog.Stopped(_wave, EmoteStopReason.Finished, 2f));
            emotes.Tick(_now);
            Assert.That(emotes.IsPlaying, Is.False);
            Assert.That(emotes.LastStop, Is.EqualTo(EmoteStopReason.Finished));

            Assert.That(emotes.Play(_dance), Is.True);
            _now = 60f;
            emotes.Tick(_now);
            Assert.That(emotes.IsPlaying, Is.True, "a looping emote runs until something stops it");
        }

        [Test]
        public void PlayingAnotherEmoteReplacesTheCurrentOne()
        {
            var emotes = Player(Emote.On);
            Assert.That(emotes.Play(_wave), Is.True);

            LogAssert.Expect(LogType.Log, EmoteLog.Stopped(_wave, EmoteStopReason.Replaced, 0f));
            Assert.That(emotes.Play(_dance), Is.True);

            Assert.That(emotes.Current.Id, Is.EqualTo(_dance));
            Assert.That(emotes.GetComponent<ViewRig>().Mode, Is.EqualTo(ViewMode.ThirdPerson));

            emotes.Stop();
            Assert.That(emotes.LastStop, Is.EqualTo(EmoteStopReason.Cancelled));
            Assert.That(emotes.GetComponent<ViewRig>().Mode, Is.EqualTo(ViewMode.FirstPerson));
        }

        [Test]
        public void APlayerInThirdPersonStaysThere()
        {
            var emotes = Player(Emote.On);
            var rig = emotes.GetComponent<ViewRig>();
            rig.Mode = ViewMode.ThirdPerson;

            Assert.That(emotes.Play(_wave), Is.True);
            Assert.That(emotes.ChangedView, Is.False);
            emotes.Stop();

            Assert.That(rig.Mode, Is.EqualTo(ViewMode.ThirdPerson));
        }

        [Test]
        public void TurningTheRuleOffStopsTheEmoteAndTakesTheWheelDown()
        {
            var emotes = Player(Emote.On);
            Assert.That(emotes.OpenWheel(), Is.True);
            Assert.That(emotes.Play(_stand), Is.True);

            emotes.Configure(EmoteRules(Emote.Off));

            Assert.That(emotes.IsPlaying, Is.False);
            Assert.That(emotes.LastStop, Is.EqualTo(EmoteStopReason.RuleOff));
            Assert.That(emotes.IsWheelOpen, Is.False);
            Assert.That(GameSignals.Emotes.HasValue, Is.False);
            Assert.That(emotes.GetComponent<ViewRig>().Mode, Is.EqualTo(ViewMode.FirstPerson));
        }

        [Test]
        public void TheInstallerAddsTheControllerOnlyWhenTheRuleIsOn()
        {
            var installer = new EmoteActionInstaller();
            var off = Body("Off");
            installer.Install(off, EmoteRules(Emote.Off));
            Assert.That(off.GetComponent<EmoteController>(), Is.Null);

            var on = Body("On");
            installer.Install(on, EmoteRules(Emote.On));
            var controller = on.GetComponent<EmoteController>();
            Assert.That(controller, Is.Not.Null);
            Assert.That(controller.UsesEmotes, Is.True);

            installer.Install(on, EmoteRules(Emote.Off));
            Assert.That(on.GetComponent<EmoteController>(), Is.SameAs(controller), "the controller is kept so it can be turned back on");
            Assert.That(controller.UsesEmotes, Is.False);

            Assert.That(installer.Order, Is.EqualTo(EmoteActionInstaller.InstallOrder));
            Assert.Throws<ArgumentNullException>(() => installer.Install(null, EmoteRules(Emote.On)));
            Assert.Throws<ArgumentNullException>(() => installer.Install(on, null));
        }

        [Test]
        public void TheInstallerIsNotRegisteredWithThePlayerActions()
        {
            foreach (var installer in PlayerActions.Installers)
            {
                Assert.That(installer, Is.Not.InstanceOf<EmoteActionInstaller>());
            }
        }

        [Test]
        public void TheEmoteListMustHaveUniqueIds()
        {
            var emotes = Body("Body").AddComponent<EmoteController>();

            Assert.Throws<ArgumentNullException>(() => emotes.ApplyEmotes(null));
            Assert.Throws<ArgumentException>(() => emotes.ApplyEmotes(new[] { new EmoteSpec(_wave), new EmoteSpec(_wave) }));
        }

        [Test]
        public void TheCatalogFindsAnimationsById()
        {
            var catalog = Track(ScriptableObject.CreateInstance<EmoteCatalog>());
            var clip = Track(new AnimationClip());
            catalog.Add(_wave, clip);

            Assert.That(catalog.Find(_wave), Is.SameAs(clip));
            Assert.That(catalog.Find(_dance), Is.Null);
            Assert.That(catalog.Find(null), Is.Null);
            Assert.Throws<ArgumentException>(() => catalog.Add(" ", clip));
            Assert.That(EmoteCatalog.ResourcePath, Is.EqualTo("Presentation/EmoteCatalog"));
        }

        [Test]
        public void TheAnimatorRefusesAnEmoteWithoutARig()
        {
            var body = Body("Body");
            var animator = body.AddComponent<CharacterAnimator>();
            var clip = Track(new AnimationClip());

            Assert.Throws<ArgumentNullException>(() => animator.PlayEmote(null));
            Assert.That(animator.PlayEmote(clip), Is.False);
            Assert.That(animator.IsPlayingEmote, Is.False);
            Assert.That(animator.EmoteClip, Is.Null);
            animator.StopEmote();
        }

        [Test]
        public void AnEmoteWithoutAnAnimationStillPlays()
        {
            var emotes = Player(Emote.On);
            var catalog = Track(ScriptableObject.CreateInstance<EmoteCatalog>());
            catalog.Add(_dance, Track(new AnimationClip()));
            emotes.Catalog = catalog;

            LogAssert.Expect(LogType.Log, EmoteLog.NoAnimation(_wave));
            Assert.That(emotes.Play(_wave), Is.True);
            Assert.That(emotes.IsPlaying, Is.True);
        }

        [Test]
        public void AVehicleCameraBorrowingTheRigDuringAnEmoteReturnsToFirstPerson()
        {
            var emotes = Player(Emote.On);
            var rig = emotes.GetComponent<ViewRig>();
            LogAssert.Expect(LogType.Log, EmoteLog.Played(_emotes[0], hasAnimation: false));
            Assert.That(emotes.Play(_wave), Is.True);

            var camera = new VehicleCamera(rig);
            camera.Begin(VehicleCameraView.Chase);
            Assert.That(rig.Mode, Is.EqualTo(ViewMode.ThirdPerson));

            camera.Restore();
            Assert.That(rig.Mode, Is.EqualTo(ViewMode.FirstPerson));
        }

        private EmoteController Player(Emote rule)
        {
            var go = Body("Player");
            go.AddComponent<Damageable>().Initialize("Player", Damageable.DefaultMaxHealth);
            go.AddComponent<ViewRig>();
            var emotes = go.AddComponent<EmoteController>();
            emotes.ApplyEmotes(_emotes);
            emotes.Catalog = null;
            emotes.Clock = () => _now;
            emotes.Configure(EmoteRules(rule));
            return emotes;
        }

        private GameObject Body(string name)
        {
            return Track(new GameObject(name));
        }

        private T Track<T>(T created)
            where T : Object
        {
            _objects.Add(created);
            return created;
        }
    }
}
