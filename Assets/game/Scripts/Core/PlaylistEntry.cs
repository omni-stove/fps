using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class PlaylistEntry
    {
        // 引数名は playlists.json の項目名と同じにする。SpecJsonReader が ArgumentException の ParamName で断った項目を示すため。
        public PlaylistEntry(string scene, string rules = null, IEnumerable<string> mutators = null)
        {
            if (string.IsNullOrWhiteSpace(scene))
            {
                throw new ArgumentException("a playlist entry needs the scene of its mode", nameof(scene));
            }

            var ids = new List<string>();
            if (mutators != null)
            {
                foreach (var id in mutators)
                {
                    if (string.IsNullOrWhiteSpace(id))
                    {
                        throw new ArgumentException("a mutator id in a playlist entry must not be empty", nameof(mutators));
                    }

                    ids.Add(id);
                }
            }

            Scene = scene;
            Rules = string.IsNullOrWhiteSpace(rules) ? string.Empty : rules;
            MutatorIds = ids.AsReadOnly();
        }

        public string Scene { get; }

        public string Rules { get; }

        public bool HasRules => Rules.Length > 0;

        public IReadOnlyList<string> MutatorIds { get; }
    }
}
