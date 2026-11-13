using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ロビー 1 つの、ある時点の状態。ID はどれも EOS が返す文字列のまま持ち、SDK の型には依存しない。
    /// </summary>
    public sealed class LobbyInfo
    {
        private readonly string[] _members;
        private readonly LobbyAttribute[] _attributes;

        /// <param name="id">ロビーの ID。</param>
        /// <param name="ownerUserId">オーナーの ProductUserId。</param>
        /// <param name="maxMembers">参加できる最大人数 (1 以上)。</param>
        /// <param name="members">参加中のメンバーの ProductUserId。空・重複は取り除く。</param>
        public LobbyInfo(string id, string ownerUserId, int maxMembers, IReadOnlyList<string> members)
            : this(id, ownerUserId, maxMembers, members, Array.Empty<LobbyAttribute>())
        {
        }

        /// <param name="id">ロビーの ID。</param>
        /// <param name="ownerUserId">オーナーの ProductUserId。</param>
        /// <param name="maxMembers">参加できる最大人数 (1 以上)。</param>
        /// <param name="members">参加中のメンバーの ProductUserId。空・重複は取り除く。</param>
        /// <param name="attributes">公開の属性。null の要素は取り除き、同じキーは先のものを残す。</param>
        public LobbyInfo(
            string id, string ownerUserId, int maxMembers, IReadOnlyList<string> members, IReadOnlyList<LobbyAttribute> attributes)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("must not be null or blank", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(ownerUserId))
            {
                throw new ArgumentException("must not be null or blank", nameof(ownerUserId));
            }

            if (maxMembers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxMembers), maxMembers, "must be at least 1");
            }

            if (members == null)
            {
                throw new ArgumentNullException(nameof(members));
            }

            if (attributes == null)
            {
                throw new ArgumentNullException(nameof(attributes));
            }

            var uniqueAttributes = new List<LobbyAttribute>(attributes.Count);
            foreach (var attribute in attributes)
            {
                if (attribute == null || Find(uniqueAttributes, attribute.Key) != null)
                {
                    continue;
                }

                uniqueAttributes.Add(attribute);
            }

            _attributes = uniqueAttributes.ToArray();

            var unique = new List<string>(members.Count);
            foreach (var member in members)
            {
                if (string.IsNullOrWhiteSpace(member) || Contains(unique, member))
                {
                    continue;
                }

                unique.Add(member);
            }

            Id = id;
            OwnerUserId = ownerUserId;
            MaxMembers = maxMembers;
            _members = unique.ToArray();
        }

        public string Id { get; }

        public string OwnerUserId { get; }

        public int MaxMembers { get; }

        /// <summary>参加中のメンバーの ProductUserId。</summary>
        public IReadOnlyList<string> Members => _members;

        public int MemberCount => _members.Length;

        /// <summary>あと何人参加できるか。</summary>
        public int FreeSlots => Math.Max(0, MaxMembers - _members.Length);

        /// <summary>公開の属性。検索で読んだときだけ入り、無ければ空。</summary>
        public IReadOnlyList<LobbyAttribute> Attributes => _attributes;

        /// <summary>文字列の属性 <paramref name="key"/> を読む。無い・整数の属性なら false。</summary>
        public bool TryGetText(string key, out string value)
        {
            var attribute = Find(_attributes, key);
            value = attribute != null && attribute.IsText ? attribute.Text : null;
            return value != null;
        }

        /// <summary>整数の属性 <paramref name="key"/> を読む。無い・文字列の属性なら false。</summary>
        public bool TryGetNumber(string key, out long value)
        {
            var attribute = Find(_attributes, key);
            var found = attribute != null && !attribute.IsText;
            value = found ? attribute.Number : 0;
            return found;
        }

        /// <summary><paramref name="userId"/> がオーナーか。ID の大文字・小文字は区別しない。</summary>
        public bool IsOwnedBy(string userId)
        {
            return userId != null && string.Equals(OwnerUserId, userId, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary><paramref name="userId"/> がメンバーか。ID の大文字・小文字は区別しない。</summary>
        public bool HasMember(string userId)
        {
            return userId != null && Contains(_members, userId);
        }

        /// <summary>ログ向けのメンバーの一覧。<c>2/8 [id1,id2]</c> の形。</summary>
        public string DescribeMembers()
        {
            return MemberCount.ToString(CultureInfo.InvariantCulture) + "/"
                + MaxMembers.ToString(CultureInfo.InvariantCulture) + " [" + string.Join(",", _members) + "]";
        }

        public override string ToString()
        {
            return "id=" + Id + " owner=" + OwnerUserId + " members=" + DescribeMembers();
        }

        private static LobbyAttribute Find(IEnumerable<LobbyAttribute> attributes, string key)
        {
            if (key == null)
            {
                return null;
            }

            foreach (var attribute in attributes)
            {
                if (string.Equals(attribute.Key, key, StringComparison.Ordinal))
                {
                    return attribute;
                }
            }

            return null;
        }

        private static bool Contains(IEnumerable<string> userIds, string userId)
        {
            foreach (var candidate in userIds)
            {
                if (string.Equals(candidate, userId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
