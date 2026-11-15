using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    // Edit Mode では Awake・Update が走らないので、参照は Initialize / Configure で渡し、技の時間は Tick で進める。寸法は MeleeControllerTests と同じ。
    public sealed class ContextMeleeActionTests
    {
        private const float _height = 2f;
        private const float _radius = 0.5f;
        private const int _playerHealth = 100;
        private const int _enemyHealth = 1000;
        private const int _legShot = 25;
        private static readonly Vector3 _bodyPosition = new Vector3(0f, 1f, 0f);
        private static readonly Vector3 _enemyPosition = new Vector3(0.6f, 1f, 1.6f);

        private GameObject _body;
        private GameObject _floor;
        private GameObject _enemyHost;
        private PlayerController _player;
        private WeaponController _weapon;
        private MeleeController _melee;
        private ContextMeleeAction _context;
        private Damageable _self;
        private EnemyAgent _agent;
        private Damageable _enemy;
        private StaggerReaction _reaction;
        private WeaponDefinition _rifle;
        private Stagger _savedRule;
        private InputAction _move;
        private InputAction _sprint;
        private InputAction _crouch;
        private InputAction _jump;
        private InputAction _attack;
        private InputAction _reload;
        private InputAction _meleeAction;

        private static GameRules Rules(Melee melee)
        {
            return GameRules.Default.WithActions(ActionRules.Default.WithMelee(melee));
        }

        [SetUp]
        public void SetUp()
        {
            _savedRule = StaggerReaction.ActiveRule;
            StaggerReaction.ActiveRule = Fps.Core.Stagger.On;

            _floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _floor.name = "Floor";
            _floor.transform.position = new Vector3(0f, -0.5f, 0f);
            _floor.transform.localScale = new Vector3(20f, 1f, 20f);

            _body = new GameObject("Body");
            _body.transform.position = _bodyPosition;
            var characterController = _body.AddComponent<CharacterController>();
            characterController.height = _height;
            characterController.radius = _radius;
            characterController.center = Vector3.zero;
            characterController.skinWidth = 0.01f;
            _self = _body.AddComponent<Damageable>();
            _self.Initialize("Body", _playerHealth);

            var eye = new GameObject("Eye").transform;
            eye.SetParent(_body.transform, false);
            eye.localPosition = new Vector3(0f, 0.6f, 0f);
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.SetParent(eye, false);
            camera.fieldOfView = 60f;
            _body.AddComponent<ViewRig>().Initialize(eye, camera);

            var look = _body.AddComponent<FirstPersonLook>();
            _player = _body.AddComponent<PlayerController>();
            _move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
            _sprint = new InputAction("Sprint", InputActionType.Button);
            _crouch = new InputAction("Crouch", InputActionType.Button);
            _jump = new InputAction("Jump", InputActionType.Button);
            _player.Initialize(characterController, look, _move, _sprint, _crouch, _jump, new MotorSpec(5f, 8f, 1.2f, 20f, 1f, 2.5f));

            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());
            _attack = new InputAction("Attack", InputActionType.Button);
            _reload = new InputAction("Reload", InputActionType.Button);
            _weapon = camera.gameObject.AddComponent<WeaponController>();
            _weapon.Initialize(camera.transform, _attack, _reload, _rifle);
            _weapon.Handoff = _player.Handoff;

            _meleeAction = new InputAction("Melee", InputActionType.Button);
            _melee = _body.AddComponent<MeleeController>();
            _melee.Initialize(_player, _weapon, _meleeAction);
            _melee.Configure(Rules(Melee.On));

            _context = _body.AddComponent<ContextMeleeAction>();
            _context.Configure(Rules(Melee.On));

            _enemyHost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _enemyHost.name = "Enemy";
            _enemyHost.transform.SetPositionAndRotation(_enemyPosition, Quaternion.LookRotation(Vector3.back));
            _agent = _enemyHost.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _agent.Id = "enemy-1";
            _enemy = _enemyHost.GetComponent<Damageable>();
            _enemy.Initialize("enemy-1", _enemyHealth);
            Assert.That(_agent.TryInitialize(), Is.True);
            _reaction = _enemyHost.AddComponent<StaggerReaction>();
            Assert.That(_reaction.Bind(), Is.True);

            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            StaggerReaction.ActiveRule = _savedRule;
            _move.Dispose();
            _sprint.Dispose();
            _crouch.Dispose();
            _jump.Dispose();
            _attack.Dispose();
            _reload.Dispose();
            _meleeAction.Dispose();
            Object.DestroyImmediate(_body);
            Object.DestroyImmediate(_enemyHost);
            Object.DestroyImmediate(_floor);
            Object.DestroyImmediate(_rifle);
        }

        [Test]
        public void ConfiguringOnRegistersTheFinisherFromTheMeleeSpec()
        {
            Assert.That(_context.enabled, Is.True);
            var finisher = _context.Selector.Find(ContextMeleeMove.FinisherId);
            Assert.That(finisher, Is.Not.Null);
            Assert.That(finisher.HealthReward, Is.EqualTo(_melee.Spec.FinisherHealth));
            Assert.That(finisher.AmmoReward, Is.EqualTo(_melee.Spec.FinisherAmmo));

            _melee.Spec = new MeleeSpec(40, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 0.5f, 7, 3, false);
            Stagger();
            Assert.That(_melee.TryMelee(), Is.True);
            Assert.That(_context.Run.Move.AmmoReward, Is.EqualTo(3), "a replaced spec rebuilds the finisher");

            _context.IncludeFinisher = false;
            Assert.That(_context.Selector.Find(ContextMeleeMove.FinisherId), Is.Null);
        }

        [Test]
        public void ACalmEnemyGetsThePlainMelee()
        {
            LogAssert.Expect(LogType.Log, new Regex(@"^\[ContextMelee\] none id=Body target=enemy-1 conditions=\S+$"));
            LogAssert.Expect(LogType.Log, "[Melee] swing id=Body");

            Assert.That(_melee.TryMelee(), Is.True);
            Assert.That(_context.IsPerforming, Is.False);
            Assert.That(_melee.IsSwinging, Is.True);
        }

        [Test]
        public void ObservingSeesTheStaggerAndTheUnawareEnemy()
        {
            var calm = _context.Observe(_enemy);
            Assert.That(calm & ContextMeleeConditions.TargetStaggered, Is.EqualTo(ContextMeleeConditions.None));
            Assert.That(calm & ContextMeleeConditions.TargetUnaware, Is.EqualTo(ContextMeleeConditions.TargetUnaware), "a fresh enemy is idle");
            Assert.That(calm & ContextMeleeConditions.TargetBehind, Is.EqualTo(ContextMeleeConditions.None), "the enemy faces the player");

            Stagger();
            Assert.That(_context.Observe(_enemy) & ContextMeleeConditions.TargetStaggered, Is.EqualTo(ContextMeleeConditions.TargetStaggered));

            _enemyHost.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Assert.That(_context.Observe(_enemy) & ContextMeleeConditions.TargetBehind, Is.EqualTo(ContextMeleeConditions.TargetBehind));
        }

        [Test]
        public void AStaggeredEnemyIsFinishedAndThePlayerGainsHealthAndAmmo()
        {
            _self.TakeDamage(40);
            MakeAmmoRoom();
            var room = _weapon.Weapon.ReserveAmmoRoom;
            Assert.That(room, Is.GreaterThan(0), "the reload should have spent reserve ammo");
            var expectedAmmo = Math.Min(_melee.Spec.FinisherAmmo, room);
            var reserveBefore = _weapon.Weapon.ReserveAmmo;

            Stagger();
            LogAssert.Expect(LogType.Log, "[ContextMelee] started id=Body target=enemy-1 move=finisher");
            Assert.That(_melee.TryMelee(), Is.True);
            Assert.That(_context.IsPerforming, Is.True);
            Assert.That(_context.Target, Is.SameAs(_enemy));
            Assert.That(_melee.IsSwinging, Is.False, "the plain melee is not swung as well");

            Assert.That(_player.IsControlHandedOff, Is.True);
            Assert.That(_weapon.IsFireBlocked, Is.True);
            Assert.That(_agent.IsInterrupted, Is.True);
            Assert.That(_melee.CheckSwing(), Is.EqualTo(MeleeRefusal.Busy));
            Assert.That(_melee.TryMelee(), Is.False, "a second press during the move does nothing");

            var remaining = _enemy.Health.Current;
            LogAssert.Expect(LogType.Log, $"[ContextMelee] resolved id=Body target=enemy-1 move=finisher outcome=Kill dmg={remaining} killed=true");
            LogAssert.Expect(LogType.Log, $"[ContextMelee] reward id=Body move=finisher health={_melee.Spec.FinisherHealth} ammo={expectedAmmo}");
            _context.Tick(0.05f);
            Assert.That(_context.IsPerforming, Is.True);
            _context.Tick(_melee.Spec.FinisherSeconds + 1f);

            Assert.That(_context.IsPerforming, Is.False);
            Assert.That(_enemy.IsDead, Is.True);
            Assert.That(_self.Health.Current, Is.EqualTo(_playerHealth - 40 + _melee.Spec.FinisherHealth));
            Assert.That(_weapon.Weapon.ReserveAmmo, Is.EqualTo(reserveBefore + expectedAmmo));
            Assert.That(_player.IsControlHandedOff, Is.False);
            Assert.That(_weapon.IsFireBlocked, Is.False);
        }

        [Test]
        public void TheMoveLinesThePlayerUpFacingTheEnemy()
        {
            Stagger();
            Assert.That(_melee.TryMelee(), Is.True);
            var move = _context.Run.Move;
            _context.Tick(move.AlignSeconds);

            var offset = _enemyHost.transform.position - _body.transform.position;
            offset.y = 0f;
            Assert.That(offset.magnitude, Is.EqualTo(move.StandDistance).Within(0.05f));
            var forward = _body.transform.forward;
            forward.y = 0f;
            Assert.That(Vector3.Dot(forward.normalized, offset.normalized), Is.GreaterThan(0.99f));
        }

        [Test]
        public void BeingShotInterruptsAnInterruptibleMove()
        {
            Stagger();
            Assert.That(_melee.TryMelee(), Is.True);
            _context.Tick(0.3f);

            LogAssert.Expect(LogType.Log, "[ContextMelee] interrupted id=Body target=enemy-1 move=finisher reason=Shot");
            _self.TakeDamage(1, _body.transform.position + Vector3.back, Vector3.forward, DamageKind.Bullet);

            Assert.That(_context.IsPerforming, Is.False);
            Assert.That(_enemy.IsDead, Is.False);
            Assert.That(_player.IsControlHandedOff, Is.False);
            Assert.That(_weapon.IsFireBlocked, Is.False);
            Assert.That(_agent.InterruptReason, Is.Not.EqualTo(ContextMeleeAction.InterruptReasonPrefix + ContextMeleeMove.FinisherId));
        }

        [Test]
        public void ANonInterruptibleMoveKeepsGoingWhenShot()
        {
            _melee.Spec = new MeleeSpec(40, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 1f, 10, 0, false);
            Stagger();
            Assert.That(_melee.TryMelee(), Is.True);

            _self.TakeDamage(1, _body.transform.position + Vector3.back, Vector3.forward, DamageKind.Bullet);
            Assert.That(_context.IsPerforming, Is.True);

            _context.Tick(5f);
            Assert.That(_enemy.IsDead, Is.True);
        }

        [Test]
        public void DyingDuringTheMoveStopsItEvenWhenNotInterruptible()
        {
            _melee.Spec = new MeleeSpec(40, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 1f, 10, 0, false);
            Stagger();
            Assert.That(_melee.TryMelee(), Is.True);

            LogAssert.Expect(LogType.Log, "[ContextMelee] interrupted id=Body target=enemy-1 move=finisher reason=AttackerDown");
            _self.Kill();
            Assert.That(_context.IsPerforming, Is.False);
            Assert.That(_player.IsControlHandedOff, Is.False);
        }

        [Test]
        public void TheTargetDyingElsewhereEndsTheMoveWithoutAReward()
        {
            _self.TakeDamage(40);
            Stagger();
            Assert.That(_melee.TryMelee(), Is.True);

            LogAssert.Expect(LogType.Log, "[ContextMelee] interrupted id=Body target=enemy-1 move=finisher reason=TargetLost");
            _enemy.Kill();
            Assert.That(_context.IsPerforming, Is.False);
            Assert.That(_self.Health.Current, Is.EqualTo(_playerHealth - 40));
        }

        [Test]
        public void ThePresenterHearsTheMove()
        {
            var presenter = new RecordingPresenter();
            _context.Presenter = presenter;
            Stagger();
            _melee.TryMelee();
            Assert.That(presenter.Started, Is.EqualTo(1));
            _context.Tick(5f);
            Assert.That(presenter.Resolved, Is.EqualTo(1));
            Assert.That(presenter.LastKilled, Is.True);
            Assert.That(presenter.Interrupted, Is.Zero);
        }

        [Test]
        public void WithTheRuleOffNoMoveStarts()
        {
            _context.Configure(Rules(Melee.Off));
            Stagger();
            Assert.That(_context.enabled, Is.False);
            Assert.That(_context.TryBegin(), Is.False);
            Assert.That(_context.IsPerforming, Is.False);
        }

        [Test]
        public void TurningTheRuleOffMidMoveCancelsIt()
        {
            Stagger();
            Assert.That(_melee.TryMelee(), Is.True);

            LogAssert.Expect(LogType.Log, "[ContextMelee] interrupted id=Body target=enemy-1 move=finisher reason=Cancelled");
            _context.Configure(Rules(Melee.Off));
            Assert.That(_context.IsPerforming, Is.False);
            Assert.That(_player.IsControlHandedOff, Is.False);
        }

        [Test]
        public void TheInstallerAddsTheActionAndThePlaceholderPresenterOnce()
        {
            Object.DestroyImmediate(_context);
            var installer = new ContextMeleeActionInstaller();
            installer.Install(_body, Rules(Melee.On));
            installer.Install(_body, Rules(Melee.On));

            Assert.That(_body.GetComponents<ContextMeleeAction>(), Has.Length.EqualTo(1));
            Assert.That(_body.GetComponents<ContextMeleeCameraPresenter>(), Has.Length.EqualTo(1));
            Assert.That(_body.GetComponent<ContextMeleeAction>().enabled, Is.True);

            installer.Install(_body, Rules(Melee.Off));
            Assert.That(_body.GetComponent<ContextMeleeAction>().enabled, Is.False);
        }

        [Test]
        public void ThePlaceholderPresenterZoomsInDuringTheMoveAndBackAfter()
        {
            var presenter = _body.AddComponent<ContextMeleeCameraPresenter>();
            var camera = presenter.Camera;
            Assert.That(camera, Is.Not.Null);
            presenter.ZoomFactor = 0.5f;

            presenter.OnStarted(_body, _enemy, null);
            presenter.Step(1f);
            Assert.That(camera.fieldOfView, Is.EqualTo(30f).Within(1e-3f));

            // 技の間にほかが視野角を書いたら、それを元の値として寄せ直し、終えたらそこへ戻す。
            camera.fieldOfView = 50f;
            presenter.Step(0f);
            Assert.That(camera.fieldOfView, Is.EqualTo(25f).Within(1e-3f));

            presenter.OnResolved(_body, _enemy, null, 0, true);
            presenter.Step(1f);
            Assert.That(camera.fieldOfView, Is.EqualTo(50f).Within(1e-3f));
            Assert.That(presenter.Weight, Is.Zero);
        }

        [Test]
        public void ConfiguringOnAlsoRegistersTheTakedownFromTheMeleeSpec()
        {
            var takedown = _context.Selector.Find(ContextMeleeMove.TakedownId);
            Assert.That(takedown, Is.Not.Null);
            Assert.That(takedown.PerformSeconds, Is.EqualTo(_melee.Spec.TakedownSeconds));
            Assert.That(takedown.Interruptible, Is.EqualTo(_melee.Spec.TakedownInterruptible));
            Assert.That(takedown.WitnessAwareness, Is.EqualTo(_melee.Spec.TakedownWitnessAwareness));

            _melee.Spec = new MeleeSpec(40, 2f, 90f, 0.5f, 0.4f, 0.2f, 0.75f, 0.5f, 7, 3, false, 0.8f, false, 0.5f);
            TurnEnemyAway();
            Assert.That(_melee.TryMelee(), Is.True);
            Assert.That(_context.Run.Move.Id, Is.EqualTo(ContextMeleeMove.TakedownId));
            Assert.That(_context.Run.Move.PerformSeconds, Is.EqualTo(0.8f), "a replaced spec rebuilds the takedown");
            Assert.That(_context.Run.Move.WitnessAwareness, Is.EqualTo(0.5f));
            _context.Interrupt(ContextMeleeInterruption.Cancelled, true);

            _context.IncludeTakedown = false;
            Assert.That(_context.Selector.Find(ContextMeleeMove.TakedownId), Is.Null);
            Assert.That(_context.Selector.Find(ContextMeleeMove.FinisherId), Is.Not.Null, "turning the takedown off keeps the finisher");
        }

        [Test]
        public void AnUnawareEnemyFromBehindIsTakenDownSilentlyAndStaysWhereItFell()
        {
            var victim = AddStealth(_enemyHost);
            var (bystanderHost, bystanderAgent, bystander) = CreateStealthEnemy("enemy-2", new Vector3(-3f, 1f, 3f));
            bystanderAgent.Target = _self;
            TurnEnemyAway();

            var cues = 0;
            Action<SoundCueSource> onCue = _ => cues++;
            var lines = new List<string>();
            Application.LogCallback onLog = (condition, stackTrace, type) => lines.Add(condition);
            SubtitleFeed.CuePlayed += onCue;
            Application.logMessageReceived += onLog;
            try
            {
                LogAssert.Expect(LogType.Log, "[ContextMelee] started id=Body target=enemy-1 move=takedown");
                Assert.That(_melee.TryMelee(), Is.True);
                Assert.That(_context.IsPerforming, Is.True);
                Assert.That(_context.Run.Move.Id, Is.EqualTo(ContextMeleeMove.TakedownId));
                Assert.That(_melee.IsSwinging, Is.False, "the plain melee is not swung as well");
                Assert.That(_player.IsControlHandedOff, Is.True);
                Assert.That(_agent.IsInterrupted, Is.True);

                _context.Tick(_context.Run.Move.AlignSeconds);
                var fellAt = _enemyHost.transform.position;
                _context.NoticeWitnesses(_context.Run.Move, new[] { victim, bystander });

                LogAssert.Expect(LogType.Log, $"[ContextMelee] resolved id=Body target=enemy-1 move=takedown outcome=Kill dmg={_enemy.Health.Current} killed=true");
                _context.Tick(_melee.Spec.TakedownSeconds + 1f);

                Assert.That(_context.IsPerforming, Is.False);
                Assert.That(_enemy.IsDead, Is.True);
                Assert.That(_enemyHost != null && _enemyHost.activeSelf, Is.True, "the body stays");
                Assert.That(_enemyHost.transform.position, Is.EqualTo(fellAt), "the body stays where it fell");
                Assert.That(_self.Health.Current, Is.EqualTo(_playerHealth), "the takedown gives no reward");
                Assert.That(_player.IsControlHandedOff, Is.False);

                Assert.That(cues, Is.Zero, "the takedown makes no sound cue");
                Assert.That(bystander.Stage, Is.EqualTo(AwarenessStage.Unaware), "an enemy that did not see it stays unaware");
                Assert.That(bystander.Level, Is.Zero);
                Assert.That(bystander.Awareness.LastSeen, Is.Null);
                Assert.That(lines, Has.None.StartsWith(ContextMeleeLog.WitnessedPrefix("Body")));
                Assert.That(lines, Has.None.StartsWith(ContextMeleeLog.RewardPrefix("Body", ContextMeleeMove.TakedownId)));
            }
            finally
            {
                SubtitleFeed.CuePlayed -= onCue;
                Application.logMessageReceived -= onLog;
                Object.DestroyImmediate(bystanderHost);
            }
        }

        [Test]
        public void AnEnemyThatHasNoticedThePlayerGetsThePlainMeleeEvenFromBehind()
        {
            var stealth = AddStealth(_enemyHost);
            TurnEnemyAway();
            stealth.Awareness.Meter.Raise(stealth.Awareness.Spec.NoticeThreshold);
            Assert.That(stealth.Stage, Is.EqualTo(AwarenessStage.Noticed));

            var unawareBehind = ContextMeleeConditions.TargetBehind | ContextMeleeConditions.TargetUnaware;
            Assert.That(_context.Observe(_enemy) & unawareBehind, Is.EqualTo(ContextMeleeConditions.TargetBehind));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[ContextMelee\] none id=Body target=enemy-1 conditions=TargetBehind(\|Self\w+)*$"));
            LogAssert.Expect(LogType.Log, "[Melee] swing id=Body");
            Assert.That(_melee.TryMelee(), Is.True);
            Assert.That(_context.IsPerforming, Is.False);
            Assert.That(_melee.IsSwinging, Is.True);
        }

        [Test]
        public void AnEnemyFacingThePlayerIsNotTakenDown()
        {
            AddStealth(_enemyHost);
            Assert.That(_context.Observe(_enemy) & ContextMeleeConditions.TargetUnaware, Is.EqualTo(ContextMeleeConditions.TargetUnaware));

            LogAssert.Expect(LogType.Log, new Regex(@"^\[ContextMelee\] none id=Body target=enemy-1 conditions=TargetUnaware(\|Self\w+)*$"));
            Assert.That(_melee.TryMelee(), Is.True);
            Assert.That(_context.IsPerforming, Is.False);
        }

        [Test]
        public void AnEnemyWatchingTheTakedownBecomesAwareAtOnce()
        {
            TurnEnemyAway();
            var (witnessHost, witnessAgent, witness) = CreateStealthEnemy("enemy-2", new Vector3(-4f, 1f, 0f));
            var lines = new List<string>();
            Application.LogCallback onLog = (condition, stackTrace, type) => lines.Add(condition);
            Application.logMessageReceived += onLog;
            try
            {
                witnessAgent.Target = _self;
                SeeTheTarget(witness);
                Assert.That(_melee.TryMelee(), Is.True);
                Assert.That(_context.Run.Move.Id, Is.EqualTo(ContextMeleeMove.TakedownId));

                LogAssert.Expect(LogType.Log, "[ContextMelee] witnessed id=Body target=enemy-1 move=takedown by=enemy-2 stage=Engaged");
                _context.NoticeWitnesses(_context.Run.Move, new[] { witness });
                Assert.That(witness.Stage, Is.EqualTo(AwarenessStage.Engaged));
                Assert.That(witness.Awareness.RaisedByAlarm, Is.False);
                Assert.That(witness.Awareness.LastSeen, Is.EqualTo(_self.GroundPoint.ToNumerics()));

                _context.NoticeWitnesses(_context.Run.Move, new[] { witness });
                Assert.That(lines.FindAll(l => l.StartsWith(ContextMeleeLog.WitnessedPrefix("Body"), StringComparison.Ordinal)), Has.Count.EqualTo(1));
                Assert.That(_context.IsPerforming, Is.True, "being seen does not stop the takedown by itself");
            }
            finally
            {
                Application.logMessageReceived -= onLog;
                Object.DestroyImmediate(witnessHost);
            }
        }

        [Test]
        public void OnlyAnEnemyLookingAtThePlayerDuringAMoveThatRaisesWitnessesCounts()
        {
            TurnEnemyAway();
            var (witnessHost, witnessAgent, witness) = CreateStealthEnemy("enemy-2", new Vector3(-4f, 1f, 0f));
            try
            {
                Assert.That(_melee.TryMelee(), Is.True);
                var takedown = _context.Run.Move;

                witnessAgent.Target = _self;
                _context.NoticeWitnesses(takedown, new[] { witness });
                Assert.That(witness.Stage, Is.EqualTo(AwarenessStage.Unaware), "not looking");

                witnessAgent.Target = null;
                SeeTheTarget(witness);
                var level = witness.Level;
                _context.NoticeWitnesses(takedown, new[] { witness });
                Assert.That(witness.Level, Is.EqualTo(level), "looking at someone else");

                witnessAgent.Target = _self;
                _context.NoticeWitnesses(ContextMeleeMove.Finisher(_melee.Spec), new[] { witness });
                Assert.That(witness.Level, Is.EqualTo(level), "the finisher does not raise witnesses");

                witness.Awareness.Rule = Stealth.Off;
                _context.NoticeWitnesses(takedown, new[] { witness });
                Assert.That(witness.Stage, Is.EqualTo(AwarenessStage.Unaware), "stealth off keeps the old behavior");
            }
            finally
            {
                Object.DestroyImmediate(witnessHost);
            }
        }

        private void TurnEnemyAway()
        {
            _enemyHost.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            Physics.SyncTransforms();
        }

        private static EnemyStealth AddStealth(GameObject host)
        {
            var stealth = host.AddComponent<EnemyStealth>();
            Assert.That(stealth.TryInitialize(), Is.True);
            stealth.Awareness.Rule = Stealth.On;
            return stealth;
        }

        private static void SeeTheTarget(EnemyStealth stealth)
        {
            stealth.Awareness.Meter.Tick(0f, EnemySpec.DefaultSightRange, new AwarenessSighting(true, 4f, 0f, AwarenessPosture.Standing, 1f, 0f));
            Assert.That(stealth.Awareness.Meter.IsSeeing, Is.True);
        }

        private (GameObject Host, EnemyAgent Agent, EnemyStealth Stealth) CreateStealthEnemy(string id, Vector3 position)
        {
            var host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            host.name = id;
            host.transform.SetPositionAndRotation(position, Quaternion.LookRotation(Vector3.left));
            var agent = host.AddComponent<EnemyAgent>();
            agent.WeaponDefinition = _rifle;
            agent.Id = id;
            host.GetComponent<Damageable>().Initialize(id, _enemyHealth);
            Assert.That(agent.TryInitialize(), Is.True);
            var stealth = AddStealth(host);
            Physics.SyncTransforms();
            return (host, agent, stealth);
        }

        private void Stagger()
        {
            _enemy.ApplyHit(_legShot, HitZone.Limb, 2f, DamageFalloff.None);
            Assert.That(_reaction.Current, Is.EqualTo(StaggerKind.Kneel));
        }

        private void MakeAmmoRoom()
        {
            Assert.That(_weapon.Fire(), Is.True);
            Assert.That(_weapon.StartReload(), Is.True);
            _weapon.Tick(10f);
        }

        private sealed class RecordingPresenter : IContextMeleePresenter
        {
            public int Started { get; private set; }

            public int Resolved { get; private set; }

            public int Interrupted { get; private set; }

            public bool LastKilled { get; private set; }

            public void OnStarted(GameObject attacker, Damageable target, ContextMeleeMove move)
            {
                Started++;
            }

            public void OnResolved(GameObject attacker, Damageable target, ContextMeleeMove move, int damage, bool killed)
            {
                Resolved++;
                LastKilled = killed;
            }

            public void OnInterrupted(GameObject attacker, Damageable target, ContextMeleeMove move, ContextMeleeInterruption reason)
            {
                Interrupted++;
            }
        }
    }
}
