using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Fps.Core
{
    /// <summary>ミッションの目標の状態。</summary>
    public enum ObjectiveState
    {
        /// <summary>前提の目標が未完了で、まだ進められない。</summary>
        Locked,

        /// <summary>進められる。</summary>
        Active,

        /// <summary>必要な数に届いて完了した。</summary>
        Completed,

        /// <summary>失敗した。</summary>
        Failed,
    }

    /// <summary>
    /// ミッションの目標の定義。不変で、進み具合は <see cref="Mission"/> が持つ。
    /// </summary>
    public sealed class Objective
    {
        /// <summary>ミッション内で一意な識別子。</summary>
        public string Id { get; }

        /// <summary>プレイヤーに見せる説明。</summary>
        public string Description { get; }

        /// <summary>完了までに必要な進み具合。1 以上。</summary>
        public int RequiredCount { get; }

        /// <summary>任意の目標なら true。ミッションの成否に影響しない。</summary>
        public bool Optional { get; }

        /// <summary>この目標を進める前に完了している必要がある目標の Id。</summary>
        public IReadOnlyList<string> Prerequisites { get; }

        /// <param name="id">ミッション内で一意な識別子。空にはできない。</param>
        /// <param name="description">プレイヤーに見せる説明。null は空文字として扱う。</param>
        /// <param name="requiredCount">完了までに必要な進み具合。1 以上。</param>
        /// <param name="optional">任意の目標なら true。</param>
        /// <param name="prerequisites">先に完了している必要がある目標の Id。null なら前提なし。</param>
        public Objective(
            string id,
            string description,
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
            Description = description ?? string.Empty;
            RequiredCount = requiredCount;
            Optional = optional;
            Prerequisites = new ReadOnlyCollection<string>(prerequisiteList);
        }
    }
}
