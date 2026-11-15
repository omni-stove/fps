using System.Reflection;
using NUnit.Framework;

namespace Fps.Server.Tests
{
    public sealed class ServerBuildVersionTests
    {
        [Test]
        public void FindsTheVersionByItsKey()
        {
            var metadata = new[]
            {
                new AssemblyMetadataAttribute("RepositoryUrl", "https://example.com"),
                new AssemblyMetadataAttribute(ServerBuildVersion.MetadataKey, "v1.2.3"),
            };

            Assert.That(ServerBuildVersion.Find(metadata), Is.EqualTo("v1.2.3"));
        }

        [Test]
        public void IsNullWhenNotBuiltIn()
        {
            Assert.That(ServerBuildVersion.Find(System.Array.Empty<AssemblyMetadataAttribute>()), Is.Null);
            Assert.That(ServerBuildVersion.Find(new[] { new AssemblyMetadataAttribute(ServerBuildVersion.MetadataKey, " ") }), Is.Null);
        }

        [Test]
        public void ATestBuildHasNoVersionBuiltIn()
        {
            Assert.That(ServerBuildVersion.Embedded(), Is.Null);
        }
    }
}
