using System.Collections.Generic;

namespace Fps.Server
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var settings = ServerMotorSettings.Default;
            var movement = new ServerMovement(settings);
            var snapshots = new ServerSnapshots(settings.Rules, new ISnapshotContributor[] { movement });
            return ServerProgram.Run(
                args,
                (ServerLaunch launch, ServerRuntime runtime, ICollection<IServerModule> modules, out string error) =>
                {
                    modules.Add(movement);
                    modules.Add(snapshots);
                    return ServerModules.Create(launch, runtime, modules, out error);
                },
                ServerRuntime.Process());
        }
    }
}
