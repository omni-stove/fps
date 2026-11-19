using System;
using Shooter.Runtime;

namespace Shooter.Editor
{
    public static class MissionDefinitionAssets
    {
        // 既存のアセットは GUID を保つので、それを参照しているシーンは壊れない。
        public static MissionDefinition CreateOrUpdate(string path, Action<MissionDefinition> configure)
        {
            return SceneBuildHelpers.CreateOrUpdateAsset(path, configure);
        }
    }
}
