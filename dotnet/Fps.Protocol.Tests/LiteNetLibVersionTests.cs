using System.IO;
using System.Reflection;
using LiteNetLib;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    public sealed class LiteNetLibVersionTests
    {
        /// <summary>版がずれるとクライアント (Unity) とサーバー (.NET) で通信の手順が食い違うので、片方だけ上げたら落とす。</summary>
        [Test]
        public void UnityPluginMatchesTheNuGetPackage()
        {
            var plugin = Path.Combine(RepositoryRoot(), "Assets", "Plugins", "LiteNetLib", "LiteNetLib.dll");

            var unityVersion = AssemblyName.GetAssemblyName(plugin).Version;
            var dotnetVersion = typeof(LiteNetManager).Assembly.GetName().Version;

            Assert.That(unityVersion, Is.EqualTo(dotnetVersion));
        }

        private static string RepositoryRoot()
        {
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "Assets")))
            {
                directory = directory.Parent;
            }

            Assert.That(directory, Is.Not.Null, "no Assets folder above " + TestContext.CurrentContext.TestDirectory);
            return directory.FullName;
        }
    }
}
