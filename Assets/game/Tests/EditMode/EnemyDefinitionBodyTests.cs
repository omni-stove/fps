using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Update は Edit Mode では走らないので、向き直りは Face を、怯みは Bind した StaggerReaction へ当てた弾で直接確かめる。
    public sealed class EnemyDefinitionBodyTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
        private Damageable _damageable;
        private EnemyDefinition _definition;
        private WeaponDefinition _rifle;
        private GameContent _content;
        private Stagger _savedRule;

        [SetUp]
        public void SetUp()
        {
            _savedRule = StaggerReaction.ActiveRule;
            StaggerReaction.ActiveRule = Stagger.On;

            // Resources に置いた入口の敵のデータが、定義の無い敵の値に混ざらないようにする。
            _content = ScriptableObject.CreateInstance<GameContent>();
            _content.name = "Empty";
            GameContent.Use(_content);

            _rifle = ScriptableObject.CreateInstance<WeaponDefinition>();
            _rifle.name = "Rifle";
            WeaponAssets.Configure(_rifle, TestWeapons.RifleSpec());

            _definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            _definition.name = "Grunt";

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Enemy";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            _agent.Id = "enemy-1";
            _damageable = _host.GetComponent<Damageable>();
            _damageable.Initialize("enemy-1", 1000);
        }

        [TearDown]
        public void TearDown()
        {
            StaggerReaction.ActiveRule = _savedRule;
            GameContent.Use(null);
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_definition);
            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_content);
        }

        [Test]
        public void TheDefinitionsTurnSpeedPacesTurningToATargetBehind()
        {
            _definition.TurnSpeed = 90f;
            _agent.Definition = _definition;
            Assert.That(_agent.TryInitialize(), Is.True);

            _agent.Face(new System.Numerics.Vector3(0f, 0f, -10f), 1f);

            Assert.That(_agent.TurnSpeed, Is.EqualTo(90f));
            Assert.That(Quaternion.Angle(Quaternion.identity, _host.transform.rotation), Is.EqualTo(90f).Within(0.01f));
        }

        [Test]
        public void WithoutADefinitionTheInspectorTurnSpeedIsUsed()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            _agent.Face(new System.Numerics.Vector3(0f, 0f, -10f), 0.25f);

            Assert.That(_agent.TurnSpeed, Is.EqualTo(EnemyAgent.DefaultTurnSpeed));
            Assert.That(Quaternion.Angle(Quaternion.identity, _host.transform.rotation), Is.EqualTo(90f).Within(0.01f), "360 deg/s x 0.25 s");
        }

        [Test]
        public void TheDefinitionsBodyReachesTheAgent()
        {
            _definition.EyeHeight = 1.9f;
            _definition.CrouchHeight = 1.2f;
            _definition.CrouchEyeHeight = 1.1f;
            _definition.RecoilControl = 0.2f;
            _agent.Definition = _definition;

            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.EyeHeight, Is.EqualTo(1.9f));
            Assert.That(_agent.CrouchHeight, Is.EqualTo(1.2f));
            Assert.That(_agent.CrouchEyeHeight, Is.EqualTo(1.1f));
            Assert.That(_agent.EyePosition.y - _agent.GroundPosition.y, Is.EqualTo(1.9f).Within(1e-4f));
            Assert.That(_agent.Brain.Spec.RecoilControl, Is.EqualTo(0.2f));
        }

        [Test]
        public void AnAppliedSpecKeepsTheDefinitionsBody()
        {
            _definition.TurnSpeed = 45f;
            _definition.EyeHeight = 1.4f;
            _agent.Definition = _definition;
            _agent.ApplySpec(new EnemySpec(20f, 10f, 1f, 2f, 0.5f, recoilControl: 0.3f));

            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.TurnSpeed, Is.EqualTo(45f));
            Assert.That(_agent.EyeHeight, Is.EqualTo(1.4f));
            Assert.That(_agent.Brain.Spec.RecoilControl, Is.EqualTo(0.3f));
        }

        [Test]
        public void WithoutADefinitionTheInspectorBodyIsUsed()
        {
            Assert.That(_agent.TryInitialize(), Is.True);

            Assert.That(_agent.EyeHeight, Is.EqualTo(EnemyAgent.DefaultEyeHeight));
            Assert.That(_agent.CrouchHeight, Is.EqualTo(EnemyAgent.DefaultCrouchHeight));
            Assert.That(_agent.CrouchEyeHeight, Is.EqualTo(EnemyAgent.DefaultCrouchEyeHeight));
            Assert.That(_agent.Brain.Spec.RecoilControl, Is.EqualTo(EnemySpec.DefaultRecoilControl));
        }

        [Test]
        public void TheDefinitionsStaggerResistanceIsReadBeforeAndAfterInitializing()
        {
            _definition.StaggerResistance = 2f;
            _agent.Definition = _definition;

            Assert.That(_agent.StaggerResistance, Is.EqualTo(2f));
            Assert.That(_agent.TryInitialize(), Is.True);
            Assert.That(_agent.StaggerResistance, Is.EqualTo(2f));
        }

        [Test]
        public void ADefinitionLeftOnItsArchetypeUsesTheArchetypesStaggerResistance()
        {
            _definition.Archetype = EnemyArchetype.Shield;
            _agent.Definition = _definition;

            Assert.That(_agent.StaggerResistance, Is.EqualTo(ShieldSpec.DefaultStaggerResistance));
        }

        [Test]
        public void AResistantDefinitionNeedsMoreDamageToStaggerAndStaggersShorter()
        {
            _definition.StaggerResistance = 2f;
            _agent.Definition = _definition;
            Assert.That(_agent.TryInitialize(), Is.True);
            var reaction = _host.AddComponent<StaggerReaction>();
            Assert.That(reaction.Bind(), Is.True);

            _damageable.ApplyHit(10, HitZone.Limb, 5f, DamageFalloff.None);
            Assert.That(reaction.IsStaggered, Is.False, "8 x 3 = 24 is needed; 8 is not enough");

            LogAssert.Expect(LogType.Log, "[AI] id=enemy-1 interrupted reason=stagger-Kneel seconds=0.50");
            LogAssert.Expect(LogType.Log, new Regex(@"^\[Stagger\] id=enemy-1 staggered kind=Kneel seconds=0\.50 "));
            _damageable.ApplyHit(25, HitZone.Limb, 5f, DamageFalloff.None);
            Assert.That(reaction.Current, Is.EqualTo(StaggerKind.Kneel));
        }
    }
}
