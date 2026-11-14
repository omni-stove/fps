using System;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed class GrappleSurfaceFilter
    {
        private readonly int _layers;
        private readonly string[] _tags;

        public GrappleSurfaceFilter(LayerMask layers, string[] tags)
        {
            _layers = layers.value;
            _tags = tags != null ? (string[])tags.Clone() : Array.Empty<string>();
            for (var i = 0; i < _tags.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(_tags[i]))
                {
                    throw new ArgumentException($"tag {i} is empty", nameof(tags));
                }
            }
        }

        public int Layers => _layers;

        public int TagCount => _tags.Length;

        // CompareTag はプロジェクトに無いタグを渡すとエラーを出す。ゲームが消したタグがアセットに残っていても、掴めないだけで済ませる。
        public bool Allows(GameObject surface)
        {
            if (surface == null || (_layers & (1 << surface.layer)) == 0)
            {
                return false;
            }

            if (_tags.Length == 0)
            {
                return true;
            }

            var tag = surface.tag;
            for (var i = 0; i < _tags.Length; i++)
            {
                if (string.Equals(_tags[i], tag, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
