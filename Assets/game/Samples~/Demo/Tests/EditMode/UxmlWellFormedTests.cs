using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Shooter.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class UxmlWellFormedTests
    {
        private static IEnumerable<string> SampleUxmlPaths()
        {
            var root = Path.Combine(Path.GetDirectoryName(Application.dataPath), DemoSample.Root);
            return Directory.GetFiles(root, "*.uxml", SearchOption.AllDirectories)
                .Select(p => p.Replace('\\', '/'))
                .OrderBy(p => p, System.StringComparer.Ordinal);
        }

        [Test]
        public void TheSampleHasUxmlFilesToCheck()
        {
            Assert.That(SampleUxmlPaths(), Has.Some.EndsWith("UI/Resources/Hud/Hud.uxml"));
        }

        [TestCaseSource(nameof(SampleUxmlPaths))]
        public void EveryUxmlIsWellFormedXml(string path)
        {
            Assert.DoesNotThrow(() => new XmlDocument().Load(path), $"{path} must parse as XML or Unity imports it as an empty layout");
        }

        [Test]
        public void ACommentWithADoubleHyphenIsRejected()
        {
            var document = new XmlDocument();

            Assert.Throws<XmlException>(() => document.LoadXml("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"><!-- a hud__crosshair--blocked class --></ui:UXML>"));
        }
    }
}
