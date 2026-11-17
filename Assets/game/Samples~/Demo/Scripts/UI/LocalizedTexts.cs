using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Shooter.Runtime
{
    // 書き換えた後の text からはキーが読めないので、キーはここで持つ。
    public sealed class LocalizedTexts
    {
        public const string KeyPrefix = "#";

        private readonly List<KeyValuePair<TextElement, string>> _entries;

        private LocalizedTexts(List<KeyValuePair<TextElement, string>> entries)
        {
            _entries = entries;
        }

        public int Count => _entries.Count;

        public static LocalizedTexts Bind(VisualElement root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            var entries = new List<KeyValuePair<TextElement, string>>();
            root.Query<TextElement>().ForEach(element =>
            {
                if (TryReadKey(element.text, out var key))
                {
                    entries.Add(new KeyValuePair<TextElement, string>(element, key));
                }
            });

            var texts = new LocalizedTexts(entries);
            texts.Refresh();
            return texts;
        }

        public static bool TryReadKey(string text, out string key)
        {
            if (text != null && text.Length > KeyPrefix.Length && text.StartsWith(KeyPrefix, StringComparison.Ordinal))
            {
                key = text.Substring(KeyPrefix.Length);
                return true;
            }

            key = null;
            return false;
        }

        public void Refresh()
        {
            foreach (var entry in _entries)
            {
                entry.Key.text = Localizer.Get(entry.Value);
            }
        }

        public string KeyOf(TextElement element)
        {
            foreach (var entry in _entries)
            {
                if (entry.Key == element)
                {
                    return entry.Value;
                }
            }

            return null;
        }
    }
}
