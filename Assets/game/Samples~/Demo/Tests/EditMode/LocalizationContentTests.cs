using System.Collections.Generic;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class LocalizationContentTests
    {
        private const string TextKey = "menu.title";

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            LocalizationContent.Use(null);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void SwappingTheContentSwapsTheText()
        {
            LocalizationContent.Use(Content("A", "Game A"));
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Get(TextKey), Is.EqualTo("Game A"));
            Assert.That(Localizer.TableOf(GameLanguage.English).Get(TextKey), Is.Not.EqualTo("Game A"), "only the language the tables are for");

            LocalizationContent.Use(Content("B", "Game B"));
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Get(TextKey), Is.EqualTo("Game B"));

            LocalizationContent.Use(null);
            Assert.That(Localizer.TableOf(GameLanguage.Japanese).Get(TextKey), Is.Not.EqualTo("Game B"));
        }

        [Test]
        public void TheContentTablesComeAfterTheResourcesInTheirOrder()
        {
            var content = Content("A", "first");
            var second = Track(new TextAsset("{ \"" + TextKey + "\": \"second\" }"));
            second.name = "menu-2";
            content.SetTexts(new[]
            {
                new GameContentTextTables(" ja ", new[] { content.TextsFor("ja")[0], null, second }),
                new GameContentTextTables("en", null),
                null,
            });

            var layers = Localizer.ReadContentLayers(content, GameLanguage.Japanese);

            Assert.That(content.TextsFor("ja"), Is.EqualTo(new[] { content.Texts[0].Tables[0], second }));
            Assert.That(content.TextsFor("en"), Is.Empty);
            Assert.That(layers.Count, Is.EqualTo(2));
            Assert.That(layers[0].Key, Is.EqualTo("LocalizationContent 'A' ja/menu"));
            Assert.That(layers[1].Value, Does.Contain("second"));
            Assert.That(Localizer.ReadContentLayers(null, GameLanguage.Japanese), Is.Empty);
        }

        private LocalizationContent Content(string name, string title)
        {
            var content = Track(ScriptableObject.CreateInstance<LocalizationContent>());
            content.name = name;
            var table = Track(new TextAsset("{ \"" + TextKey + "\": \"" + title + "\" }"));
            table.name = "menu";
            content.SetTexts(new[] { new GameContentTextTables(GameLanguages.Code(GameLanguage.Japanese), new[] { table }) });
            return content;
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
