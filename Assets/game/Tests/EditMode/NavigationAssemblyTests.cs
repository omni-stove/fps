using System.Linq;
using Shooter.Core;
using Shooter.Core.Navigation;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class NavigationAssemblyTests
    {
        private const string NavigationAssembly = "Shooter.Core.Navigation";
        private const string CoreAssembly = "Shooter.Core";

        [Test]
        public void NavigationTypesLiveInTheirOwnAssembly()
        {
            Assert.That(typeof(MeshNavigation).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
            Assert.That(typeof(NavMeshFile).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
            Assert.That(typeof(NavLink).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
            Assert.That(typeof(NavLinkKind).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
            Assert.That(typeof(NavTriangle).Assembly.GetName().Name, Is.EqualTo(NavigationAssembly));
        }

        [Test]
        public void TheNavigationContractStaysInCore()
        {
            Assert.That(typeof(INavigation).Assembly.GetName().Name, Is.EqualTo(CoreAssembly));
            Assert.That(typeof(NavPath).Assembly.GetName().Name, Is.EqualTo(CoreAssembly));
        }

        [Test]
        public void NavigationDependsOnCoreButCoreDoesNotDependOnNavigation()
        {
            var navigationReferences = typeof(MeshNavigation).Assembly.GetReferencedAssemblies().Select(name => name.Name);
            var coreReferences = typeof(INavigation).Assembly.GetReferencedAssemblies().Select(name => name.Name);

            Assert.That(navigationReferences, Does.Contain(CoreAssembly));
            Assert.That(coreReferences, Does.Not.Contain(NavigationAssembly));
        }

        [Test]
        public void NavigationDoesNotReferenceTheEngine()
        {
            var navigationReferences = typeof(MeshNavigation).Assembly.GetReferencedAssemblies().Select(name => name.Name);

            Assert.That(navigationReferences.Where(name => name.StartsWith("UnityEngine")), Is.Empty);
        }
    }
}
