namespace Fps.Server
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            var settings = ServerMotorSettings.Default;
            var movement = new ServerMovement(settings);
            var snapshots = new ServerSnapshots(settings.Rules, new ISnapshotContributor[] { movement });
            return ServerProgram.Run(args, new IServerModule[] { movement, snapshots }, ServerRuntime.Process());
        }
    }
}
