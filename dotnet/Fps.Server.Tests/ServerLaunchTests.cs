using System.Collections.Generic;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Server.Tests
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
        public void AcceptsRegisterLobby()
        {
            var launch = Parse(Env(), "-register-lobby", "203.0.113.10:7777");

            Assert.That(launch.Options.RegisterLobbyEndpoint, Is.EqualTo("203.0.113.10:7777"));
        }

        [TestCase("-mode", "host", "-mode must be server")]
        [TestCase("-mode", "client", "-mode must be server")]
        [TestCase("-transport", "eos", "-transport eos is not supported")]
        [TestCase("-bots", "4", "-bots is not supported")]
        [TestCase("-net-latency", "100", "are not supported")]
        [TestCase("-drain-timeout", "3601", "-drain-timeout")]
        [TestCase("-map", "../etc", "-map")]
        [TestCase("-buildVersion", "+bad", "-buildVersion")]
        public void RejectsWhatTheServerCannotRun(string flag, string value, string expected)
        {
            Assert.That(Fail(Env(), flag, value), Does.Contain(expected));
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
