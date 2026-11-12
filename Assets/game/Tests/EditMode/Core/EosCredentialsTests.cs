using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class EosCredentialsTests
    {
        private static readonly Func<string, string> _emptyEnvironment = _ => null;

        private static Dictionary<string, string> AllValues(string prefix)
        {
            return new Dictionary<string, string>
            {
                { EosCredentials.ProductIdVariable, prefix + "product" },
                { EosCredentials.SandboxIdVariable, prefix + "sandbox" },
                { EosCredentials.DeploymentIdVariable, prefix + "deployment" },
                { EosCredentials.ClientIdVariable, prefix + "client" },
                { EosCredentials.ClientSecretVariable, prefix + "secret" },
            };
        }

        private static Func<string, string> Lookup(IReadOnlyDictionary<string, string> values)
        {
            return name => values.TryGetValue(name, out var value) ? value : null;
        }

        [Test]
        public void ReadsEverythingFromLookup()
        {
            var credentials = EosCredentials.Resolve(Lookup(AllValues("env-")), null, out var missing);

            Assert.That(missing, Is.Empty);
            Assert.That(credentials, Is.Not.Null);
            Assert.That(credentials.ProductId, Is.EqualTo("env-product"));
            Assert.That(credentials.SandboxId, Is.EqualTo("env-sandbox"));
            Assert.That(credentials.DeploymentId, Is.EqualTo("env-deployment"));
            Assert.That(credentials.ClientId, Is.EqualTo("env-client"));
            Assert.That(credentials.ClientSecret, Is.EqualTo("env-secret"));
        }

        [Test]
        public void ReadsEverythingFromLocalFile()
        {
            var credentials = EosCredentials.Resolve(_emptyEnvironment, AllValues("file-"), out var missing);

            Assert.That(missing, Is.Empty);
            Assert.That(credentials.ClientSecret, Is.EqualTo("file-secret"));
        }

        [Test]
        public void EnvironmentOverridesLocalFilePerKey()
        {
            var environment = new Dictionary<string, string> { { EosCredentials.ClientSecretVariable, "env-secret" } };

            var credentials = EosCredentials.Resolve(Lookup(environment), AllValues("file-"), out _);

            Assert.That(credentials.ClientSecret, Is.EqualTo("env-secret"));
            Assert.That(credentials.ProductId, Is.EqualTo("file-product"));
        }

        [Test]
        public void BlankEnvironmentFallsBackToLocalFile()
        {
            var environment = new Dictionary<string, string> { { EosCredentials.ProductIdVariable, "  " } };

            var credentials = EosCredentials.Resolve(Lookup(environment), AllValues("file-"), out _);

            Assert.That(credentials.ProductId, Is.EqualTo("file-product"));
        }

        [Test]
        public void TrimsValues()
        {
            var values = AllValues("");
            values[EosCredentials.ClientIdVariable] = "  spaced \n";

            var credentials = EosCredentials.Resolve(Lookup(values), null, out _);

            Assert.That(credentials.ClientId, Is.EqualTo("spaced"));
        }

        [Test]
        public void ReportsEverythingMissingWhenNothingIsSet()
        {
            var credentials = EosCredentials.Resolve(_emptyEnvironment, null, out var missing);

            Assert.That(credentials, Is.Null);
            Assert.That(missing, Is.EqualTo(EosCredentials.VariableNames));
        }

        [Test]
        public void ReportsOnlyTheMissingNames()
        {
            var values = AllValues("x");
            values.Remove(EosCredentials.DeploymentIdVariable);
            values[EosCredentials.ClientSecretVariable] = "";

            var credentials = EosCredentials.Resolve(Lookup(values), null, out var missing);

            Assert.That(credentials, Is.Null);
            var expected = new[] { EosCredentials.DeploymentIdVariable, EosCredentials.ClientSecretVariable };
            Assert.That(missing, Is.EqualTo(expected));
        }

        [Test]
        public void HidesValuesInToString()
        {
            var credentials = EosCredentials.Resolve(Lookup(AllValues("visible-")), null, out _);

            Assert.That(credentials.ToString(), Does.Not.Contain("visible-"));
        }

        [Test]
        public void ConstructorRejectsBlankValues()
        {
            Assert.Throws<ArgumentException>(() => new EosCredentials("p", "s", "d", "c", " "));
            Assert.Throws<ArgumentException>(() => new EosCredentials(null, "s", "d", "c", "x"));
        }

        [Test]
        public void RejectsNullLookup()
        {
            Assert.Throws<ArgumentNullException>(() => EosCredentials.Resolve(null, null, out _));
        }
    }
}
