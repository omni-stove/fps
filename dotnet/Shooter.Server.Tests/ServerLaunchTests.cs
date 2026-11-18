using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    public sealed class ServerLaunchTests
    {
        [Test]
        public void WithoutArgumentsListensOnEveryAddressAtTheDefaultPort()
        {
            var launch = Parse(Env());

            Assert.That(launch.Options.Endpoint, Is.EqualTo("0.0.0.0:7777"));
            Assert.That(launch.Map, Is.EqualTo(ServerLaunch.DefaultMap));
            Assert.That(launch.ServerVersion, Is.EqualTo(ServerLaunch.UnknownVersion));
            Assert.That(launch.DrainTimeoutSeconds, Is.EqualTo(ServerDrain.DefaultTimeoutSeconds));
            Assert.That(launch.Options.LogFormat, Is.EqualTo(ServerLogFormat.Text));
        }

        [Test]
        public void RulesDefaultToTeamDeathmatchAndAreChosenByName()
        {
            Assert.That(Parse(Env()).Rules, Is.EqualTo(ServerLaunch.DefaultRules));
            Assert.That(Parse(Env(), "-rules", "Domination").Rules, Is.EqualTo("Domination"));
        }

        [TestCase("-rules")]
        [TestCase("-rules", "../etc")]
        [TestCase("-rules", "A", "-rules", "B")]
        public void BadRulesAreRejected(params string[] args)
        {
            Assert.That(ServerLaunch.TryParse(args, _ => null, out _, out var error), Is.False);
            Assert.That(error, Does.StartWith("-rules"));
        }

        [Test]
        public void EnvironmentChoosesTheAddressAndPort()
        {
            var launch = Parse(Env(ServerLaunch.AddressVariable, "127.0.0.1", ServerLaunch.PortVariable, " 9000 "));

            Assert.That(launch.Options.Endpoint, Is.EqualTo("127.0.0.1:9000"));
        }

        [Test]
        public void ArgumentsChooseTheAddressAndPortWhenTheEnvironmentIsEmpty()
        {
            var launch = Parse(Env(ServerLaunch.PortVariable, ""), "-address", "127.0.0.1", "-port", "9001");

            Assert.That(launch.Options.Endpoint, Is.EqualTo("127.0.0.1:9001"));
        }

        [TestCase(ServerLaunch.AddressVariable, "-address", "127.0.0.1")]
        [TestCase(ServerLaunch.PortVariable, "-port", "9000")]
        public void GivingBothTheArgumentAndTheVariableIsRejected(string variable, string flag, string value)
        {
            var error = Fail(Env(variable, value), flag, value);

            Assert.That(error, Does.Contain(flag + " is given more than once"));
            Assert.That(error, Does.Contain(variable));
        }

        [Test]
        public void ReadsTheSameFlagsAsTheUnityServer()
        {
            var launch = Parse(
                Env(), "-mode", "server", "-map", "NetArena01", "-drain-timeout", "0", "-log-format", "json", "-buildVersion", "v0.2.0");

            Assert.That(launch.Map, Is.EqualTo("NetArena01"));
            Assert.That(launch.DrainTimeoutSeconds, Is.Zero);
            Assert.That(launch.Options.LogFormat, Is.EqualTo(ServerLogFormat.Json));
            Assert.That(launch.Version, Is.EqualTo("0.2.0"));
            Assert.That(launch.ServerVersion, Is.EqualTo("0.2.0"));
        }

        [Test]
        public void TheFirstMapIsTheArgumentThenTheFirstCandidateThenTheDefault()
        {
            var pool = new List<string> { "NetArena02", "NetArena01" };

            Assert.That(Parse(Env(), "-map", "NetArena01").StartMap(pool), Is.EqualTo("NetArena01"));
            Assert.That(Parse(Env()).StartMap(pool), Is.EqualTo("NetArena02"));
            Assert.That(Parse(Env()).StartMap(new List<string>()), Is.EqualTo(ServerLaunch.DefaultMap));
            Assert.That(Parse(Env(), "-map", "NetArena01").StartMap(new List<string>()), Is.EqualTo("NetArena01"));
        }

        [Test]
        public void TheBuiltVersionIsUsedWithoutTheArgument()
        {
            Assert.That(ServerLaunch.TryParse(System.Array.Empty<string>(), _ => null, "v0.3.0", out var launch, out var error), Is.True, error);

            Assert.That(launch.Version, Is.EqualTo("0.3.0"));
        }

        [Test]
        public void TheArgumentWinsOverTheBuiltVersion()
        {
            var args = new[] { "-buildVersion", "0.4.0-test" };
            Assert.That(ServerLaunch.TryParse(args, _ => null, "0.3.0", out var launch, out var error), Is.True, error);

            Assert.That(launch.Version, Is.EqualTo("0.4.0-test"));
        }

        [Test]
        public void AnUnusableBuiltVersionIsRejected()
        {
            Assert.That(ServerLaunch.TryParse(System.Array.Empty<string>(), _ => null, "1.0+sha", out var launch, out var error), Is.False);

            Assert.That(launch, Is.Null);
            Assert.That(error, Does.StartWith("the version built into the server: "));
        }

        [Test]
        public void AcceptsRegisterLobby()
        {
            var launch = Parse(Env(), "-register-lobby", "203.0.113.10:7777");

            Assert.That(launch.Options.RegisterLobbyEndpoint, Is.EqualTo("203.0.113.10:7777"));
        }

        [Test]
        public void AcceptsTheEosTransportAndCanSwapItsOptions()
        {
            var launch = Parse(Env(), "-transport", "eos", "-rules", "Custom", "-buildVersion", "1.2.3");
            Assert.That(launch.Options.Transport, Is.EqualTo(NetTransportKind.Eos));

            var udp = launch.WithOptions(launch.Options.WithTransport(NetTransportKind.Udp));

            Assert.That(udp.Options.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(udp.Version, Is.EqualTo("1.2.3"));
            Assert.That(udp.Rules, Is.EqualTo("Custom"));
            Assert.That(udp.DrainTimeoutSeconds, Is.EqualTo(launch.DrainTimeoutSeconds));
        }

        [Test]
        public void AcceptsBots()
        {
            var launch = Parse(Env(), "-bots", "4");

            Assert.That(launch.Options.Bots, Is.EqualTo(4));
        }

        [TestCase("-mode", "host", "-mode must be server")]
        [TestCase("-mode", "client", "-mode must be server")]
        [TestCase("-net-latency", "100", "are not supported")]
        [TestCase("-drain-timeout", "3601", "-drain-timeout")]
        [TestCase("-map", "../etc", "-map")]
        [TestCase("-buildVersion", "+bad", "-buildVersion")]
        public void RejectsWhatTheServerCannotRun(string flag, string value, string expected)
        {
            Assert.That(Fail(Env(), flag, value), Does.Contain(expected));
        }

        [Test]
        public void WithoutPrivacyFlagsTheLaunchIsPublic()
        {
            var launch = Parse(Env());

            Assert.That(launch.PrivateMatch, Is.False);
            Assert.That(launch.JoinCode, Is.Null);
        }

        [Test]
        public void PrivateAloneLeavesTheCodeToBeGenerated()
        {
            var launch = Parse(Env(), "--private");

            Assert.That(launch.PrivateMatch, Is.True);
            Assert.That(launch.JoinCode, Is.Null);
        }

        [Test]
        public void TheJoinCodeIsNormalizedAndMakesTheMatchPrivate()
        {
            var launch = Parse(Env(), "--join-code", "ab-12_c");

            Assert.That(launch.PrivateMatch, Is.True);
            Assert.That(launch.JoinCode, Is.EqualTo("AB-12_C"));
        }

        [Test]
        public void PrivacyFlagsSurviveSwappingTheOptions()
        {
            var launch = Parse(Env(), "--private", "--join-code", "FRIENDS", "-transport", "eos");

            var udp = launch.WithOptions(launch.Options.WithTransport(NetTransportKind.Udp));

            Assert.That(udp.PrivateMatch, Is.True);
            Assert.That(udp.JoinCode, Is.EqualTo("FRIENDS"));
        }

        [TestCase("--join-code", ServerLaunch.JoinCodeFlag + " requires a value")]
        [TestCase("--join-code", ServerLaunch.JoinCodeFlag + " requires a value", "--private")]
        [TestCase("--join-code", ServerLaunch.JoinCodeFlag + " must be 1-16 characters", "has/slash")]
        [TestCase("--join-code", ServerLaunch.JoinCodeFlag + " must be 1-16 characters", "ABCDEFGHJKMNPQRST")]
        [TestCase("--join-code", ServerLaunch.JoinCodeFlag + " is given more than once", "A", "--join-code", "B")]
        [TestCase("--private", ServerLaunch.PrivateFlag + " is given more than once", "--private")]
        public void BadPrivacyFlagsAreRejected(string flag, string expected, params string[] rest)
        {
            var args = new List<string> { flag };
            args.AddRange(rest);

            Assert.That(Fail(Env(), args.ToArray()), Does.StartWith(expected));
        }

        [Test]
        public void TheBanFileIsOptionalAndSurvivesSwappingTheOptions()
        {
            Assert.That(Parse(Env()).BanFile, Is.Null);

            var launch = Parse(Env(), "--ban-file", "bans.local.json", "-transport", "eos");
            var udp = launch.WithOptions(launch.Options.WithTransport(NetTransportKind.Udp));

            Assert.That(launch.BanFile, Is.EqualTo("bans.local.json"));
            Assert.That(udp.BanFile, Is.EqualTo("bans.local.json"));
        }

        [TestCase(ServerLaunch.BanFileFlag + " requires a path", "--ban-file")]
        [TestCase(ServerLaunch.BanFileFlag + " requires a path", "--ban-file", "--private")]
        [TestCase(ServerLaunch.BanFileFlag + " is given more than once", "--ban-file", "a.json", "--ban-file", "b.json")]
        public void BadBanFileFlagsAreRejected(string expected, params string[] args)
        {
            Assert.That(Fail(Env(), args), Does.StartWith(expected));
        }

        [Test]
        public void APublicLaunchKeepsThePublicRules()
        {
            var rules = new NetMatchSettings(joinCode: "IGNORED");

            var applied = Parse(Env()).ApplyPrivacy(rules, _ => throw new AssertionException("must not generate"), out var generated);

            Assert.That(applied, Is.SameAs(rules));
            Assert.That(generated, Is.False);
        }

        [Test]
        public void PrivateGeneratesACodeWhenNeitherTheArgumentsNorTheRulesHaveOne()
        {
            var next = 0;
            var applied = Parse(Env(), "--private").ApplyPrivacy(new NetMatchSettings(maxPlayers: 4), _ => next++, out var generated);

            Assert.That(generated, Is.True);
            Assert.That(applied.PrivateMatch, Is.True);
            Assert.That(applied.JoinCode, Is.EqualTo("234567"));
            Assert.That(applied.JoinCode, Has.Length.EqualTo(JoinCode.DefaultLength));
            Assert.That(applied.MaxPlayers, Is.EqualTo(4), "the other rules are kept");
        }

        [Test]
        public void TheRulesFileCanMakeThePrivateMatchAlone()
        {
            var applied = Parse(Env()).ApplyPrivacy(new NetMatchSettings(privateMatch: true, joinCode: "friends"), _ => 0, out var generated);

            Assert.That(generated, Is.False);
            Assert.That(applied.PrivateMatch, Is.True);
            Assert.That(applied.JoinCode, Is.EqualTo("FRIENDS"));
        }

        [Test]
        public void ThePrivateRulesWithoutACodeGetAGeneratedOne()
        {
            var applied = Parse(Env()).ApplyPrivacy(new NetMatchSettings(privateMatch: true), _ => 1, out var generated);

            Assert.That(generated, Is.True);
            Assert.That(applied.JoinCode, Is.EqualTo("333333"));
        }

        [Test]
        public void TheArgumentsWinOverTheRules()
        {
            var rules = new NetMatchSettings(privateMatch: false, joinCode: "FROMRULES");

            Assert.That(Parse(Env(), "--private").ApplyPrivacy(rules, _ => 0, out var generated).JoinCode, Is.EqualTo("FROMRULES"));
            Assert.That(generated, Is.False, "--private alone uses the code in the rules");

            var applied = Parse(Env(), "--join-code", "FROMARGS").ApplyPrivacy(rules, _ => 0, out generated);

            Assert.That(applied.PrivateMatch, Is.True);
            Assert.That(applied.JoinCode, Is.EqualTo("FROMARGS"));
            Assert.That(generated, Is.False);
        }

        [Test]
        public void ThePrivacyLogLineShowsTheCodeAndWhetherItWasGenerated()
        {
            Assert.That(ServerLaunch.PrivacyLogLine(NetMatchSettings.Default, false), Is.EqualTo("[Server] public match"));
            Assert.That(
                ServerLaunch.PrivacyLogLine(new NetMatchSettings(privateMatch: true, joinCode: "ABC234"), true),
                Is.EqualTo("[Server] private match join_code=ABC234 (generated; pass --join-code to choose one)"));
            Assert.That(
                ServerLaunch.PrivacyLogLine(new NetMatchSettings(privateMatch: true, joinCode: "FRIENDS"), false),
                Is.EqualTo("[Server] private match join_code=FRIENDS"));
        }

        private static ServerLaunch Parse(Dictionary<string, string> environment, params string[] args)
        {
            Assert.That(ServerLaunch.TryParse(args, Lookup(environment), out var launch, out var error), Is.True, error);
            return launch;
        }

        private static string Fail(Dictionary<string, string> environment, params string[] args)
        {
            Assert.That(ServerLaunch.TryParse(args, Lookup(environment), out var launch, out var error), Is.False);
            Assert.That(launch, Is.Null);
            return error;
        }

        private static System.Func<string, string> Lookup(Dictionary<string, string> environment)
        {
            return name => environment.TryGetValue(name, out var value) ? value : null;
        }

        private static Dictionary<string, string> Env(params string[] pairs)
        {
            var environment = new Dictionary<string, string>();
            for (var i = 0; i < pairs.Length; i += 2)
            {
                environment[pairs[i]] = pairs[i + 1];
            }

            return environment;
        }
    }
}
