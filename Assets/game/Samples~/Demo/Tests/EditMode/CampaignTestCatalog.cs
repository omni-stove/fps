using System;
using System.Collections.Generic;
using System.IO;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    // Mission01 のシーンは MenuRoute.CampaignScene (テストのホストが Build Settings にあることにするシーン)、
    // Mission02 のシーンはどのビルドにも無いパス。
    internal sealed class CampaignTestCatalog : IDisposable
    {
        public const string Mission01 = "Campaign01";
        public const string Mission02 = "Campaign02";
        public const string Mission01NameKey = "campaign.mission.Campaign01.name";
        public const string Mission02NameKey = "campaign.mission.Campaign02.name";
        public const string NotBuiltScene = "Assets/__not_in_build_settings__/Campaign02.unity";

        private readonly List<Object> _created = new List<Object>();

        public CampaignTestCatalog()
        {
            Catalog = ScriptableObject.CreateInstance<MissionCatalog>();
            Catalog.name = "TestMissionCatalog";
            _created.Add(Catalog);
            Catalog.SetMissions(new[]
            {
                new MissionCatalogEntry(Mission01, Mission01NameKey, MenuRoute.CampaignScene, Definition(Mission01)),
                new MissionCatalogEntry(Mission02, Mission02NameKey, NotBuiltScene, Definition(Mission02)),
            });
        }

        public static string Mission01Name => Localizer.Get(Mission01NameKey);

        public static string Mission02Name => Localizer.Get(Mission02NameKey);

        public MissionCatalog Catalog { get; }

        public static string SaveJson(string missionId, string checkpointId)
        {
            return new CampaignSave(
                missionId,
                checkpointId,
                100,
                new[] { new WeaponAmmo("rifle", 30, 90) },
                "rifle",
                new[] { new ObjectiveProgress("reach", ObjectiveState.Active, 0) }).ToJson();
        }

        public void Dispose()
        {
            foreach (var created in _created)
            {
                Object.DestroyImmediate(created);
            }

            _created.Clear();
        }

        private MissionDefinition Definition(string id)
        {
            var definition = ScriptableObject.CreateInstance<MissionDefinition>();
            definition.name = id;
            definition.Id = id;
            definition.SetObjectives(new[] { new ObjectiveDefinition("reach", "") });
            _created.Add(definition);
            return definition;
        }
    }

    internal sealed class MemorySaveStorage : ISaveStorage
    {
        public readonly Dictionary<string, string> Slots = new Dictionary<string, string>(StringComparer.Ordinal);

        public bool FailDeletes { get; set; }

        public bool TryRead(string slot, out string text)
        {
            return Slots.TryGetValue(slot, out text);
        }

        public void Write(string slot, string text)
        {
            Slots[slot] = text;
        }

        public void Delete(string slot)
        {
            if (FailDeletes)
            {
                throw new IOException("the save folder is read-only");
            }

            Slots.Remove(slot);
        }
    }
}
