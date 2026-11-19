using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Editor;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // Run は開いているシーンを次々に置き換えて保存するので、本物の手順ではなく記録するだけの手順で確かめる。
    public sealed class SceneBuildStepsTests
    {
        [Test]
        public void OneMenuBuildsEveryScene()
        {
            Assert.That(SceneBuildSteps.MenuPath, Is.EqualTo("Shooter/Build All Scenes"));
            Assert.That(BootstrapSceneBuilder.MenuPath, Is.EqualTo(SceneBuildSteps.MenuPath));
            Assert.That(MenuSceneBuilder.MenuPath, Is.EqualTo(SceneBuildSteps.MenuPath));
            Assert.That(CampaignSceneBuilder.MenuPath, Is.EqualTo(SceneBuildSteps.MenuPath));
        }

        [Test]
        public void BuiltInStepsStartAtTheBootstrapAndEndAtTheMainMenu()
        {
            var names = SceneBuildSteps.Sort(SceneBuildSteps.BuiltIn).Select(step => step.Name).ToList();

            Assert.That(names, Is.EqualTo(new[]
            {
                "Bootstrap", "TpsRange", "Features", "EnemyAiTest", "WaveTest", "DestructionTest", "MissionTest", "VehicleRange",
                "ModeTest", "FreeForAll", "KillConfirmed", "CaptureTheFlag", "Domination", "Hardpoint", "SearchAndDestroy",
                "Execution", "GunGame", "Infected", "Horde", "Guardian", "Extraction", "BattleRoyale", "Campaign", "MainMenu",
            }));
            Assert.That(names.IndexOf("MainMenu"), Is.GreaterThan(names.IndexOf("Campaign")), "the menu lists the missions");
            Assert.That(SceneBuildSteps.BuiltIn.Max(step => step.Order), Is.LessThan(SceneBuildSteps.NetOrder));
        }

        [Test]
        public void DiscoverAddsTheNetStepAfterTheBuiltInOnes()
        {
            var names = SceneBuildSteps.Discover().Select(step => step.Name).ToList();

            Assert.That(names.First(), Is.EqualTo("Bootstrap"));
            Assert.That(names.Last(), Is.EqualTo("Net"));
            Assert.That(names, Is.Unique);
            Assert.That(names.Count, Is.EqualTo(SceneBuildSteps.BuiltIn.Count + 1));
        }

        [Test]
        public void CollectSkipsHiddenAndUnrelatedTypes()
        {
            Assert.That(SceneBuildSteps.Collect(new[] { typeof(HiddenStep), typeof(string) }), Is.Empty);
            Assert.Throws<ArgumentException>(() => SceneBuildSteps.Collect(new Type[] { null }));
            Assert.Throws<ArgumentNullException>(() => SceneBuildSteps.Collect(null));
        }

        [Test]
        public void RunGoesInOrderAndNamesTheFailingStep()
        {
            var log = new List<string>();
            var done = SceneBuildSteps.Run(new[] { new RecordingStep("B", 2, log), new RecordingStep("A", 1, log) });

            Assert.That(done, Is.EqualTo(new[] { "A", "B" }));
            Assert.That(log, Is.EqualTo(new[] { "A", "B" }));

            var failing = new RecordingStep("Broken", 0, log, fail: true);
            var e = Assert.Throws<InvalidOperationException>(() => SceneBuildSteps.Run(new[] { failing }));
            Assert.That(e.Message, Does.Contain("Broken"));
        }

        [Test]
        public void SortRejectsNullAndDuplicateNames()
        {
            var log = new List<string>();

            Assert.Throws<ArgumentException>(() => SceneBuildSteps.Sort(new ISceneBuildStep[] { null }));
            Assert.Throws<ArgumentException>(() => SceneBuildSteps.Sort(new[] { new RecordingStep("A", 1, log), new RecordingStep("A", 2, log) }));
            Assert.Throws<ArgumentNullException>(() => SceneBuildSteps.Sort(null));
        }

        // 公開すると本物の Discover にも拾われ、Build All Scenes で走ってしまうので、テストの手順は公開しない。
        private sealed class HiddenStep : ISceneBuildStep
        {
            public string Name => "Hidden";

            public int Order => 0;

            public void Build()
            {
            }
        }

        private sealed class RecordingStep : ISceneBuildStep
        {
            private readonly List<string> _log;
            private readonly bool _fail;

            public RecordingStep(string name, int order, List<string> log, bool fail = false)
            {
                Name = name;
                Order = order;
                _log = log;
                _fail = fail;
            }

            public string Name { get; }

            public int Order { get; }

            public void Build()
            {
                if (_fail)
                {
                    throw new InvalidOperationException("boom");
                }

                _log.Add(Name);
            }
        }
    }
}
