using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    // ID を EOS が返す文字列のまま持つのは、Core を EOS SDK の型に依存させないため。
    public sealed class LobbyInfo
    {
        private readonly string[] _members;
        private readonly LobbyAttribute[] _attributes;

        public LobbyInfo(string id, string ownerUserId, int maxMembers, IReadOnlyList<string> members)
            : this(id, ownerUserId, maxMembers, members, Array.Empty<LobbyAttribute>())
        {
        }

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

        public IReadOnlyList<string> Members => _members;

        public int MemberCount => _members.Length;

        public int FreeSlots => Math.Max(0, MaxMembers - _members.Length);

        public IReadOnlyList<LobbyAttribute> Attributes => _attributes;

        public bool TryGetText(string key, out string value)
        {
            var attribute = Find(_attributes, key);
            value = attribute != null && attribute.IsText ? attribute.Text : null;
            return value != null;
        }

        public bool TryGetNumber(string key, out long value)
        {
            var attribute = Find(_attributes, key);
            var found = attribute != null && !attribute.IsText;
            value = found ? attribute.Number : 0;
            return found;
        }

        public bool IsPrivate => TryGetNumber(ServerLobby.PrivateKey, out var value) && value != 0;

        public string JoinCode => TryGetText(ServerLobby.JoinCodeKey, out var value) ? value : null;

        public int? SkillAverage =>
            TryGetNumber(ServerLobby.SkillAverageKey, out var value) && value >= int.MinValue && value <= int.MaxValue
                ? (int)value
                : (int?)null;

        public string Region => TryGetText(ServerLobby.RegionKey, out var value) ? value : null;

        public string Mode => TryGetText(ServerLobby.ModeKey, out var value) ? value : null;

        public string Map => TryGetText(ServerLobby.MapKey, out var value) ? value : null;

        public bool VoiceEnabled => TryGetNumber(ServerLobby.VoiceEnabledKey, out var value) && value != 0;

        public bool IsOwnedBy(string userId)
        {
            return userId != null && string.Equals(OwnerUserId, userId, StringComparison.OrdinalIgnoreCase);
        }

        public bool HasMember(string userId)
        {
            return userId != null && Contains(_members, userId);
        }

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
