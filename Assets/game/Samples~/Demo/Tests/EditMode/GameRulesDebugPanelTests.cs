using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;

namespace Shooter.Tests.EditMode
{
    public sealed class GameRulesDebugPanelTests
    {
        [Test]
        public void EveryFieldHasALabelInEveryLanguage()
        {
            foreach (var language in GameLanguages.All)
            {
                var table = Localizer.TableOf(language);
                foreach (var field in GameRulesDebugDraft.Fields)
                {
                    var key = GameRulesDebugPanel.FieldLabelKey(field);
                    Assert.That(table.Contains(key), Is.True, $"{GameLanguages.Code(language)} has no '{key}'");
                }
            }
        }

        [Test]
        public void LabelKeysAreNamedAfterTheField()
        {
            Assert.That(GameRulesDebugPanel.FieldLabelKey(GameRulesDebugField.SlowMotion), Is.EqualTo("debug.rules.field.SlowMotion"));
        }
    }
}
