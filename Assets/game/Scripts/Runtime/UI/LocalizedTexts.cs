using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Fps.Runtime
{
    /// <summary>
    /// UXML に文字列表のキーで書いた文字 (<c>text="#pause.title"</c> のように <see cref="KeyPrefix"/> を頭に付けた text) を、
    /// 今の言語の文字 (<see cref="Localizer"/>) にする。<see cref="Bind"/> で木からキーを集めて書き換え、
    /// 言語が変わったら持ち主が <see cref="Refresh"/> で書き直す (書き換えた後の text からはキーが読めないので、キーはここで持つ)。
    /// </summary>
    public sealed class LocalizedTexts
    {
        /// <summary>UXML の text をキーとして読む印。</summary>
        public const string KeyPrefix = "#";

        private readonly List<KeyValuePair<TextElement, string>> _entries;

        private LocalizedTexts(List<KeyValuePair<TextElement, string>> entries)
        {
            _entries = entries;
        }

        /// <summary>書き換えている要素の数。</summary>
        public int Count => _entries.Count;

        /// <summary>
        /// root (root 自身を含む) の下で text が <see cref="KeyPrefix"/> で始まる TextElement (Label・Button など) を集め、今の言語の文字にする。
        /// </summary>
        /// <exception cref="ArgumentNullException">root が null。</exception>
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

        /// <summary>text が <see cref="KeyPrefix"/> の付いたキーなら、印を除いたキーを key に入れて true。</summary>
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

        /// <summary>集めた要素を今の言語の文字で書き直す。</summary>
        public void Refresh()
        {
            foreach (var entry in _entries)
            {
                entry.Key.text = Localizer.Get(entry.Value);
            }
        }

        /// <summary>element に書いた文字のキー。集めていない要素なら null。</summary>
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
