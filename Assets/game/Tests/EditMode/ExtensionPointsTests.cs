using System;
using System.Linq;
using Fps.Editor;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="ExtensionPoints"/> が差し込み口を実装した public な具象クラスだけを順に作ることを確かめる。
    /// </summary>
    public sealed class ExtensionPointsTests
    {
        public interface IProbe
        {
            int Order { get; }
        }

        public interface IProbeWithoutConstructor
        {
        }

        [Test]
        public void CreatesPublicConcreteTypesInOrderThenByName()
        {
            var created = ExtensionPoints.CreateAll<IProbe>(p => p.Order);

            Assert.That(created.Select(p => p.GetType()), Is.EqualTo(new[] { typeof(FirstProbe), typeof(ProbeA), typeof(ProbeB) }));
        }

        [Test]
        public void SkipsTypesThatCannotBeCreatedOrAreNotPublic()
        {
            Assert.That(ExtensionPoints.IsCandidate(typeof(ProbeA)), Is.True);
            Assert.That(ExtensionPoints.IsCandidate(typeof(HiddenProbe)), Is.False, "private");
            Assert.That(ExtensionPoints.IsCandidate(typeof(AbstractProbe)), Is.False, "abstract");
            Assert.That(ExtensionPoints.IsCandidate(typeof(GenericProbe<>)), Is.False, "open generic");
            Assert.That(ExtensionPoints.IsCandidate(typeof(IProbe)), Is.False, "interface");
            Assert.That(ExtensionPoints.IsCandidate(null), Is.False);
        }

        [Test]
        public void APublicTypeWithoutAConstructorIsReported()
        {
            var e = Assert.Throws<InvalidOperationException>(() => ExtensionPoints.CreateAll<IProbeWithoutConstructor>(_ => 0));

            Assert.That(e.Message, Does.Contain(nameof(NeedsAnArgument)));
        }

        [Test]
        public void CreateAllRejectsAMissingOrder()
        {
            Assert.Throws<ArgumentNullException>(() => ExtensionPoints.CreateAll<IProbe>(null));
        }

        public sealed class ProbeA : IProbe
        {
            public int Order => 1;
        }

        public sealed class ProbeB : IProbe
        {
            public int Order => 1;
        }

        public sealed class FirstProbe : IProbe
        {
            public int Order => 0;
        }

        public abstract class AbstractProbe : IProbe
        {
            public int Order => -1;
        }

        public sealed class GenericProbe<T> : IProbe
        {
            public int Order => -1;
        }

        public sealed class NeedsAnArgument : IProbeWithoutConstructor
        {
            public NeedsAnArgument(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        private sealed class HiddenProbe : IProbe
        {
            public int Order => -1;
        }
    }
}
