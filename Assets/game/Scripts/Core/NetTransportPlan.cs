using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class NetTransportPlan
    {
        // .NET のサーバーと v2 のクライアントで同じ文にするため、ここに 1 つだけ持つ。
        public const string CredentialsMissingLog = "[Net] EOS credentials missing, using UDP";

        private static readonly IReadOnlyList<string> _none = Array.Empty<string>();

        private static readonly AntiCheatSettings _antiCheatNotResolved =
            AntiCheatSettings.Disabled("disabled: not resolved for this plan");

        public NetLaunchOptions Options { get; }

        public EosCredentials Credentials { get; }

        public bool FellBackToUdp { get; }

        public IReadOnlyList<string> MissingCredentials { get; }

        public AntiCheatSettings AntiCheat { get; }

        private NetTransportPlan(
            NetLaunchOptions options,
            EosCredentials credentials,
            bool fellBackToUdp,
            IReadOnlyList<string> missingCredentials,
            AntiCheatSettings antiCheat)
        {
            Options = options;
            Credentials = credentials;
            FellBackToUdp = fellBackToUdp;
            MissingCredentials = missingCredentials;
            AntiCheat = antiCheat;
        }

        public string DescribeMissingCredentials()
        {
            return "[Net] missing EOS settings: " + string.Join(", ", MissingCredentials)
                + " (set them as environment variables or in " + EosCredentials.LocalFileName + ")";
        }

        public NetTransportPlan WithAntiCheat(AntiCheatSettings antiCheat)
        {
            if (antiCheat == null)
            {
                throw new ArgumentNullException(nameof(antiCheat));
            }

            if (antiCheat.Enabled && Options.Transport != NetTransportKind.Eos)
            {
                throw new ArgumentException("anti-cheat needs the EOS transport", nameof(antiCheat));
            }

            return new NetTransportPlan(Options, Credentials, FellBackToUdp, MissingCredentials, antiCheat);
        }

        public static NetTransportPlan Decide(
            NetLaunchOptions requested,
            EosCredentials credentials,
            IReadOnlyList<string> missingCredentials)
        {
            if (requested == null)
            {
                throw new ArgumentNullException(nameof(requested));
            }

            if (requested.Transport == NetTransportKind.Udp)
            {
                return new NetTransportPlan(requested, null, false, _none, _antiCheatNotResolved);
            }

            if (credentials != null)
            {
                return new NetTransportPlan(requested, credentials, false, _none, _antiCheatNotResolved);
            }

            return new NetTransportPlan(
                requested.WithTransport(NetTransportKind.Udp),
                null,
                true,
                missingCredentials ?? _none,
                _antiCheatNotResolved);
        }
    }
}
