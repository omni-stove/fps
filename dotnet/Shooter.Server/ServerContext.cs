using System;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerContext
    {

        public ServerContext(
            NetLaunchOptions options,
            string version,
            TextWriter log,
            ServerMapData map,
            ServerMatch match,
            ServerMessenger messenger,
            IServerControl control,
            ServerRulesData rules = null,
            ServerBodies bodies = null,
            IConnectionUserIds userIds = null)
        {
            UserIds = userIds;
            Rules = rules;
            Bodies = bodies ?? new ServerBodies((rules?.Motor ?? NetMotorSettings.Default).Stance);
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Version = version;
            Log = log ?? throw new ArgumentNullException(nameof(log));
            Map = map ?? throw new ArgumentNullException(nameof(map));
            Match = match ?? throw new ArgumentNullException(nameof(match));
            Messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            Control = control ?? throw new ArgumentNullException(nameof(control));
        }

        public NetLaunchOptions Options { get; }

        public string Version { get; }

        public TextWriter Log { get; }

        public ServerMapData Map { get; }

        public IWorldCollision World => Map.World;

        public INavigation Navigation => Map.Navigation;

        public ServerMatch Match { get; }

        public ServerMessenger Messenger { get; }

        public ServerRulesData Rules { get; }

        public ServerBodies Bodies { get; }

        public IServerControl Control { get; }

        public IConnectionUserIds UserIds { get; }
    }
}
