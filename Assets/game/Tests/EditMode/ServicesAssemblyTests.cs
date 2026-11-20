using System;
using System.Linq;
using NUnit.Framework;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    public sealed class ServicesAssemblyTests
    {
        private const string ServicesAssembly = "Shooter.Services";
        private const string RuntimeAssembly = "Shooter.Runtime";

        private static readonly Type[] _serviceTypes =
        {
            typeof(AccountBootstrap),
            typeof(AccountDriver),
            typeof(AccountSignIn),
            typeof(IAccountBackend),
            typeof(CloudSaveBootstrap),
            typeof(CloudSaveDriver),
            typeof(CloudSaveSync),
            typeof(ICloudSaveBackend),
            typeof(FileSaveStorage),
            typeof(SkillBootstrap),
            typeof(SkillCloudSave),
            typeof(SkillRecorder),
        };

        [TestCaseSource(nameof(_serviceTypes))]
        public void ServiceType_LivesInTheServicesAssembly_UnderTheRuntimeNamespace(Type type)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo(ServicesAssembly));
            Assert.That(type.Namespace, Is.EqualTo("Shooter.Runtime"), "the namespace stays so games using these types do not change");
        }

        [Test]
        public void RuntimeAssembly_DoesNotReferenceTheServicesAssembly()
        {
            var runtime = typeof(GameSignals).Assembly;
            Assert.That(runtime.GetName().Name, Is.EqualTo(RuntimeAssembly));
            Assert.That(runtime.GetReferencedAssemblies().Select(a => a.Name), Has.None.EqualTo(ServicesAssembly));
        }

        [Test]
        public void RuntimeAssembly_NoLongerHoldsTheServiceTypes()
        {
            var runtime = typeof(GameSignals).Assembly;
            foreach (var type in _serviceTypes)
            {
                Assert.That(runtime.GetType(type.FullName), Is.Null, type.FullName);
            }
        }

        [Test]
        public void ServicesAssembly_OnlyBuildsOnCoreAndRuntime()
        {
            var references = typeof(CloudSaveBootstrap).Assembly.GetReferencedAssemblies()
                .Select(a => a.Name)
                .Where(n => n.StartsWith("Shooter.", StringComparison.Ordinal))
                .OrderBy(n => n, StringComparer.Ordinal);
            Assert.That(references, Is.EqualTo(new[] { "Shooter.Core", "Shooter.Runtime" }));
        }
    }
}
