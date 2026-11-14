using System;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// <see cref="TestWeapons"/> と同じ値の武器の JSON。ゲームの武器の JSON (サンプルの Weapons/Json) を変えても取り込みのテストが動かないよう、テストの側に持つ。
    /// </summary>
    public static class TestWeaponJson
    {
        public static readonly string[] Names = { "Rifle", "Handgun", "Launcher" };

        public const string Rifle = @"{
  ""id"": ""6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83"",
  ""nameKey"": ""weapon.rifle"",
  ""category"": ""AssaultRifle"",
  ""fireMode"": ""Auto"",
  ""magazineSize"": 30,
  ""reserveAmmo"": 90,
  ""fireInterval"": 0.1,
  ""reloadSeconds"": 2,
  ""damage"": 25,
  ""range"": 100,
  ""falloff"": {
    ""startDistance"": 20,
    ""endDistance"": 60,
    ""minMultiplier"": 0.5
  },
  ""explosionRadius"": 0,
  ""adsFieldOfView"": 45,
  ""blindFireSpreadDegrees"": 8,
  ""activeReloadWindowStart"": 0.5,
  ""activeReloadWindowLength"": 0.15,
  ""activeReloadFailSeconds"": 0.6,
  ""recoilPitchDegrees"": 0.5,
  ""recoilYawDegrees"": 0.2,
  ""recoilRecoveryPerSecond"": 10,
  ""hipSpreadDegrees"": 3,
  ""adsSpreadDegrees"": 0.5,
  ""moveSpreadDegrees"": 3
}
";

        public const string Handgun = @"{
  ""id"": ""b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45"",
  ""nameKey"": ""weapon.handgun"",
  ""category"": ""Handgun"",
  ""fireMode"": ""Semi"",
  ""magazineSize"": 12,
  ""reserveAmmo"": 48,
  ""fireInterval"": 0.2,
  ""reloadSeconds"": 1.5,
  ""damage"": 34,
  ""range"": 60,
  ""falloff"": {
    ""startDistance"": 15,
    ""endDistance"": 40,
    ""minMultiplier"": 0.6
  },
  ""explosionRadius"": 0,
  ""adsFieldOfView"": 55,
  ""blindFireSpreadDegrees"": 12,
  ""activeReloadWindowStart"": 0.45,
  ""activeReloadWindowLength"": 0.2,
  ""activeReloadFailSeconds"": 0.4,
  ""recoilPitchDegrees"": 2.5,
  ""recoilYawDegrees"": 0.6,
  ""recoilRecoveryPerSecond"": 60,
  ""hipSpreadDegrees"": 2,
  ""adsSpreadDegrees"": 0.8,
  ""moveSpreadDegrees"": 1.5
}
";

        public const string Launcher = @"{
  ""id"": ""e7a3c5d1-2f84-4b69-a0d3-5c8e1b7f2a96"",
  ""nameKey"": ""weapon.launcher"",
  ""category"": ""Launcher"",
  ""fireMode"": ""Semi"",
  ""magazineSize"": 1,
  ""reserveAmmo"": 4,
  ""fireInterval"": 0.5,
  ""reloadSeconds"": 3,
  ""damage"": 120,
  ""range"": 150,
  ""falloff"": ""None"",
  ""explosionRadius"": 4,
  ""adsFieldOfView"": null,
  ""blindFireSpreadDegrees"": 0,
  ""activeReloadWindowStart"": 0.5,
  ""activeReloadWindowLength"": 0.15,
  ""activeReloadFailSeconds"": 0.5,
  ""recoilPitchDegrees"": 0,
  ""recoilYawDegrees"": 0,
  ""recoilRecoveryPerSecond"": 0,
  ""hipSpreadDegrees"": 0,
  ""adsSpreadDegrees"": 0,
  ""moveSpreadDegrees"": 0
}
";

        /// <summary>name (<see cref="Names"/> のどれか) の JSON。</summary>
        public static string Text(string name)
        {
            switch (name)
            {
                case "Rifle":
                    return Rifle;
                case "Handgun":
                    return Handgun;
                case "Launcher":
                    return Launcher;
                default:
                    throw new ArgumentException($"no test weapon JSON named '{name}'", nameof(name));
            }
        }
    }
}
