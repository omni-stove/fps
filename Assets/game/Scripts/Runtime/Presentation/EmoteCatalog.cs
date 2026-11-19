using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "EmoteCatalog", menuName = "Shooter/Emote Catalog")]
    public sealed class EmoteCatalog : ScriptableObject
    {
        public const string CatalogName = "Emote";

        public static readonly string ResourcePath = PresentationCatalog.CatalogResourcePath(CatalogName);

        [SerializeField, Tooltip("エモートの Id とアニメーションの組")]
        private List<Entry> _animations = new List<Entry>();

        public IReadOnlyList<Entry> Animations => _animations ??= new List<Entry>();

        public static EmoteCatalog Load()
        {
            var fromContent = GameContent.Pick(c => c.EmoteAnimations);
            return fromContent != null ? fromContent : Resources.Load<EmoteCatalog>(ResourcePath);
        }

        public AnimationClip Find(string emoteId)
        {
            if (string.IsNullOrEmpty(emoteId))
            {
                return null;
            }

            foreach (var entry in Animations)
            {
                if (entry != null && string.Equals(entry.EmoteId, emoteId, StringComparison.Ordinal))
                {
                    return entry.Clip;
                }
            }

            return null;
        }

        public void Add(string emoteId, AnimationClip clip)
        {
            if (string.IsNullOrWhiteSpace(emoteId))
            {
                throw new ArgumentException("an emote id from emotes.json is required", nameof(emoteId));
            }

            _animations ??= new List<Entry>();
            _animations.Add(new Entry { EmoteId = emoteId.Trim(), Clip = clip });
        }

        [Serializable]
        public sealed class Entry
        {
            [SerializeField, Tooltip("emotes.json のエモートの id")]
            private string _emoteId;

            [SerializeField, Tooltip("リグの Animator に流すクリップ。繰り返すエモートでは繰り返しのクリップにする")]
            private AnimationClip _clip;

            public string EmoteId
            {
                get => _emoteId;
                set => _emoteId = value;
            }

            public AnimationClip Clip
            {
                get => _clip;
                set => _clip = value;
            }
        }
    }
}
