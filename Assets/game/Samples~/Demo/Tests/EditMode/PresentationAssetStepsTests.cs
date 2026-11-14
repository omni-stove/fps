using System;
using System.Collections.Generic;
using System.Linq;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="PresentationAssetSteps"/> が手順を集める規則・実行の順・失敗したときの止まり方と、既存の
    /// <see cref="PresentationAssetBuilder"/> が手順として集まることを確かめる。素材を書き出す <see cref="PresentationAssetSteps.BuildAll"/> は呼ばない。
    /// </summary>
    public sealed class PresentationAssetStepsTests
    {
        [Test]
        public void DiscoverFindsThePresentationCatalogStepFirst()
        {
            var steps = PresentationAssetSteps.Discover();

            Assert.That(steps, Is.Not.Empty);
            Assert.That(steps[0], Is.InstanceOf<PresentationCatalogAssetStep>(), "the existing builder runs from the batch entry");
            Assert.That(steps.Count(step => step is PresentationCatalogAssetStep), Is.EqualTo(1));
            Assert.That(steps.Any(step => step.GetType().DeclaringType == typeof(PresentationAssetStepsTests)), Is.False,
                "steps that only tests can see are not run by the batch entry");
        }

        [Test]
        public void PresentationCatalogStepRunsTheExistingBuilderFirst()
        {
            var step = new PresentationCatalogAssetStep();

            Assert.That(step.Order, Is.EqualTo(PresentationCatalogAssetStep.StepOrder));
            Assert.That(step.Name, Is.EqualTo("PresentationCatalog"));
        }

        [Test]
        public void CollectCreatesOnlyPublicConcreteSteps()
        {
            var steps = PresentationAssetSteps.Collect(new[]
            {
                typeof(IPresentationAssetStep),
                typeof(AbstractStep),
                typeof(HiddenStep),
                typeof(string),
                typeof(PresentationCatalogAssetStep),
            });

            Assert.That(steps, Has.Count.EqualTo(1));
            Assert.That(steps[0], Is.InstanceOf<PresentationCatalogAssetStep>());
        }

        [Test]
        public void CollectRejectsNullInput()
        {
            Assert.Throws<ArgumentNullException>(() => PresentationAssetSteps.Collect(null));
            Assert.Throws<ArgumentException>(() => PresentationAssetSteps.Collect(new Type[] { null }));
        }

        [Test]
        public void RunExecutesTheStepsByOrderThenTypeName()
        {
            var log = new List<string>();
            var steps = new IPresentationAssetStep[]
            {
                new RecordingStep("late", 20, log),
                new OtherRecordingStep("tie-other", 10, log),
                new RecordingStep("tie", 10, log),
                new RecordingStep("first", -5, log),
            };

            var done = PresentationAssetSteps.Run(steps);

            // 同じ Order は型の完全名の順 (OtherRecordingStep が RecordingStep より先)。
            Assert.That(log, Is.EqualTo(new[] { "first", "tie-other", "tie", "late" }));
            Assert.That(done, Is.EqualTo(log));
        }

        [Test]
        public void AFailingStepStopsTheRunAndNamesTheStep()
        {
            var log = new List<string>();
            var steps = new IPresentationAssetStep[]
            {
                new RecordingStep("before", 1, log),
                new RecordingStep("broken", 2, log, new InvalidOperationException("no shader")),
                new RecordingStep("after", 3, log),
            };

            var thrown = Assert.Throws<InvalidOperationException>(() => PresentationAssetSteps.Run(steps));

            Assert.That(thrown.Message, Does.Contain("broken"));
            Assert.That(thrown.Message, Does.Contain("no shader"));
            Assert.That(thrown.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(log, Is.EqualTo(new[] { "before", "broken" }), "the steps after the failure do not run");
        }

        [Test]
        public void RunRejectsNullInput()
        {
            Assert.Throws<ArgumentNullException>(() => PresentationAssetSteps.Run(null));
            Assert.Throws<ArgumentException>(() => PresentationAssetSteps.Run(new IPresentationAssetStep[] { null }));
        }

        [Test]
        public void RunWithNoStepsDoesNothing()
        {
            Assert.That(PresentationAssetSteps.Run(Array.Empty<IPresentationAssetStep>()), Is.Empty);
        }

        [Test]
        public void CatalogResourcePathFollowsTheNamingRule()
        {
            Assert.That(PresentationCatalog.CatalogResourcePath("Impact"), Is.EqualTo("Presentation/ImpactCatalog"));
            Assert.That(PresentationCatalog.CatalogResourcePath("Presentation"), Is.EqualTo(PresentationCatalog.ResourcePath),
                "the existing catalog follows the same rule");

            Assert.Throws<ArgumentException>(() => PresentationCatalog.CatalogResourcePath(null));
            Assert.Throws<ArgumentException>(() => PresentationCatalog.CatalogResourcePath(string.Empty));
            Assert.Throws<ArgumentException>(() => PresentationCatalog.CatalogResourcePath("Bad Name"));
            Assert.Throws<ArgumentException>(() => PresentationCatalog.CatalogResourcePath("Sub/Impact"));
            Assert.Throws<ArgumentException>(() => PresentationCatalog.CatalogResourcePath("ImpactCatalog"));
        }

        public abstract class AbstractStep : IPresentationAssetStep
        {
            public abstract string Name { get; }

            public abstract int Order { get; }

            public abstract void Build();
        }

        // 公開されていない型は集めない。引数の無いコンストラクタがあっても一括実行には混ざらない。
        private sealed class HiddenStep : IPresentationAssetStep
        {
            public string Name => "hidden";

            public int Order => 0;

            public void Build()
            {
                throw new InvalidOperationException("a hidden step must never run");
            }
        }

        private class RecordingStep : IPresentationAssetStep
        {
            private readonly List<string> _log;
            private readonly Exception _failure;

            public RecordingStep(string name, int order, List<string> log, Exception failure = null)
            {
                Name = name;
                Order = order;
                _log = log;
                _failure = failure;
            }

            public string Name { get; }

            public int Order { get; }

            public void Build()
            {
                _log.Add(Name);
                if (_failure != null)
                {
                    throw _failure;
                }
            }
        }

        private sealed class OtherRecordingStep : RecordingStep
        {
            public OtherRecordingStep(string name, int order, List<string> log)
                : base(name, order, log)
            {
            }
        }
    }
}
