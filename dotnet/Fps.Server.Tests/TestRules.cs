using System.IO;
using System.Numerics;
using Fps.Core;

namespace Fps.Server.Tests
{
    internal static class TestRules
    {
        public const string RifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
        public const string HandgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        public static WeaponSpec Rifle() => new WeaponSpec(
            RifleId, "weapon.rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, new DamageFalloff(20f, 60f, 0.5f));

        public static WeaponSpec Handgun() => new WeaponSpec(
            HandgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None);

        public static NetMatchFile File(GameRules rules = null, NetMatchSettings settings = null, string name = "TeamDeathmatch")
        {
            return new NetMatchFile(
                name,
                rules ?? GameRules.Default,
                settings ?? NetMatchSettings.Default,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle, WeaponCategory.Handgun }, new[] { WeaponCategory.Handgun, WeaponCategory.AssaultRifle } }),
                new[] { Rifle(), Handgun() },
                0.4f,
                "WeaponCatalog",
                new WeaponCatalog(new[] { Rifle(), Handgun() }));
        }

        public static ServerRulesData Data(GameRules rules = null, NetMatchSettings settings = null, PingSpec ping = null, string name = "TeamDeathmatch")
        {
            return new ServerRulesData(name, File(rules, settings, name), ping ?? PingSpec.Default, "memory", null);
        }

        public static NetMapPoints Points(int controlPoints = 0)
        {
            var areas = new ControlPointArea[controlPoints];
            for (var i = 0; i < controlPoints; i++)
            {
                areas[i] = new ControlPointArea(i, new Vector3(0f, 0f, (i - 1) * 15f), 4f, 3f);
            }

            return new NetMapPoints(
                new[]
                {
                    new SpawnPoint(new Vector3(-20f, 1f, -20f), Team.Alpha),
                    new SpawnPoint(new Vector3(20f, 1f, 20f), Team.Bravo),
                },
                areas);
        }

        public static string Write(string root, string name, NetMatchFile file = null)
        {
            var directory = Path.Combine(root, ServerMapData.DataDirectoryName, ServerRulesData.RulesDirectoryName);
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name + NetMatchFile.Extension);
            System.IO.File.WriteAllText(path, NetMatchFile.Write(file ?? File(name: name)));
            return path;
        }
    }
}
