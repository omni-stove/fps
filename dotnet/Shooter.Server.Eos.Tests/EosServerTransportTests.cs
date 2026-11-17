using System;
using System.IO;
using NUnit.Framework;
using Shooter.Core;
using Shooter.EosSdk;
using Shooter.Protocol;

namespace Shooter.Server.Eos.Tests
{
    public sealed class EosServerTransportTests
    {
        private static readonly EosCredentials _credentials = new EosCredentials("product", "sandbox", "deployment", "client", "secret");

        [Test]
        public void SaysWhereToPutTheSdkWhenTheBuildHasNone()
        {
            Assume.That(EosSdkBuild.Included, Is.False, "this build includes the EOS SDK");

            Assert.That(
                EosServerTransport.TryCreate(_credentials, "1.0.0", AppContext.BaseDirectory, TextWriter.Null, out var transport, out var error),
                Is.False);
            Assert.That(transport, Is.Null);
            Assert.That(error, Does.Contain("dotnet/ThirdParty/EOSSDK"));
        }

        [Test]
        public void SaysTheNativeLibraryIsMissingWhenItIsNotNextToTheServer()
        {
            Assume.That(EosSdkBuild.Included, Is.True, "this build has no EOS SDK");
            var empty = Path.Combine(Path.GetTempPath(), "shooter-eos-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(empty);
            try
            {
                Assert.That(
                    EosServerTransport.TryCreate(_credentials, "1.0.0", empty, TextWriter.Null, out var transport, out var error),
                    Is.False);
                Assert.That(transport, Is.Null);
                Assert.That(error, Does.Contain(EosSdkBuild.NativeLibraryFile));
            }
            finally
            {
                Directory.Delete(empty, true);
            }
        }

        [Test]
        public void CreatesAnEosTransportThatDoesNotTouchTheSdkUntilItListens()
        {
            Assume.That(EosSdkBuild.Included && EosSdkBuild.HasNativeLibrary(AppContext.BaseDirectory), Is.True, "this build has no EOS SDK");

            Assert.That(
                EosServerTransport.TryCreate(_credentials, "1.0.0", AppContext.BaseDirectory, TextWriter.Null, out var transport, out var error),
                Is.True,
                error);
            using (transport)
            {
                Assert.That(transport, Is.InstanceOf<EosMatchTransport>());
                Assert.That(transport.IsActive, Is.False);
            }
        }
    }
}
