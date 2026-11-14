using System;
using System.Linq;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // 度合いの計算と遮蔽点の判断は Unity 抜きで回せる Core のテスト (SuppressionMeterTests・SuppressedCoverBehaviorTests) に置き、ここは部品のつなぎだけを見る。
    public sealed class SuppressionTests
    {
        // 半径 2m、かすめて 0.3、待ち 0.5 秒、毎秒 1 抜ける、0.5 で制圧・0.2 で解ける。
        private static readonly SuppressionSpec _spec = new SuppressionSpec(2f, 0.3f, 0.5f, 1f, 0.5f, 0.2f, 6f, 0.5f, 0.8f);

        private GameObject _target;
        private SuppressionReceiver _receiver;
        private readonly System.Collections.Generic.List<Object> _created = new System.Collections.Generic.List<Object>();
        private readonly System.Collections.Generic.List<InputAction> _actions = new System.Collections.Generic.List<InputAction>();

        [SetUp]
        public void SetUp()
        {
            _target = Track(new GameObject("Target"));
            _target.transform.position = new Vector3(0f, 1f, 10f);
            _receiver = _target.AddComponent<SuppressionReceiver>();
        }

        [TearDown]
        public void TearDown()
        {
            EnemySuppression.ConfigureAll(Suppression.Off, SuppressionSpec.Default);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
            foreach (var action in _actions)
            {
                action.Dispose();
            }

            _actions.Clear();
        }

        [Test]
        public void OffTakesNoNearMisses()
        {
            _receiver.Configure(Suppression.Off, _spec);

            Assert.That(Hitscan.NearMissTargets, Does.Not.Contain(_receiver));
            Assert.That(Hitscan.ReportNearMisses(Vector3.up, Vector3.forward, 50f, null, null), Is.Zero);
            Assert.That(_receiver.Level, Is.EqualTo(0f));
        }

        [Test]
        public void OnCountsAShotPassingBeside()
        {
            LogAssert.Expect(LogType.Log, "[Suppression] id=Target rule=On");
            _receiver.Configure(Suppression.On, _spec);
            HitscanNearMiss seen = default;
            _receiver.NearMissed += (_, nearMiss) => seen = nearMiss;

            LogAssert.Expect(LogType.Log, "[Suppression] id=Target near-miss distance=1.00 level=0.15 suppressed=false count=1");
            var count = Hitscan.ReportNearMisses(new Vector3(1f, 1f, 0f), Vector3.forward, 50f, null, null);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(_receiver.Level, Is.EqualTo(0.15f).Within(1e-5f));
            Assert.That(seen.Distance, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(seen.ClosestPoint, Is.EqualTo(new Vector3(1f, 1f, 10f)));
        }

        [Test]
        public void TheShooterTheStruckAndThoseBehindAreNotNearMisses()
        {
            _receiver.Configure(Suppression.On, _spec);
            var body = Track(GameObject.CreatePrimitive(PrimitiveType.Cube));
            body.transform.SetParent(_target.transform, false);

            Assert.That(Hitscan.ReportNearMisses(new Vector3(1f, 1f, 0f), Vector3.forward, 50f, _target.transform, null), Is.Zero, "the shooter");
            Assert.That(Hitscan.ReportNearMisses(new Vector3(1f, 1f, 0f), Vector3.forward, 50f, null, body.GetComponent<Collider>()), Is.Zero, "a hit");
            Assert.That(Hitscan.ReportNearMisses(new Vector3(1f, 1f, 20f), Vector3.forward, 50f, null, null), Is.Zero, "behind the gun");
            Assert.That(Hitscan.ReportNearMisses(new Vector3(5f, 1f, 0f), Vector3.forward, 50f, null, null), Is.Zero, "too far");
            Assert.That(_receiver.Level, Is.EqualTo(0f));
        }

        [Test]
        public void NearMissesBuildToSuppressedAndDrainToCleared()
        {
            _receiver.Configure(Suppression.On, _spec);
            var suppressed = new System.Collections.Generic.List<bool>();
            _receiver.SuppressedChanged += (_, value) => suppressed.Add(value);

            LogAssert.Expect(LogType.Log, "[Suppression] id=Target suppressed=true level=0.60");
            Hitscan.ReportNearMisses(new Vector3(0f, 1f, 0f), Vector3.forward, 50f, null, null);
            Hitscan.ReportNearMisses(new Vector3(0f, 1f, 0f), Vector3.forward, 50f, null, null);
            Assert.That(_receiver.IsSuppressed, Is.True);

            LogAssert.Expect(LogType.Log, "[Suppression] id=Target suppressed=false level=0.00");
            LogAssert.Expect(LogType.Log, "[Suppression] id=Target cleared");
            _receiver.Tick(5f);

            Assert.That(_receiver.Level, Is.EqualTo(0f));
            Assert.That(suppressed, Is.EqualTo(new[] { true, false }));
        }

        [Test]
        public void TurningOffDropsTheLevelAndUnregisters()
        {
            _receiver.Configure(Suppression.On, _spec);
            Hitscan.ReportNearMisses(new Vector3(0f, 1f, 0f), Vector3.forward, 50f, null, null);
            Assert.That(Hitscan.NearMissTargets, Does.Contain(_receiver));

            _receiver.Configure(Suppression.Off);

            Assert.That(_receiver.Level, Is.EqualTo(0f));
            Assert.That(Hitscan.NearMissTargets, Does.Not.Contain(_receiver));
            Assert.That(_receiver.Spec, Is.SameAs(_spec), "turning off keeps the tuning");
        }

        [Test]
        public void ADestroyedTargetIsDroppedFromTheList()
        {
            _receiver.Configure(Suppression.On, _spec);
            Object.DestroyImmediate(_target);

            Assert.That(Hitscan.ReportNearMisses(new Vector3(0f, 1f, 0f), Vector3.forward, 50f, null, null), Is.Zero);
            Assert.That(Hitscan.NearMissTargets.Any(t => t is Object o && o == null), Is.False);
        }

        [Test]
        public void ConfigureRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => _receiver.Configure(Suppression.On, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _receiver.Configure((Suppression)99, _spec));
            Assert.Throws<ArgumentNullException>(() => Hitscan.RegisterNearMissTarget(null));
            Assert.Throws<ArgumentException>(() => Hitscan.ReportNearMisses(Vector3.zero, Vector3.zero, 1f, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => Hitscan.ReportNearMisses(Vector3.zero, Vector3.forward, -1f, null, null));
        }

        [Test]
        public void TheInspectorTuningBuildsTheSpec()
        {
            var spec = _receiver.CreateSpec();

            Assert.That(spec.NearMissRadius, Is.EqualTo(SuppressionSpec.DefaultNearMissRadius));
            Assert.That(spec.MaxSpreadDegrees, Is.EqualTo(SuppressionSpec.DefaultMaxSpreadDegrees));

            _receiver.Configure(Suppression.On, _spec);
            Assert.That(_receiver.CreateSpec().NearMissRadius, Is.EqualTo(_spec.NearMissRadius), "configured values are written back");
        }

        [Test]
        public void TheWeaponModifierFollowsTheLevel()
        {
            var modifier = new SuppressionWeaponModifier(_receiver);
            Assert.That(modifier.ModifyConeDegrees(null, 2f), Is.EqualTo(2f), "off adds nothing");

            _receiver.Configure(Suppression.On, _spec);
            Hitscan.ReportNearMisses(new Vector3(0f, 1f, 0f), Vector3.forward, 50f, null, null);

            Assert.That(modifier.ModifyConeDegrees(null, 2f), Is.EqualTo(2f + (0.3f * 6f)).Within(1e-5f));
            Assert.That(modifier.ModifySpreadFraction(0.1f), Is.EqualTo(0.1f + (0.3f * 0.5f)).Within(1e-5f));
            Assert.Throws<ArgumentNullException>(() => _ = new SuppressionWeaponModifier(null));
        }

        [Test]
        public void PlayerSuppressionWidensTheAimAndShowsTheVignetteOnlyWhenOn()
        {
            var player = Track(new GameObject("Player"));
            var camera = Track(new GameObject("Camera", typeof(Camera)));
            camera.transform.SetParent(player.transform, false);
            var weapon = InitializeWeapon(camera);
            var spec = weapon.Weapon.Spec;
            var baseCone = weapon.CurrentConeDegrees(spec);

            var suppression = player.AddComponent<PlayerSuppression>();
            suppression.Configure(Suppression.On, _spec);
            var receiver = suppression.Receiver;
            var vignette = player.GetComponent<SuppressionVignette>();

            Assert.That(weapon.Modifiers, Does.Contain(suppression.Modifier));
            Assert.That(vignette, Is.Not.Null, "the default presenter");
            Assert.That(suppression.Presenter, Is.SameAs(vignette));

            receiver.ReceiveNearMiss(new HitscanNearMiss(receiver, null, Vector3.zero, Vector3.forward, Vector3.zero, 0f));
            Assert.That(weapon.CurrentConeDegrees(spec), Is.EqualTo(baseCone + (0.3f * 6f)).Within(1e-4f));
            Assert.That(weapon.SpreadFraction, Is.EqualTo(0.3f * 0.5f).Within(1e-5f));

            suppression.Configure(Suppression.Off, _spec);
            Assert.That(weapon.Modifiers, Does.Not.Contain(suppression.Modifier));
            Assert.That(weapon.CurrentConeDegrees(spec), Is.EqualTo(baseCone));
            Assert.That(vignette.Alpha, Is.EqualTo(0f));
        }

        [Test]
        public void ThePresenterCanBeReplaced()
        {
            var player = Track(new GameObject("Player"));
            var suppression = player.AddComponent<PlayerSuppression>();
            var custom = new RecordingPresenter();
            suppression.Presenter = custom;

            suppression.Configure(Suppression.On, _spec);

            Assert.That(suppression.Presenter, Is.SameAs(custom));
            Assert.That(player.GetComponent<SuppressionVignette>(), Is.Null, "the default is only added when nothing is set");
            Assert.That(custom.LastSpec, Is.SameAs(_spec));

            suppression.Presenter = null;
            Assert.That(custom.LastLevel, Is.EqualTo(0f), "the replaced presenter is cleared");
        }

        [Test]
        public void TheVignetteScalesWithTheLevelAndFallsBackToAPlaceholder()
        {
            var vignette = Track(new GameObject("Vignette")).AddComponent<SuppressionVignette>();

            vignette.Present(0.5f, _spec);
            Assert.That(vignette.Alpha, Is.EqualTo(0.4f).Within(1e-5f));

            vignette.Present(2f, _spec);
            Assert.That(vignette.Alpha, Is.EqualTo(0.8f).Within(1e-5f));

            vignette.Present(float.NaN, _spec);
            Assert.That(vignette.Alpha, Is.EqualTo(0f));

            if (SuppressionCatalog.Load() == null)
            {
                Assert.That(vignette.UsesPlaceholder, Is.True);
            }

            Assert.That(vignette.Texture, Is.Not.Null);

            // Edit Mode では OnDestroy が走らず、コンポーネントが作った仮の模様が残るので、ここで消す。
            if (vignette.UsesPlaceholder)
            {
                Track(vignette.Texture);
            }

            var placeholder = SuppressionVignette.CreatePlaceholder();
            try
            {
                var half = SuppressionVignette.PlaceholderSize / 2;
                Assert.That(placeholder.GetPixel(half, half).a, Is.EqualTo(0f).Within(0.01f), "clear in the middle");
                Assert.That(placeholder.GetPixel(0, 0).a, Is.EqualTo(1f).Within(0.01f), "dark at the corners");
            }
            finally
            {
                Object.DestroyImmediate(placeholder);
            }
        }

        [Test]
        public void EnemySuppressionFeedsThePerceptionAndAddsTheBehaviorOnlyWhenOn()
        {
            var enemy = CreateEnemy("Enemy");
            var agent = enemy.GetComponent<EnemyAgent>();
            Assert.That(agent.TryInitialize(), Is.True);

            var suppression = enemy.AddComponent<EnemySuppression>();
            suppression.Configure(Suppression.On, _spec);

            Assert.That(agent.PerceptionContributors, Does.Contain(suppression.Perception));
            Assert.That(agent.Brain.Behaviors, Does.Contain(suppression.Behavior));

            suppression.Receiver.ReceiveNearMiss(new HitscanNearMiss(suppression.Receiver, null, Vector3.zero, Vector3.forward, Vector3.zero, 0f));
            var perception = agent.Perceive();
            Assert.That(perception.SuppressionLevel, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(perception.IsSuppressed, Is.False);

            suppression.Configure(Suppression.Off, _spec);
            Assert.That(agent.PerceptionContributors, Is.Empty);
            Assert.That(agent.Brain.Behaviors, Does.Not.Contain(suppression.Behavior));
            Assert.That(agent.Perceive().SuppressionLevel, Is.EqualTo(0f));
        }

        [Test]
        public void ConfigureAllReachesEnemiesInitializedLater()
        {
            var early = CreateEnemy("Early");
            Assert.That(early.GetComponent<EnemyAgent>().TryInitialize(), Is.True);

            EnemySuppression.ConfigureAll(Suppression.On, _spec);
            Assert.That(early.GetComponent<EnemySuppression>(), Is.Not.Null);

            var late = CreateEnemy("Late");
            Assert.That(late.GetComponent<EnemySuppression>(), Is.Null);
            Assert.That(late.GetComponent<EnemyAgent>().TryInitialize(), Is.True);

            var lateSuppression = late.GetComponent<EnemySuppression>();
            Assert.That(lateSuppression, Is.Not.Null);
            Assert.That(lateSuppression.Receiver.Rule, Is.EqualTo(Suppression.On));
            Assert.That(late.GetComponent<EnemyAgent>().Brain.Behaviors, Does.Contain(lateSuppression.Behavior));

            EnemySuppression.ConfigureAll(Suppression.Off, _spec);
            Assert.That(early.GetComponent<EnemySuppression>().Receiver.Rule, Is.EqualTo(Suppression.Off));

            var afterOff = CreateEnemy("AfterOff");
            Assert.That(afterOff.GetComponent<EnemyAgent>().TryInitialize(), Is.True);
            Assert.That(afterOff.GetComponent<EnemySuppression>(), Is.Null, "off adds nothing to new enemies");
        }

        [Test]
        public void TheInstallerAddsNothingWhenOffAndConfiguresWhenOn()
        {
            var player = Track(new GameObject("Player"));
            var installer = new SuppressionActionInstaller(_spec);

            installer.Install(player, GameRules.Default);
            Assert.That(player.GetComponent<PlayerSuppression>(), Is.Null);
            Assert.That(player.GetComponent<SuppressionReceiver>(), Is.Null);

            installer.Install(player, GameRules.Default.WithActions(ActionRules.Default.WithSuppression(Suppression.On)));
            var suppression = player.GetComponent<PlayerSuppression>();
            Assert.That(suppression, Is.Not.Null);
            Assert.That(suppression.Receiver.IsActive, Is.True);
            Assert.That(suppression.Receiver.Spec, Is.SameAs(_spec));

            installer.Install(player, GameRules.Default);
            Assert.That(suppression.Receiver.Rule, Is.EqualTo(Suppression.Off), "a later Off turns the earlier On back off");
            Assert.That(player.GetComponents<PlayerSuppression>(), Has.Length.EqualTo(1));
            Assert.Throws<ArgumentNullException>(() => _ = new SuppressionActionInstaller(null));
        }

        [Test]
        public void TheInstallerIsBuiltIn()
        {
            Assert.That(PlayerActions.Installers.OfType<SuppressionActionInstaller>().Count(), Is.EqualTo(1));
        }



        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        private GameObject CreateEnemy(string name)
        {
            var rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            rifle.name = "Rifle";
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());

            var host = Track(GameObject.CreatePrimitive(PrimitiveType.Capsule));
            host.name = name;
            host.AddComponent<EnemyAgent>().WeaponDefinition = rifle;
            return host;
        }

        private WeaponController InitializeWeapon(GameObject camera)
        {
            var rifle = Track(ScriptableObject.CreateInstance<WeaponDefinition>());
            rifle.name = "Rifle";
            WeaponAssets.Configure(rifle, TestWeapons.RifleSpec());
            var attack = new InputAction("Attack", InputActionType.Button);
            var reload = new InputAction("Reload", InputActionType.Button);
            _actions.Add(attack);
            _actions.Add(reload);
            var weapon = camera.AddComponent<WeaponController>();
            weapon.Initialize(camera.transform, attack, reload, rifle);
            return weapon;
        }

        private sealed class RecordingPresenter : ISuppressionPresenter
        {
            public float LastLevel { get; private set; } = -1f;

            public SuppressionSpec LastSpec { get; private set; }

            public void Present(float level, SuppressionSpec spec)
            {
                LastLevel = level;
                LastSpec = spec;
            }
        }
    }
}
