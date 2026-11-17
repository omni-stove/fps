using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Server.Eos;

namespace Shooter.Server
{
    /// <summary>プロセスの外とのつながり。テストで標準出力・環境変数・シグナル・マップの置き場所を差し替えるために 1 つにまとめる。</summary>
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
            string builtVersion = null)
        {
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

        /// <summary><c>-transport eos</c> の通信路を作る。SDK が無いビルドなどで作れなければ false と理由を返す。</summary>
        public delegate bool EosTransportFactory(
            EosCredentials credentials, string version, TextWriter log, out IMatchTransport transport, out string error);

        public TextWriter Output { get; }

        public TextWriter Error { get; }

        public Func<string, string> Environment { get; }

        public IStopSignal StopSignal { get; }

        public IReadOnlyList<string> MapSearchRoots { get; }

        /// <summary>EOS の認証情報のファイル (<c>eos.local.json</c>) を探すフォルダ。</summary>
        public IReadOnlyList<string> CredentialSearchRoots { get; }

        /// <summary>UDP の通信路を作る。</summary>
        public Func<IMatchTransport> CreateTransport { get; }

        public EosTransportFactory CreateEosTransport { get; }

        /// <summary>ビルドに焼き込んだ版。<c>-buildVersion</c> が無いときに使う。焼き込んでいなければ null。</summary>
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
                builtVersion: ServerBuildVersion.Embedded());
        }

        private static bool CreateProcessEosTransport(
            EosCredentials credentials, string version, TextWriter log, out IMatchTransport transport, out string error)
        {
            return EosServerTransport.TryCreate(credentials, version, AppContext.BaseDirectory, log, out transport, out error);
        }
    }
}
