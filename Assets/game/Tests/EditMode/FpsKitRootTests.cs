using System;
using Fps.Core;
using Fps.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Fps.Tests.EditMode
{
    public sealed class FpsKitRootTests
    {
        private bool _hadPreference;
        private string _savedPreference;

        // 開発者が EditorPrefs で出力先を変えていても既定のパスを確かめられるよう、テストの間だけ外して後で戻す。
        [SetUp]
        public void SetUp()
        {
            _hadPreference = EditorPrefs.HasKey(FpsKitRoot.PrefsKey);
            _savedPreference = EditorPrefs.GetString(FpsKitRoot.PrefsKey, string.Empty);
            EditorPrefs.DeleteKey(FpsKitRoot.PrefsKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadPreference)
            {
                EditorPrefs.SetString(FpsKitRoot.PrefsKey, _savedPreference);
            }
            else
            {
                EditorPrefs.DeleteKey(FpsKitRoot.PrefsKey);
            }
        }

        // Assets/game の直書きにしないのは、利用側のプロジェクトに testables で入れたとき (Packages/com.omnistove.fps から読む) も通すため。
        private static string ExpectedBundled =>
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FpsKitRoot).Assembly) == null
                ? "Assets/game"
                : "Packages/com.omnistove.fps";

        [Test]
        public void OutputAndBundledDefaultToTheKitFolders()
        {
            Assert.That(FpsKitRoot.Output, Is.EqualTo(FpsKitRoot.DefaultOutput));
            Assert.That(FpsKitRoot.Bundled, Is.EqualTo(ExpectedBundled));
            Assert.That(FpsKitRoot.BundledPath("UI/Resources"), Is.EqualTo(ExpectedBundled + "/UI/Resources"));
        }

        [Test]
        public void UseDefaultOutputChangesTheFallbackUntilCleared()
        {
            var before = FpsKitRoot.DefaultOutput;
            try
            {
                FpsKitRoot.UseDefaultOutput("Assets/MyGame/");

                Assert.That(FpsKitRoot.DefaultOutput, Is.EqualTo("Assets/MyGame"));
                Assert.That(FpsKitRoot.Output, Is.EqualTo("Assets/MyGame"));
                Assert.That(FpsKitRoot.ResolveOutput(null, "Assets/FromPrefs"), Is.EqualTo("Assets/FromPrefs"), "the preference still wins");
                Assert.Throws<ArgumentException>(() => FpsKitRoot.UseDefaultOutput("Packages/com.omnistove.fps"));

                FpsKitRoot.UseDefaultOutput(null);

                Assert.That(FpsKitRoot.DefaultOutput, Is.EqualTo(KitPaths.DefaultRoot));
            }
            finally
            {
                FpsKitRoot.UseDefaultOutput(before == KitPaths.DefaultRoot ? null : before);
            }
        }
        [Test]
        public void PhysicalPathResolvesOnlyPackagePaths()
        {
            Assert.That(FpsKitRoot.PhysicalPath("Assets/MyGame/Weapons/Json"), Is.EqualTo("Assets/MyGame/Weapons/Json"));
            Assert.That(FpsKitRoot.PhysicalPath(null), Is.Null);

            var resolved = FpsKitRoot.PhysicalPath("Packages\\com.omnistove.fps\\Weapons\\Json");
            Assert.That(System.IO.Path.IsPathRooted(resolved), Is.True, resolved);
            Assert.That(resolved, Does.Not.Contain("\\"));
            Assert.That(resolved, Does.EndWith("/Weapons/Json"));
        }

        [Test]
        public void TheBundledTablesCanBeReadFromDisk()
        {
            Assert.That(System.IO.Directory.Exists(FpsKitRoot.PhysicalPath(FpsKitRoot.BundledPath("Resources/Localization"))), Is.True);
        }

        [Test]
        public void GeneratedAssetPathsFollowTheOutputRoot()
        {
            var root = FpsKitRoot.DefaultOutput;

            Assert.That(GameRulesAssets.Folder, Is.EqualTo(root + "/Rules"));
            Assert.That(WeaponAssets.Folder, Is.EqualTo(root + "/Weapons"));
            Assert.That(WeaponAssets.CatalogPath, Is.EqualTo(root + "/Weapons/WeaponCatalog.asset"));
            Assert.That(WeaponJsonImporter.DefaultOutputFolder, Is.EqualTo(root + "/Weapons"));
        }

        [Test]
        public void TheCharacterRigIsWrittenInsideTheKit()
        {
            Assert.That(CharacterRigBuilder.Folder, Is.EqualTo(ExpectedBundled + "/Characters"));
            Assert.That(CharacterRigBuilder.ControllerPath, Is.EqualTo(ExpectedBundled + "/Characters/CharacterRig.controller"));
            Assert.That(CharacterRigBuilder.ClipFolder, Is.EqualTo(ExpectedBundled + "/Characters/Clips"));
        }

        [Test]
        public void PreferenceMovesEveryGeneratedPath()
        {
            FpsKitRoot.SetPreferredOutput("Assets/FpsKit/");

            Assert.That(FpsKitRoot.Output, Is.EqualTo("Assets/FpsKit"));
            Assert.That(WeaponAssets.CatalogPath, Is.EqualTo("Assets/FpsKit/Weapons/WeaponCatalog.asset"));
            Assert.That(GameRulesAssets.Folder, Is.EqualTo("Assets/FpsKit/Rules"));
            Assert.That(FpsKitRoot.OutputScene("MainMenu"), Is.EqualTo("Assets/FpsKit/Scenes/MainMenu.unity"));
            Assert.That(FpsKitRoot.Bundled, Is.EqualTo(ExpectedBundled), "the bundled source does not follow the output root");

            FpsKitRoot.ClearPreferredOutput();

            Assert.That(FpsKitRoot.Output, Is.EqualTo(FpsKitRoot.DefaultOutput));
        }

        [Test]
        public void SetPreferredOutputRejectsReadOnlyRootsWithoutSavingThem()
        {
            Assert.Throws<ArgumentException>(() => FpsKitRoot.SetPreferredOutput("Packages/com.omnistove.fps"));
            Assert.That(EditorPrefs.HasKey(FpsKitRoot.PrefsKey), Is.False);
        }

        [Test]
        public void ResolveOutputPrefersTheArgumentOverThePreference()
        {
            var args = new[] { "Unity", "-batchmode", FpsKitRoot.ArgumentName, "Assets/FromArgs", "-quit" };

            Assert.That(FpsKitRoot.ResolveOutput(args, "Assets/FromPrefs"), Is.EqualTo("Assets/FromArgs"));
            Assert.That(FpsKitRoot.ResolveOutput(new[] { "Unity", "-batchmode" }, "Assets/FromPrefs"), Is.EqualTo("Assets/FromPrefs"));
            Assert.That(FpsKitRoot.ResolveOutput(new[] { "Unity" }, string.Empty), Is.EqualTo(FpsKitRoot.DefaultOutput));
            Assert.That(FpsKitRoot.ResolveOutput(null, null), Is.EqualTo(FpsKitRoot.DefaultOutput));
        }

        [Test]
        public void ResolveOutputNormalizesTheRoot()
        {
            var args = new[] { FpsKitRoot.ArgumentName, "Assets\\Kit\\" };

            Assert.That(FpsKitRoot.ResolveOutput(args, null), Is.EqualTo("Assets/Kit"));
        }

        [Test]
        public void ResolveOutputRejectsAMissingOrRepeatedArgument()
        {
            Assert.Throws<ArgumentException>(() => FpsKitRoot.ResolveOutput(new[] { FpsKitRoot.ArgumentName }, null));
            Assert.Throws<ArgumentException>(() => FpsKitRoot.ResolveOutput(new[] { FpsKitRoot.ArgumentName, "-quit" }, null));
            Assert.Throws<ArgumentException>(
                () => FpsKitRoot.ResolveOutput(new[] { FpsKitRoot.ArgumentName, "Assets/A", FpsKitRoot.ArgumentName, "Assets/B" }, null));
        }

        [TestCase("Packages/com.omnistove.fps")]
        [TestCase("Assets")]
        [TestCase("ProjectSettings")]
        [TestCase("Assets/../Packages")]
        public void ResolveOutputRejectsRootsOutsideAssets(string root)
        {
            Assert.Throws<ArgumentException>(() => FpsKitRoot.ResolveOutput(new[] { FpsKitRoot.ArgumentName, root }, null));
            Assert.Throws<ArgumentException>(() => FpsKitRoot.ResolveOutput(null, root));
        }

        [Test]
        public void ResolveBundledUsesThePackageWhenThereIsOne()
        {
            Assert.That(FpsKitRoot.ResolveBundled("Packages/com.omnistove.fps"), Is.EqualTo("Packages/com.omnistove.fps"));
            Assert.That(FpsKitRoot.ResolveBundled(null), Is.EqualTo(KitPaths.DefaultRoot));
            Assert.That(FpsKitRoot.ResolveBundled(string.Empty), Is.EqualTo(KitPaths.DefaultRoot));
        }
    }
}
