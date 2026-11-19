using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class KeySharingTuningFileTests
    {
        [TearDown]
        public void TearDown()
        {
            KitTuning.Reload();
        }

        [Test]
        public void TheBundledFileLetsMeleeAndReloadShareAKeyWithInteract()
        {
            var asset = Resources.Load<TextAsset>(KeySharingJson.ResourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/Tuning/key-sharing.json");

            var spec = KeySharingJson.Parse(asset.text, "Resources/Tuning/key-sharing.json");

            Assert.That(spec.Shared.Count, Is.EqualTo(2));
            Assert.That(spec.Allows("Melee", "Interact"), Is.True);
            Assert.That(spec.Allows("Reload", "Interact"), Is.True);
            Assert.That(spec.Allows("Melee", "Reload"), Is.False);
            Assert.That(spec.YieldsTo("Melee"), Is.EqualTo(new[] { "Interact" }));
            Assert.That(spec.YieldsTo("Reload"), Is.EqualTo(new[] { "Interact" }));
            Assert.That(spec.YieldsTo("Interact"), Is.Empty);
        }

        [Test]
        public void TheBundledActionsAreInThePlayerMap()
        {
            var spec = KeySharingJson.Parse(Resources.Load<TextAsset>(KeySharingJson.ResourcePath).text, "key-sharing.json");
            var asset = ProjectActionsClone.Create();
            try
            {
                var map = asset.FindActionMap(PlayerBindings.MapName, true);
                foreach (var rule in spec.Shared)
                {
                    foreach (var action in rule.Actions)
                    {
                        Assert.That(map.FindAction(action), Is.Not.Null, action);
                    }
                }
            }
            finally
            {
                ProjectActionsClone.Destroy(asset);
            }
        }

        [Test]
        public void KitTuningReadsTheBundledFile()
        {
            KitTuning.Reload();

            Assert.That(KitTuning.KeySharing.Allows("Melee", "Interact"), Is.True);
            Assert.That(KitTuning.KeySharing.Shared[0].Prefer, Is.EqualTo("Interact"));
        }
    }
}
