using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;

namespace Fps.Tests.EditMode
{
    /// <summary><see cref="GameRulesDebugPanel"/> の項目の見出しが、どの言語の表にもあることを確かめる。</summary>
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
