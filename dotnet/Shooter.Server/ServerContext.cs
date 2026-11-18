using System;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    internal sealed class ServerContext
    {
        private readonly ServerMapData _startMap;

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
            IConnectionUserIds userIds = null,
            IConnectionAddresses addresses = null)
        {
            UserIds = userIds;
            Addresses = addresses;
            Rules = rules;
            Bodies = bodies ?? new ServerBodies((rules?.Motor ?? NetMotorSettings.Default).Stance);
            Options = options ?? throw new ArgumentNullException(nameof(options));
            Version = version;
            Log = log ?? throw new ArgumentNullException(nameof(log));
            _startMap = map ?? throw new ArgumentNullException(nameof(map));
            Match = match ?? throw new ArgumentNullException(nameof(match));
            Messenger = messenger ?? throw new ArgumentNullException(nameof(messenger));
            Control = control ?? throw new ArgumentNullException(nameof(control));
        }

        public NetLaunchOptions Options { get; }

        public string Version { get; }

        public TextWriter Log { get; }

        public ServerMapData Map => Match.Map ?? _startMap;

        public IWorldCollision World => Map.World;

        public INavigation Navigation => Map.Navigation;

        public ServerMatch Match { get; }

        public ServerMessenger Messenger { get; }

        public ServerRulesData Rules { get; }

        public ServerBodies Bodies { get; }

        public IServerControl Control { get; }

        public IConnectionUserIds UserIds { get; }

        public IConnectionAddresses Addresses { get; }
    }
}
