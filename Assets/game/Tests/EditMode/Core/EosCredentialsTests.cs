using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
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

        private static Func<string, string> Lookup(Dictionary<string, string> values)
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
        public void TheEncryptionKeyIsOptional()
        {
            var credentials = EosCredentials.Resolve(Lookup(AllValues("env-")), null, out var missing);

            Assert.That(missing, Is.Empty);
            Assert.That(credentials.EncryptionKey, Is.Null);
            Assert.That(credentials.HasEncryptionKey, Is.False);
            Assert.That(EosCredentials.VariableNames, Does.Not.Contain(EosCredentials.EncryptionKeyVariable));
        }

        [Test]
        public void TheEncryptionKeyIsReadFromTheEnvironmentBeforeTheLocalFile()
        {
            var fileKey = new string('a', EosCredentials.EncryptionKeyLength);
            var environmentKey = "  " + new string('B', EosCredentials.EncryptionKeyLength) + "\n";
            var file = AllValues("file-");
            file[EosCredentials.EncryptionKeyVariable] = fileKey;

            var fromFile = EosCredentials.Resolve(_emptyEnvironment, file, out _);
            var environment = new Dictionary<string, string> { { EosCredentials.EncryptionKeyVariable, environmentKey } };
            var fromEnvironment = EosCredentials.Resolve(Lookup(environment), file, out _);

            Assert.That(fromFile.EncryptionKey, Is.EqualTo(fileKey));
            Assert.That(fromFile.HasEncryptionKey, Is.True);
            Assert.That(fromEnvironment.EncryptionKey, Is.EqualTo(new string('B', EosCredentials.EncryptionKeyLength)));
            Assert.That(fromEnvironment.HasEncryptionKey, Is.True);
        }

        [Test]
        public void AnEncryptionKeyOfTheWrongShapeIsKeptButNotUsable()
        {
            var credentials = new EosCredentials("p", "s", "d", "c", "x", "not-hex");

            Assert.That(credentials.EncryptionKey, Is.EqualTo("not-hex"));
            Assert.That(credentials.HasEncryptionKey, Is.False);
        }

        [Test]
        public void TellsAValidEncryptionKey()
        {
            Assert.That(EosCredentials.IsValidEncryptionKey("0123456789abcdefABCDEF0123456789abcdefABCDEF0123456789abcdef0123"), Is.True);
            Assert.That(EosCredentials.IsValidEncryptionKey(new string('0', 63)), Is.False);
            Assert.That(EosCredentials.IsValidEncryptionKey(new string('0', 65)), Is.False);
            Assert.That(EosCredentials.IsValidEncryptionKey(new string('g', 64)), Is.False);
            Assert.That(EosCredentials.IsValidEncryptionKey(null), Is.False);
        }

        [Test]
        public void HidesTheEncryptionKeyInToString()
        {
            var credentials = new EosCredentials("p", "s", "d", "c", "x", new string('f', EosCredentials.EncryptionKeyLength));

            Assert.That(credentials.ToString(), Does.Not.Contain("ffff"));
        }

        [Test]
        public void EpicAccountsAreOffUnlessSwitchedOn()
        {
            var credentials = EosCredentials.Resolve(Lookup(AllValues("")), null, out _);

            Assert.That(credentials.AccountLogin, Is.SameAs(EosAccountLoginSettings.Disabled));
            Assert.That(EosCredentials.VariableNames, Does.Not.Contain(EosAccountLoginSettings.EpicAccountLoginVariable));
            Assert.That(new EosCredentials("p", "s", "d", "c", "x").AccountLogin, Is.SameAs(EosAccountLoginSettings.Disabled));
        }

        [Test]
        public void TheEpicAccountSwitchAndDevAuthToolAreReadFromTheEnvironmentBeforeTheLocalFile()
        {
            var file = AllValues("file-");
            file[EosAccountLoginSettings.EpicAccountLoginVariable] = "false";
            file[EosAccountLoginSettings.EpicDevAuthVariable] = "localhost:6547/FromFile";
            var environment = new Dictionary<string, string>
            {
                { EosAccountLoginSettings.EpicAccountLoginVariable, "true" },
                { EosAccountLoginSettings.EpicDevAuthVariable, "localhost:6547/FromEnvironment" },
            };

            var credentials = EosCredentials.Resolve(Lookup(environment), file, out _);

            Assert.That(credentials.AccountLogin.EpicAccount, Is.True);
            Assert.That(credentials.AccountLogin.DevAuthHost, Is.EqualTo("localhost:6547"));
            Assert.That(credentials.AccountLogin.DevAuthCredential, Is.EqualTo("FromEnvironment"));
        }

        [Test]
        public void AnUnreadableEpicAccountSwitchStillGivesCredentials()
        {
            var values = AllValues("");
            values[EosAccountLoginSettings.EpicAccountLoginVariable] = "sometimes";

            var credentials = EosCredentials.Resolve(Lookup(values), null, out var missing);

            Assert.That(missing, Is.Empty);
            Assert.That(credentials.AccountLogin.EpicAccount, Is.False);
            Assert.That(credentials.AccountLogin.Problem, Is.EqualTo("EOS_EPIC_ACCOUNT_LOGIN must be true or false"));
        }

        [Test]
        public void RejectsNullLookup()
        {
            Assert.Throws<ArgumentNullException>(() => EosCredentials.Resolve(null, null, out _));
        }
    }
}
