using NUnit.Framework;
using Shooter.Runtime;

namespace Shooter.Tests.EditMode
{
    public sealed class LoadoutPreferenceLogTests
    {
        [Test]
        public void ThePresetLinesStartWithTheLoadoutTag()
        {
            Assert.That(LoadoutPreferenceLog.Unreadable("Key", "broken"), Is.EqualTo(
                "[Loadout] saved loadout 'Key': broken; choosing from the match's loadout instead"));
            Assert.That(LoadoutPreferenceLog.PresetsDropped("Key", 2, 3), Is.EqualTo(
                "[Loadout] saved loadout 'Key': 2 presets beyond the limit of 3 are not kept"));
            Assert.That(LoadoutPreferenceLog.PresetSwitched(1, 3), Is.EqualTo("[Loadout] switched to preset 1 of 3"));
            Assert.That(LoadoutPreferenceLog.PresetAdded(2, 3, 5), Is.EqualTo("[Loadout] saved a new preset 2 (3 of at most 5)"));
            Assert.That(LoadoutPreferenceLog.PresetRenamed(0), Is.EqualTo("[Loadout] renamed preset 0"));
            Assert.That(LoadoutPreferenceLog.PresetsNotSaved(null), Is.EqualTo("[Loadout] could not save the presets: -"));
        }
    }
}
