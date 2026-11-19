using UnityEngine;

namespace Shooter.Runtime
{
    public static class CampaignContentSlots
    {
        public const string MissionsSlotName = "Missions";

        public static void Register()
        {
            GameContentSlots.Register(
                MissionsSlotName,
                typeof(MissionCatalog),
                c => c.Missions as MissionCatalog,
                (c, v) => c.Missions = (MissionCatalog)v);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnPlay()
        {
            Register();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            Register();
        }
#endif
    }
}
