using System;
using System.Linq;
using System.Reflection;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class TelemetryInteractionAssemblyTests
    {
        [TestCase(typeof(TelemetryHost))]
        [TestCase(typeof(ClientTelemetry))]
        [TestCase(typeof(CrashCapture))]
        [TestCase(typeof(MatchSummaryReporter))]
        public void TelemetryTypesLiveInTheTelemetryAssembly(Type type)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Shooter.Telemetry"));
            Assert.That(type.Namespace, Is.EqualTo("Shooter.Runtime"));
        }

        [TestCase(typeof(CoopDoor))]
        [TestCase(typeof(CoopInteractable))]
        [TestCase(typeof(CoopInteractableDefinition))]
        public void InteractionTypesLiveInTheInteractionAssembly(Type type)
        {
            Assert.That(type.Assembly.GetName().Name, Is.EqualTo("Shooter.Interaction"));
            Assert.That(type.Namespace, Is.EqualTo("Shooter.Runtime"));
        }

        [TestCase("Shooter.Telemetry")]
        [TestCase("Shooter.Interaction")]
        public void TheRuntimeAssemblyDoesNotDependOnTheSplitAssembly(string split)
        {
            Assert.That(ReferencedNames(typeof(GameBootstrap).Assembly), Has.No.Member(split));
        }

        [TestCase(typeof(TelemetryHost))]
        [TestCase(typeof(CoopInteractable))]
        public void TheSplitAssemblyBuildsOnCoreAndRuntime(Type type)
        {
            var references = ReferencedNames(type.Assembly);

            Assert.That(references, Has.Member("Shooter.Core"));
            Assert.That(references, Has.Member("Shooter.Runtime"));
        }

        [Test]
        public void TheInteractionAssemblyReadsTheInteractInputThroughTheInputSystem()
        {
            Assert.That(ReferencedNames(typeof(CoopInteractable).Assembly), Has.Member("Unity.InputSystem"));
        }

        [Test]
        public void TheTelemetryHostStillInstallsOnLoad()
        {
            var install = typeof(TelemetryHost).GetMethod("InstallOnLoad", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(install, Is.Not.Null);
            Assert.That(install.IsDefined(typeof(UnityEngine.RuntimeInitializeOnLoadMethodAttribute), false), Is.True);
        }

        private static string[] ReferencedNames(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();
        }
    }
}
