using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NUnit.Framework;
using UnityEditor;

namespace Shooter.Tests.EditMode
{
    public sealed class UxmlSyntaxTests
    {
        // Unity の UXML インポーターは XML の誤りを Console に出すだけで読み込みを続けるので、読み込めるかではなく XML として通るかを見る。
        [Test]
        public void EveryUxmlUnderAssetsIsWellFormedXml()
        {
            var paths = AssetDatabase.FindAssets("t:VisualTreeAsset", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".uxml"))
                .ToArray();

            Assert.That(paths, Does.Contain("Assets/Samples/ShooterKitDemo/UI/Resources/Hud/Hud.uxml"));
            foreach (var path in paths)
            {
                Assert.DoesNotThrow(() => XDocument.Load(path), path);
            }
        }

        [Test]
        public void ADoubleHyphenInsideAUxmlCommentIsRejected()
        {
            Assert.Throws<XmlException>(() => XDocument.Parse("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"><!-- stamina--depleted --></ui:UXML>"));
        }
    }
}
