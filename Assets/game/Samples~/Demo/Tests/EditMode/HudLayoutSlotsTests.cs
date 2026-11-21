using System.Linq;
using System.Xml.Linq;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Shooter.Tests.EditMode
{
    public sealed class HudLayoutSlotsTests
    {
        private static readonly string[] _hiddenSlots = { "vote-kick", "wallet", "vision-overlay", "drone-hud", "nameplates" };

        [TestCaseSource(nameof(_hiddenSlots))]
        public void TheHudLayoutHasAHiddenEmptySlot(string slotName)
        {
            var layout = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath);
            Assert.That(layout, Is.Not.Null, "Resources/" + Hud.LayoutResourcePath + ".uxml is missing");
            var root = layout.CloneTree();

            var slot = root.Q<VisualElement>(slotName);

            Assert.That(slot, Is.Not.Null, slotName);
            Assert.That(root.Query<VisualElement>(slotName).ToList(), Has.Count.EqualTo(1), slotName);
            Assert.That(slot.ClassListContains(slotName), Is.True, "the slot is styled by the class of the same name in Hud.uss");
            Assert.That(InlineStyle(layout, slotName), Does.Contain("display: none"), "the slot stays hidden until a view shows it");
            Assert.That(slot.pickingMode, Is.EqualTo(PickingMode.Ignore), "a hidden slot never takes clicks from the game");
            Assert.That(slot.childCount, Is.EqualTo(0), "the view that shows the slot fills it");
        }

        // A cloned element's style only holds values set from code, so the UXML's inline style is read from the file.
        private static string InlineStyle(VisualTreeAsset layout, string elementName)
        {
            var element = XDocument.Load(AssetDatabase.GetAssetPath(layout))
                .Descendants()
                .Single(e => (string)e.Attribute("name") == elementName);
            return (string)element.Attribute("style") ?? string.Empty;
        }

        [Test]
        public void TheVisionOverlayIsDrawnBehindTheHudText()
        {
            var root = Resources.Load<VisualTreeAsset>(Hud.LayoutResourcePath).CloneTree();
            var hud = root.Q<VisualElement>("hud");

            var overlay = hud.IndexOf(root.Q<VisualElement>("vision-overlay"));

            Assert.That(overlay, Is.GreaterThan(hud.IndexOf(root.Q<VisualElement>("scope-overlay"))));
            Assert.That(overlay, Is.LessThan(hud.IndexOf(root.Q<VisualElement>("hud-time-panel"))));
        }
    }
}
