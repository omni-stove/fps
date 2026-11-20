using System;
using System.Linq;
using System.Reflection;
using Shooter.Core;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    // dotnet の Shooter.Core は Core/World も 1 つのアセンブリにまとめてビルドするので、アセンブリの境目は Unity のテストでだけ確かめる
    public sealed class CoreWorldAssemblyTests
    {
        private const string WorldAssembly = "Shooter.Core.World";

        [Test]
        public void WorldTypes_LiveInTheWorldAssembly()
        {
            var types = new[]
            {
                typeof(WorldHit),
                typeof(IWorldCollision),
                typeof(BvhWorldCollision),
                typeof(CapsuleMover),
                typeof(CapsuleMoverSpec),
                typeof(CapsuleContacts),
                typeof(MapColliderGroups),
                typeof(ShooterMapFile),
                typeof(WorldMap),
                typeof(WorldCollider),
                typeof(WorldShapeKind),
            };

            foreach (var type in types)
            {
                Assert.That(type.Assembly.GetName().Name, Is.EqualTo(WorldAssembly), type.Name);
                Assert.That(type.Namespace, Is.EqualTo("Shooter.Core"), type.Name);
            }

            Assert.That(typeof(MapVehicle).Assembly.GetName().Name, Is.EqualTo("Shooter.Core"));
        }

        [Test]
        public void WorldAssembly_ReferencesNeitherTheKitNorTheEngine()
        {
            var references = typeof(IWorldCollision).Assembly.GetReferencedAssemblies().Select(a => a.Name).ToArray();

            Assert.That(references, Has.None.StartsWith("Shooter."));
            Assert.That(references, Has.None.StartsWith("UnityEngine"));
            Assert.That(references, Has.None.StartsWith("UnityEditor"));
        }

        [Test]
        public void WorldAssembly_SharesItsInternalsOnlyWithCore()
        {
            var friends = typeof(IWorldCollision).Assembly
                .GetCustomAttributes<System.Runtime.CompilerServices.InternalsVisibleToAttribute>()
                .Select(a => a.AssemblyName)
                .ToArray();

            Assert.That(friends, Is.EquivalentTo(new[] { "Shooter.Core" }));
        }

        [Test]
        public void WorldGeometry_StaysInternal()
        {
            var geometry = typeof(IWorldCollision).Assembly.GetType("Shooter.Core.WorldGeometry", true);

            Assert.That(geometry.IsPublic, Is.False);
            Assert.That(Type.GetType("Shooter.Core.WorldGeometry, Shooter.Core"), Is.Null);
        }
    }
}
