using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Server.Eos;

namespace Shooter.Server
{
    // テストで標準出力・環境変数・シグナル・マップの置き場所を差し替えるために、プロセスの外とのつながりを 1 つにまとめる。
    internal sealed class ServerRuntime
    {
        public ServerRuntime(
            TextWriter output,
            TextWriter error,
            Func<string, string> environment,
            IStopSignal stopSignal,
            IReadOnlyList<string> mapSearchRoots,
            IReadOnlyList<string> credentialSearchRoots,
            Func<IMatchTransport> createTransport,
            EosTransportFactory createEosTransport = null,
            string builtVersion = null,
            TextReader input = null)
        {
            Input = input;
            Output = output ?? throw new ArgumentNullException(nameof(output));
            Error = error ?? throw new ArgumentNullException(nameof(error));
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            StopSignal = stopSignal ?? throw new ArgumentNullException(nameof(stopSignal));
            MapSearchRoots = mapSearchRoots ?? throw new ArgumentNullException(nameof(mapSearchRoots));
            CredentialSearchRoots = credentialSearchRoots ?? throw new ArgumentNullException(nameof(credentialSearchRoots));
            CreateTransport = createTransport ?? throw new ArgumentNullException(nameof(createTransport));
            CreateEosTransport = createEosTransport ?? CreateProcessEosTransport;
            BuiltVersion = builtVersion;
        }

        public delegate bool EosTransportFactory(
            EosCredentials credentials, string version, TextWriter log, out IMatchTransport transport, out string error);

        public TextWriter Output { get; }

        public TextWriter Error { get; }

        // null なら管理のコマンドを読まない。
        public TextReader Input { get; }

        public Func<string, string> Environment { get; }

        public IStopSignal StopSignal { get; }

        public IReadOnlyList<string> MapSearchRoots { get; }

        public IReadOnlyList<string> CredentialSearchRoots { get; }

        public Func<IMatchTransport> CreateTransport { get; }

        public EosTransportFactory CreateEosTransport { get; }

        public string BuiltVersion { get; }

        public static ServerRuntime Process()
        {
            return new ServerRuntime(
                Console.Out,
                Console.Error,
                System.Environment.GetEnvironmentVariable,
                new PosixStopSignal(),
                ServerMapData.DefaultSearchRoots(),
                ServerMapData.DefaultSearchRoots(),
                () => new UdpMatchTransport(),
                builtVersion: ServerBuildVersion.Embedded(),
                input: Console.In);
        }

        private static bool CreateProcessEosTransport(
            EosCredentials credentials, string version, TextWriter log, out IMatchTransport transport, out string error)
        {
            return EosServerTransport.TryCreate(credentials, version, AppContext.BaseDirectory, log, out transport, out error);
        }
    }
}
