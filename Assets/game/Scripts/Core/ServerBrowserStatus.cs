using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    public enum ServerBrowserStage
    {
        Loading,
        Ready,
        Unavailable,
    }

    public enum ServerJoinOutcome
    {
        Joined,
        NotFound,
        Full,
        Failed,
    }

    // 一覧は検索の返したままの並びで持ち、絞り込みと並べ替えは見る側が ServerListQuery で行う。
    public readonly struct ServerBrowserStatus : IEquatable<ServerBrowserStatus>
    {
        // 渡された一覧を後から書き換えられても変わらないよう写しを持つ。default ではコンストラクタを通らず null になるので、読むときに空へ寄せる。
        private readonly ServerListing[] _servers;

        public ServerBrowserStatus(ServerBrowserStage stage, IReadOnlyList<ServerListing> servers)
        {
            if (!Enum.IsDefined(typeof(ServerBrowserStage), stage))
            {
                throw new ArgumentOutOfRangeException(nameof(stage), stage, "is not a defined ServerBrowserStage");
            }

            Stage = stage;
            var copy = new List<ServerListing>(servers?.Count ?? 0);
            if (servers != null)
            {
                foreach (var server in servers)
                {
                    if (server != null)
                    {
                        copy.Add(server);
                    }
                }
            }

            _servers = copy.ToArray();
        }

        public ServerBrowserStage Stage { get; }

        public IReadOnlyList<ServerListing> Servers => _servers ?? Array.Empty<ServerListing>();

        public ServerListing Find(string id)
        {
            foreach (var server in Servers)
            {
                if (string.Equals(server.Id, id, StringComparison.Ordinal))
                {
                    return server;
                }
            }

            return null;
        }

        public bool Equals(ServerBrowserStatus other)
        {
            if (Stage != other.Stage)
            {
                return false;
            }

            var mine = Servers;
            var theirs = other.Servers;
            if (mine.Count != theirs.Count)
            {
                return false;
            }

            for (var i = 0; i < mine.Count; i++)
            {
                if (!mine[i].Equals(theirs[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is ServerBrowserStatus other && Equals(other);
        }

        public static bool operator ==(ServerBrowserStatus left, ServerBrowserStatus right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ServerBrowserStatus left, ServerBrowserStatus right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            var hash = Stage.GetHashCode();
            foreach (var server in Servers)
            {
                hash = HashCode.Combine(hash, server);
            }

            return hash;
        }

        public override string ToString()
        {
            return Stage + " servers " + Servers.Count.ToString(CultureInfo.InvariantCulture);
        }
    }
}
