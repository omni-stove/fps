using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Fps.Core;

namespace Fps.Server
{
    internal sealed class ServerRulesData
    {
        public const string RulesDirectoryName = "rules";
        public const string TuningDirectoryName = "tuning";
        public const string PingTuningFileName = "ping.json";
        public const string GrenadeTuningFileName = "grenade.json";

        public ServerRulesData(
            string name, NetMatchFile file, PingSpec ping, string path, string pingPath, GrenadeSpec grenade = null, string grenadePath = null)
        {
            Grenade = grenade ?? GrenadeSpec.Default;
            GrenadePath = grenadePath;
            Name = name ?? throw new ArgumentNullException(nameof(name));
            File = file ?? throw new ArgumentNullException(nameof(file));
            Ping = ping ?? throw new ArgumentNullException(nameof(ping));
            Path = path ?? "";
            PingPath = pingPath;
        }

        public string Name { get; }

        public NetMatchFile File { get; }

        public GameRules Rules => File.Rules;

        public NetMatchSettings Settings => File.Settings;

        /// <summary>移動のモジュール (#586) がクライアントの予測と同じ値で動かすための、移動と体の寸法。</summary>
        public NetMotorSettings Motor => File.Motor;

        public PingSpec Ping { get; }

        public string Path { get; }

        public string PingPath { get; }

        /// <summary>グレネードの信管・間隔・初速・威力と半径。</summary>
        public GrenadeSpec Grenade { get; }

        public string GrenadePath { get; }

        public string LogLine => ServerProgram.LogTag + " rules " + Name
            + " objective=" + Rules.Objective
            + " weapons=" + File.LoadoutWeapons.Count.ToString(CultureInfo.InvariantCulture)
            + " from=" + Path
            + " ping=" + (PingPath ?? "default")
            + " grenade=" + (GrenadePath ?? "default")
            + " walk=" + Motor.Motor.WalkSpeed.ToString(CultureInfo.InvariantCulture)
            + " sprint=" + Motor.Motor.SprintSpeed.ToString(CultureInfo.InvariantCulture)
            + " height=" + Motor.Stance.Standing.Height.ToString(CultureInfo.InvariantCulture)
            + " crouchHeight=" + Motor.Stance.Crouched.Height.ToString(CultureInfo.InvariantCulture)
            + " radius=" + Motor.Stance.Radius.ToString(CultureInfo.InvariantCulture);

        public static bool TryLoad(string name, IReadOnlyList<string> searchRoots, out ServerRulesData data, out string error)
        {
            ArgumentNullException.ThrowIfNull(searchRoots);

            data = null;
            if (!NetLaunchOptions.IsMapName(name))
            {
                error = "rules name '" + name + "' is not a name of letters, digits, '_' or '-'";
                return false;
            }

            var fileName = name + NetMatchFile.Extension;
            string root = null;
            var looked = new List<string>();
            foreach (var candidate in searchRoots)
            {
                var directory = System.IO.Path.GetFullPath(System.IO.Path.Combine(candidate, ServerMapData.DataDirectoryName, RulesDirectoryName));
                looked.Add(directory);
                if (System.IO.File.Exists(System.IO.Path.Combine(directory, fileName)))
                {
                    root = candidate;
                    break;
                }
            }

            if (root == null)
            {
                error = "rules " + name + " not found: " + fileName + " is not in " + string.Join(" or ", looked)
                    + "; export it from Unity with make export-server-rules";
                return false;
            }

            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, RulesDirectoryName, fileName));
            NetMatchFile file;
            try
            {
                file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ServerDataJsonException || e is GameRulesJsonException)
            {
                error = "cannot read rules " + name + ": " + e.Message;
                return false;
            }

            // 印の調整値は Unity では Resources の JSON から読むので、無ければ同じく既定値で動かす
            var pingPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, TuningDirectoryName, PingTuningFileName));
            var ping = PingSpec.Default;
            if (System.IO.File.Exists(pingPath))
            {
                try
                {
                    ping = TuningJson.ParsePing(System.IO.File.ReadAllText(pingPath), pingPath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read ping tuning: " + e.Message;
                    return false;
                }
            }
            else
            {
                pingPath = null;
            }

            // グレネードも Unity では Resources の JSON (KitTuning.Grenade) から読むので、無ければ同じく既定値で動かす
            var grenadePath = System.IO.Path.GetFullPath(
                System.IO.Path.Combine(root, ServerMapData.DataDirectoryName, TuningDirectoryName, GrenadeTuningFileName));
            var grenade = GrenadeSpec.Default;
            if (System.IO.File.Exists(grenadePath))
            {
                try
                {
                    grenade = TuningJson.ParseGrenade(System.IO.File.ReadAllText(grenadePath), grenadePath);
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is TuningJsonException)
                {
                    error = "cannot read grenade tuning: " + e.Message;
                    return false;
                }
            }
            else
            {
                grenadePath = null;
            }

            data = new ServerRulesData(name, file, ping, path, pingPath, grenade, grenadePath);
            error = null;
            return true;
        }
    }
}
