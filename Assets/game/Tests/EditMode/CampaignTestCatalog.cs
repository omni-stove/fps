using System;
using System.Collections.Generic;
using System.IO;
using Fps.Core;
using Fps.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// メニューのテストに使うミッションの一覧。<see cref="Mission01"/> のシーンは <see cref="MenuRoute.CampaignScene"/>
    /// (テストのホストが Build Settings にあることにするシーン)、<see cref="Mission02"/> のシーンはどのビルドにも無いパス。
    /// </summary>
    internal sealed class CampaignTestCatalog : IDisposable
    {
        public const string Mission01 = "Campaign01";
        public const string Mission02 = "Campaign02";
        public const string Mission01Name = "Operation One";
        public const string Mission02Name = "Operation Two";
        public const string NotBuiltScene = "Assets/__not_in_build_settings__/Campaign02.unity";

        private readonly List<Object> _created = new List<Object>();

        public CampaignTestCatalog()
        {
            Catalog = ScriptableObject.CreateInstance<MissionCatalog>();
            Catalog.name = "TestMissionCatalog";
            _created.Add(Catalog);
            Catalog.SetMissions(new[]
            {
                new MissionCatalogEntry(Mission01, Mission01Name, MenuRoute.CampaignScene, Definition(Mission01)),
                new MissionCatalogEntry(Mission02, Mission02Name, NotBuiltScene, Definition(Mission02)),
            });
        }

        public MissionCatalog Catalog { get; }

        /// <summary>missionId のミッションの、チェックポイント checkpointId で書いたセーブの文字列。</summary>
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
            definition.SetObjectives(new[] { new ObjectiveDefinition("reach", "") });
            _created.Add(definition);
            return definition;
        }
    }

    /// <summary>メモリに置く <see cref="ISaveStorage"/>。<see cref="FailDeletes"/> で消すときに失敗させられる。</summary>
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
