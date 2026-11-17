using System.Collections.Generic;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // 物理の Step は走らないので、落ちたところは Land を直接呼んで確かめる。敵が聞くところは EnemyStealthTests と Core のテストに任せる。
    public sealed class DistractionThrowTests
    {
        private static readonly GameRules _on = GameRules.Default.WithActions(ActionRules.Default.WithAbilities(Abilities.On));

        private readonly List<Object> _created = new List<Object>();
        private GameObject _player;
        private AbilityHost _host;
        private DistractionThrowDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("DistractionThrowTests.Player");
            _player.transform.SetPositionAndRotation(new Vector3(0f, 1f, 0f), Quaternion.Euler(0f, 90f, 0f));
            _created.Add(_player);
            _host = _player.AddComponent<AbilityHost>();
            _host.Configure(_on);

            _definition = ScriptableObject.CreateInstance<DistractionThrowDefinition>();
            _definition.name = "DistractionThrow";
            _definition.Id = "distraction";
            _definition.MaxCharges = 1;
            _definition.RechargeSeconds = 10f;
            _definition.CooldownSeconds = 0f;
            _definition.WindupSeconds = 0.1f;
            _definition.ThrowSpeed = 10f;
            _definition.ThrowLiftSpeed = 2f;
            _definition.NoiseRadius = 8f;
            _created.Add(_definition);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var thrown in Thrown())
            {
                Object.DestroyImmediate(thrown.gameObject);
            }

            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheObjectIsThrownLikeAGrenadeOnceTheWindupIsOver()
        {
            _host.Equip(0, _definition);
            Assert.That(_host.TryUse(0), Is.True);

            _host.Step(PlayerCommands.None, 0.05f);
            Assert.That(Thrown(), Is.Empty, "still winding up");

            LogAssert.Expect(LogType.Log, new Regex(@"^\[Distraction\] thrown at=\(0\.7, 1\.0, 0\.0\) speed="));
            _host.Step(PlayerCommands.None, 0.1f);

            Assert.That(Thrown(), Has.Length.EqualTo(1));
            var thrown = Thrown()[0];
            Assert.That(thrown.Thrower, Is.SameAs(_player));
            Assert.That(thrown.Spec.NoiseRadius, Is.EqualTo(8f));
            var expected = GrenadeThrower.LaunchVelocity(Vector3.right, 10f, 2f);
            Assert.That(Vector3.Distance(thrown.GetComponent<Rigidbody>().linearVelocity, expected), Is.LessThan(1e-3f));
            Assert.That(_host.IsRunning(0), Is.False, "throwing ends the ability");
            Assert.That(_host.Slot(0).Charges, Is.Zero);
        }

        [Test]
        public void ItSoundsOnlyTheFirstTimeItLands()
        {
            var thrown = Spawn();
            var spot = new Vector3(2f, 0f, 3f);
            LogAssert.Expect(LogType.Log, ThrownDistraction.FormatLandedLog(spot, 8f, 0));
            var landed = 0;
            thrown.Landed += _ => landed++;

            Assert.That(thrown.Land(spot), Is.True);
            Assert.That(thrown.Land(spot + Vector3.up), Is.False, "bouncing does not call the guards again");

            Assert.That(landed, Is.EqualTo(1));
            Assert.That(thrown.HasLanded, Is.True);
            Assert.That(thrown.LandedAt, Is.EqualTo(spot));
        }

        [Test]
        public void ItIsRemovedAfterLingeringOrWhenItNeverLands()
        {
            var landed = Spawn();
            landed.Land(Vector3.zero);
            Assert.That(landed.Tick(DistractionSpec.DefaultLingerSeconds - 0.5f), Is.False);
            Assert.That(landed.Tick(1f), Is.True);
            Assert.That(landed == null, Is.True);

            var lost = Spawn();
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Distraction\] lost at="));
            Assert.That(lost.Tick(DistractionSpec.DefaultMaxFlightSeconds + 0.1f), Is.True);
            Assert.That(lost == null, Is.True);
        }

        [Test]
        public void InvalidValuesNameTheAbility()
        {
            _definition.NoiseRadius = 0f;

            var e = Assert.Throws<AbilityDefinitionException>(() => _definition.CreateAbility());
            Assert.That(e.Message, Does.Contain("distraction"));
        }

        [Test]
        public void LogsHaveAStableShape()
        {
            Assert.That(
                ThrownDistraction.FormatThrownLog(new Vector3(1f, 2f, 3f), new Vector3(0f, 0f, 4f)),
                Is.EqualTo("[Distraction] thrown at=(1.0, 2.0, 3.0) speed=4.0"));
            Assert.That(
                ThrownDistraction.FormatLandedLog(new Vector3(-1f, 0f, 2.5f), 15f, 2),
                Is.EqualTo("[Distraction] landed at=(-1.0, 0.0, 2.5) radius=15.0 reacted=2"));
            Assert.That(ThrownDistraction.FormatLandedLog(Vector3.zero, 1f, 0), Does.StartWith(ThrownDistraction.LandedLogPrefix));
        }

        private static ThrownDistraction[] Thrown()
        {
            return Object.FindObjectsByType<ThrownDistraction>(FindObjectsInactive.Exclude);
        }

        private ThrownDistraction Spawn()
        {
            return ThrownDistraction.Spawn(_player, Vector3.zero, Vector3.zero, _definition.ToDistractionSpec(), null, null);
        }
    }
}
