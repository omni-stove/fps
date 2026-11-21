using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // サーバーへ書き出すとどめの値を、オフラインの ExecutionRunner と同じ順 (欄のアセット、入口) で選ぶことを確かめる
    public sealed class NetMatchDefinitionExecutionTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private readonly List<Object> _objects = new List<Object>();
        private NetMatchDefinition _match;
        private GameContent _content;

        [SetUp]
        public void SetUp()
        {
            _match = Make<NetMatchDefinition>();
            _content = Make<GameContent>();
            GameContent.Use(_content);
        }

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            for (var i = _objects.Count - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(_objects[i]);
            }

            _objects.Clear();
        }

        [Test]
        public void WithoutAnAssetTheDefaultsAreCarried()
        {
            Assert.That(_match.ToSettings().Execution, Is.SameAs(ExecutionSpec.Default));
        }

        [Test]
        public void TheWiredExecutionAssetIsCarried()
        {
            var definition = Make<ExecutionDefinition>();
            definition.ExecuteSeconds = 2.5f;
            definition.ExecuteRange = 3f;
            _match.Execution = definition;

            var spec = _match.ToSettings().Execution;

            Assert.That(spec.ExecuteSeconds, Is.EqualTo(2.5f));
            Assert.That(spec.ExecuteRange, Is.EqualTo(3f));
        }

        [Test]
        public void WithoutAnAssetTheGameContentOneIsRead()
        {
            var definition = Make<ExecutionDefinition>();
            definition.ExecuteSeconds = 1f;
            _content.Execution = definition;

            Assert.That(_match.ToSettings().Execution.ExecuteSeconds, Is.EqualTo(1f));
        }

        [Test]
        public void AnInvalidExecutionAssetStopsTheSettings()
        {
            var definition = Make<ExecutionDefinition>();
            definition.name = "BrokenExecution";
            definition.ExecuteRange = 0f;
            _match.Execution = definition;

            var e = Assert.Throws<InvalidOperationException>(() => _match.ToSettings());

            Assert.That(e.Message, Does.Contain("BrokenExecution").And.Contain("executeRange"));
        }

        [Test]
        public void TheCarriedValuesSurviveTheServerFile()
        {
            var definition = Make<ExecutionDefinition>();
            definition.ExecuteSeconds = 2f;
            definition.ExecuteRange = 2.5f;
            _match.Execution = definition;
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None);
            var file = new NetMatchFile(
                "Execution",
                GameRules.Default.WithObjective(MatchObjective.Execution),
                _match.ToSettings(),
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.Handgun } }),
                new[] { handgun },
                0.4f,
                null,
                null);

            var read = NetMatchFile.Parse(NetMatchFile.Write(file), "Execution.json").Settings.Execution;

            Assert.That(read.ExecuteSeconds, Is.EqualTo(2f));
            Assert.That(read.ExecuteRange, Is.EqualTo(2.5f));
        }

        private T Make<T>()
            where T : ScriptableObject
        {
            var made = ScriptableObject.CreateInstance<T>();
            _objects.Add(made);
            return made;
        }
    }
}
