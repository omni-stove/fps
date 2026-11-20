using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    internal static class TestRules
    {
        public const string RifleId = "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83";
        public const string HandgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        public static WeaponSpec Rifle() => new WeaponSpec(
            RifleId, "weapon.rifle", WeaponCategory.AssaultRifle, FireMode.Auto, 30, 90, 0.1f, 2f, 25, 100f, new DamageFalloff(20f, 60f, 0.5f));

        public static WeaponSpec Handgun() => new WeaponSpec(
            HandgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f, DamageFalloff.None);

        public static NetMatchFile File(
            GameRules rules = null,
            NetMatchSettings settings = null,
            string name = "TeamDeathmatch",
            WeaponSpec rifle = null,
            NetMotorSettings motor = null,
            IReadOnlyList<string> abilities = null)
        {
            rifle ??= Rifle();
            return new NetMatchFile(
                name,
                rules ?? GameRules.Default,
                settings ?? NetMatchSettings.Default,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.AssaultRifle, WeaponCategory.Handgun }, new[] { WeaponCategory.Handgun, WeaponCategory.AssaultRifle } }),
                new[] { rifle, Handgun() },
                0.4f,
                "WeaponCatalog",
                new WeaponCatalog(new[] { rifle, Handgun() }),
                motor,
                abilities);
        }

        public static ServerRulesData Data(
            GameRules rules = null, NetMatchSettings settings = null, PingSpec ping = null, string name = "TeamDeathmatch", ChatWordFilterSpec chatWords = null)
        {
            return new ServerRulesData(name, File(rules, settings, name), ping ?? PingSpec.Default, "memory", null, chatWords: chatWords);
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

        private static readonly int[] _middlePoint = { 1 };

        // Points の出現地点に、z = -10 から z = 10 へまっすぐ 20m の道筋を足す。中継点は真ん中 (z = 0)。押すのは Alpha、範囲は半径 3m・高さ 4m
        public static NetMapPoints PayloadPoints(Team attackers = Team.Alpha)
        {
            var points = Points();
            return new NetMapPoints(
                points.SpawnPoints,
                points.ControlPoints,
                null,
                new NetMapPayload(
                    attackers,
                    new[] { new Vector3(0f, 0f, -10f), new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 10f) },
                    _middlePoint,
                    3f,
                    4f));
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
