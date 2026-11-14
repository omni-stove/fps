using Fps.Runtime;
using UnityEditor;
using UnityEngine;

namespace Fps.Editor
{
    /// <summary>
    /// 試し用の何もしない能力 (<see cref="NullAbilityDefinition"/>) のアセットを <see cref="NullAbilityPath"/> に作るか上書きする。
    /// 回数・回数の戻り・待ち時間・効果が出るまでの秒数がどれも見えるよう、どれも 0 でない値にする。
    /// あわせて、展開式の盾を置く能力 (<see cref="ShieldDeployDefinition"/>) を <see cref="ShieldAbilityPath"/> に作る。
    /// 既存のアセットは GUID を保って中身だけ更新するので、参照しているシーンは壊れない。何度実行しても同じ結果になる。
    /// Editor を閉じた状態で次のように呼べる:
    /// <code>unity -batchmode -quit -projectPath . -executeMethod Fps.Editor.AbilityAssetBuilder.Build</code>
    /// </summary>
    public static class AbilityAssetBuilder
    {
        public const string MenuPath = "Fps/Build Ability Assets";
        public const string LogTag = "[AbilityAssetBuilder]";

        public const string NullAbilityId = "null-ability";
        public const int NullAbilityMaxCharges = 2;
        public const float NullAbilityRechargeSeconds = 6f;
        public const float NullAbilityCooldownSeconds = 1.5f;
        public const float NullAbilityWindupSeconds = 0.25f;

        public const string ShieldAbilityId = "deploy-shield";
        public const int ShieldAbilityMaxCharges = 2;
        public const float ShieldAbilityRechargeSeconds = 15f;
        public const float ShieldAbilityCooldownSeconds = 1f;
        public const float ShieldAbilityWindupSeconds = 0f;

        /// <summary>能力のアセットを置く、出力先ルート (<see cref="FpsKitRoot.Output"/>) の下のフォルダ。</summary>
        public static string Folder => FpsKitRoot.OutputPath("Abilities");

        public static string NullAbilityPath => Folder + "/NullAbility.asset";

        public static string ShieldAbilityPath => Folder + "/DeployShield.asset";

        [MenuItem(MenuPath)]
        public static void Build()
        {
            BuildAt(NullAbilityPath);
            BuildShieldAt(ShieldAbilityPath);
        }

        public static ShieldDeployDefinition BuildShieldAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<ShieldDeployDefinition>(path, ConfigureShield);
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        // 手で調整した値を射撃場を作り直すたびに消さないよう、あれば中身は上書きしない。
        public static ShieldDeployDefinition LoadOrBuildShieldAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<ShieldDeployDefinition>(ShieldAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                existing.ToDeploySpec();
                return existing;
            }

            Debug.Log($"{LogTag} '{ShieldAbilityPath}' is missing; building it");
            return BuildShieldAt(ShieldAbilityPath);
        }

        /// <summary>path に何もしない能力を作るか上書きして保存し、返す。path に別の型のアセットがあれば例外を投げる。</summary>
        public static NullAbilityDefinition BuildAt(string path)
        {
            path = SceneBuildHelpers.ValidateAssetPath(path);
            SceneBuildHelpers.EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));
            var ability = SceneBuildHelpers.CreateOrUpdateAsset<NullAbilityDefinition>(path, Configure);
            Debug.Log($"{LogTag} saved '{path}'");
            return ability;
        }

        /// <summary><see cref="NullAbilityPath"/> の能力を読む。無ければ <see cref="Build"/> と同じものを作る (あれば中身は上書きしない)。</summary>
        public static NullAbilityDefinition LoadOrBuildNullAbility()
        {
            var existing = AssetDatabase.LoadAssetAtPath<NullAbilityDefinition>(NullAbilityPath);
            if (existing != null)
            {
                existing.ToSpec();
                return existing;
            }

            Debug.Log($"{LogTag} '{NullAbilityPath}' is missing; building it");
            return BuildAt(NullAbilityPath);
        }

        private static void Configure(NullAbilityDefinition ability)
        {
            ability.Id = NullAbilityId;
            ability.MaxCharges = NullAbilityMaxCharges;
            ability.RechargeSeconds = NullAbilityRechargeSeconds;
            ability.CooldownSeconds = NullAbilityCooldownSeconds;
            ability.WindupSeconds = NullAbilityWindupSeconds;
            ability.ToSpec();
        }

        private static void ConfigureShield(ShieldDeployDefinition ability)
        {
            ability.Id = ShieldAbilityId;
            ability.MaxCharges = ShieldAbilityMaxCharges;
            ability.RechargeSeconds = ShieldAbilityRechargeSeconds;
            ability.CooldownSeconds = ShieldAbilityCooldownSeconds;
            ability.WindupSeconds = ShieldAbilityWindupSeconds;
            ability.MaxActive = Fps.Core.DeploySpec.DefaultMaxActive;
            ability.Overflow = Fps.Core.DeploySpec.DefaultOverflow;
            ability.MaxHealth = Fps.Core.DeploySpec.DefaultMaxHealth;
            ability.MaxRange = Fps.Core.DeploySpec.DefaultMaxRange;
            ability.MaxSlopeDegrees = Fps.Core.DeploySpec.DefaultMaxSlopeDegrees;
            ability.MaxStepHeight = Fps.Core.DeploySpec.DefaultMaxStepHeight;
            ability.Size = new Vector3(Fps.Core.DeploySpec.DefaultWidth, Fps.Core.DeploySpec.DefaultHeight, Fps.Core.DeploySpec.DefaultDepth);
            ability.Prefab = null;
            ability.PreviewPrefab = null;
            ability.ToSpec();
            ability.ToDeploySpec();
        }
    }
}
