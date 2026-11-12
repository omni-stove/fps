using System.Collections.Generic;
using System.IO;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Net.Tests
{
    public sealed class EosCredentialSourceTests
    {
        private const string FullFile =
            "{\"EOS_PRODUCT_ID\": \"p\", \"EOS_SANDBOX_ID\": \"s\", \"EOS_DEPLOYMENT_ID\": \"d\","
            + " \"EOS_CLIENT_ID\": \"c\", \"EOS_CLIENT_SECRET\": \"file-secret\"}";

        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "fps-eos-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        private string WriteFile(string subdirectory, string content)
        {
            var directory = Path.Combine(_directory, subdirectory);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, EosCredentials.LocalFileName);
            File.WriteAllText(path, content);
            return path;
        }

        private string MissingPath()
        {
            return Path.Combine(_directory, "absent", EosCredentials.LocalFileName);
        }

        private static string NoEnvironment(string name)
        {
            return null;
        }

        [Test]
        public void NoFileAndNoEnvironmentIsMissingButNotAnError()
        {
            var ok = EosCredentialSource.TryLoad(NoEnvironment, new[] { MissingPath() }, out var credentials, out var missing, out var error);

            Assert.That(ok, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(credentials, Is.Null);
            Assert.That(missing, Is.EqualTo(EosCredentials.VariableNames));
        }

        [Test]
        public void ReadsFirstExistingFile()
        {
            var first = WriteFile("first", FullFile);
            var second = WriteFile("second", "not json");

            var ok = EosCredentialSource.TryLoad(
                NoEnvironment, new[] { MissingPath(), first, second }, out var credentials, out var missing, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(missing, Is.Empty);
            Assert.That(credentials.ClientSecret, Is.EqualTo("file-secret"));
        }

        [Test]
        public void EnvironmentOverridesFile()
        {
            var path = WriteFile("dir", FullFile);
            var environment = new Dictionary<string, string> { { EosCredentials.ClientSecretVariable, "env-secret" } };

            EosCredentialSource.TryLoad(
                name => environment.TryGetValue(name, out var value) ? value : null,
                new[] { path },
                out var credentials,
                out _,
                out _);

            Assert.That(credentials.ClientSecret, Is.EqualTo("env-secret"));
            Assert.That(credentials.ProductId, Is.EqualTo("p"));
        }

        [Test]
        public void BrokenFileIsAnErrorThatNamesTheFile()
        {
            var path = WriteFile("dir", "{\"EOS_PRODUCT_ID\": 42}");

            var ok = EosCredentialSource.TryLoad(NoEnvironment, new[] { path }, out var credentials, out _, out var error);

            Assert.That(ok, Is.False);
            Assert.That(credentials, Is.Null);
            Assert.That(error, Does.Contain(path));
        }

        [Test]
        public void ErrorsDoNotLeakSecrets()
        {
            var path = WriteFile("dir", "{\"EOS_CLIENT_SECRET\": \"top-secret\", }");

            EosCredentialSource.TryLoad(NoEnvironment, new[] { path }, out _, out _, out var error);

            Assert.That(error, Does.Not.Contain("top-secret"));
        }

        [Test]
        public void CandidatePathsEndWithLocalFileName()
        {
            var paths = EosCredentialSource.CandidatePaths();

            Assert.That(paths, Is.Not.Empty);
            Assert.That(paths, Is.Unique);
            foreach (var path in paths)
            {
                Assert.That(Path.GetFileName(path), Is.EqualTo(EosCredentials.LocalFileName));
                Assert.That(Path.IsPathRooted(path), Is.True);
            }
        }
    }
}
