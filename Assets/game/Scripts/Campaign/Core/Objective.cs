using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Shooter.Core
{
    public enum ObjectiveState
    {
        Locked,

        Active,

        Completed,

        Failed,
    }

    public sealed class Objective
    {
        public string Id { get; }

        // 表の文には数を直に書かず RequiredCount を差し込む。定義の数を変えたときに説明だけ古いまま残らないようにするため。
        public string DescriptionKey { get; }

        public int RequiredCount { get; }

        public bool Optional { get; }

        public IReadOnlyList<string> Prerequisites { get; }

        public Objective(
            string id,
            string descriptionKey,
            int requiredCount = 1,
            bool optional = false,
            IEnumerable<string> prerequisites = null)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (id.Length == 0)
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (requiredCount <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(requiredCount), requiredCount, "requiredCount must be positive");
            }

            var prerequisiteList = new List<string>();
            if (prerequisites != null)
            {
                foreach (var prerequisite in prerequisites)
                {
                    if (string.IsNullOrEmpty(prerequisite))
                    {
                        throw new ArgumentException("prerequisites must not contain null or empty ids", nameof(prerequisites));
                    }

                    if (!prerequisiteList.Contains(prerequisite))
                    {
                        prerequisiteList.Add(prerequisite);
                    }
                }
            }

            Id = id;
            DescriptionKey = descriptionKey ?? string.Empty;
            RequiredCount = requiredCount;
            Optional = optional;
            Prerequisites = new ReadOnlyCollection<string>(prerequisiteList);
        }
    }
}
