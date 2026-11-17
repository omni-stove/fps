using System;
using Shooter.Core;
using Shooter.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Shooter.Tests.EditMode
{
    public sealed class ShooterKitRootTests
    {
        private bool _hadPreference;
        private string _savedPreference;

        // 開発者が EditorPrefs で出力先を変えていても既定のパスを確かめられるよう、テストの間だけ外して後で戻す。
        [SetUp]
        public void SetUp()
        {
            _hadPreference = EditorPrefs.HasKey(ShooterKitRoot.PrefsKey);
            _savedPreference = EditorPrefs.GetString(ShooterKitRoot.PrefsKey, string.Empty);
            EditorPrefs.DeleteKey(ShooterKitRoot.PrefsKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hadPreference)
            {
                EditorPrefs.SetString(ShooterKitRoot.PrefsKey, _savedPreference);
            }
            else
            {
                EditorPrefs.DeleteKey(ShooterKitRoot.PrefsKey);
            }
        }

        // Assets/game の直書きにしないのは、利用側のプロジェクトに testables で入れたとき (Packages/com.omnistove.shooter-kit から読む) も通すため。
        private static string ExpectedBundled =>
            UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(ShooterKitRoot).Assembly) == null
                ? "Assets/game"
                : "Packages/com.omnistove.shooter-kit";

        [Test]
        public void OutputAndBundledDefaultToTheKitFolders()
        {
            Assert.That(ShooterKitRoot.Output, Is.EqualTo(ShooterKitRoot.DefaultOutput));
            Assert.That(ShooterKitRoot.Bundled, Is.EqualTo(ExpectedBundled));
            Assert.That(ShooterKitRoot.BundledPath("UI/Resources"), Is.EqualTo(ExpectedBundled + "/UI/Resources"));
        }

        [Test]
        public void UseDefaultOutputChangesTheFallbackUntilCleared()
        {
            var before = ShooterKitRoot.DefaultOutput;
            try
            {
                ShooterKitRoot.UseDefaultOutput("Assets/MyGame/");

                Assert.That(ShooterKitRoot.DefaultOutput, Is.EqualTo("Assets/MyGame"));
                Assert.That(ShooterKitRoot.Output, Is.EqualTo("Assets/MyGame"));
                Assert.That(ShooterKitRoot.ResolveOutput(null, "Assets/FromPrefs"), Is.EqualTo("Assets/FromPrefs"), "the preference still wins");
                Assert.Throws<ArgumentException>(() => ShooterKitRoot.UseDefaultOutput("Packages/com.omnistove.shooter-kit"));

                ShooterKitRoot.UseDefaultOutput(null);

                Assert.That(ShooterKitRoot.DefaultOutput, Is.EqualTo(KitPaths.DefaultRoot));
            }
            finally
            {
                ShooterKitRoot.UseDefaultOutput(before == KitPaths.DefaultRoot ? null : before);
            }
        }
        [Test]
        public void PhysicalPathResolvesOnlyPackagePaths()
        {
            Assert.That(ShooterKitRoot.PhysicalPath("Assets/MyGame/Weapons/Json"), Is.EqualTo("Assets/MyGame/Weapons/Json"));
            Assert.That(ShooterKitRoot.PhysicalPath(null), Is.Null);

            var resolved = ShooterKitRoot.PhysicalPath("Packages\\com.omnistove.shooter-kit\\Weapons\\Json");
            Assert.That(System.IO.Path.IsPathRooted(resolved), Is.True, resolved);
            Assert.That(resolved, Does.Not.Contain("\\"));
            Assert.That(resolved, Does.EndWith("/Weapons/Json"));
        }

        [Test]
        public void TheBundledTuningCanBeReadFromDisk()
        {
            Assert.That(System.IO.Directory.Exists(ShooterKitRoot.PhysicalPath(ShooterKitRoot.BundledPath("Resources/Tuning"))), Is.True);
        }

        [Test]
        public void GeneratedAssetPathsFollowTheOutputRoot()
        {
            var root = ShooterKitRoot.DefaultOutput;

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
            ShooterKitRoot.SetPreferredOutput("Assets/ShooterKit/");

            Assert.That(ShooterKitRoot.Output, Is.EqualTo("Assets/ShooterKit"));
            Assert.That(WeaponAssets.CatalogPath, Is.EqualTo("Assets/ShooterKit/Weapons/WeaponCatalog.asset"));
            Assert.That(GameRulesAssets.Folder, Is.EqualTo("Assets/ShooterKit/Rules"));
            Assert.That(ShooterKitRoot.OutputScene("MainMenu"), Is.EqualTo("Assets/ShooterKit/Scenes/MainMenu.unity"));
            Assert.That(ShooterKitRoot.Bundled, Is.EqualTo(ExpectedBundled), "the bundled source does not follow the output root");

            ShooterKitRoot.ClearPreferredOutput();

            Assert.That(ShooterKitRoot.Output, Is.EqualTo(ShooterKitRoot.DefaultOutput));
        }

        [Test]
        public void SetPreferredOutputRejectsReadOnlyRootsWithoutSavingThem()
        {
            Assert.Throws<ArgumentException>(() => ShooterKitRoot.SetPreferredOutput("Packages/com.omnistove.shooter-kit"));
            Assert.That(EditorPrefs.HasKey(ShooterKitRoot.PrefsKey), Is.False);
        }

        [Test]
        public void ResolveOutputPrefersTheArgumentOverThePreference()
        {
            var args = new[] { "Unity", "-batchmode", ShooterKitRoot.ArgumentName, "Assets/FromArgs", "-quit" };

            Assert.That(ShooterKitRoot.ResolveOutput(args, "Assets/FromPrefs"), Is.EqualTo("Assets/FromArgs"));
            Assert.That(ShooterKitRoot.ResolveOutput(new[] { "Unity", "-batchmode" }, "Assets/FromPrefs"), Is.EqualTo("Assets/FromPrefs"));
            Assert.That(ShooterKitRoot.ResolveOutput(new[] { "Unity" }, string.Empty), Is.EqualTo(ShooterKitRoot.DefaultOutput));
            Assert.That(ShooterKitRoot.ResolveOutput(null, null), Is.EqualTo(ShooterKitRoot.DefaultOutput));
        }

        [Test]
        public void ResolveOutputNormalizesTheRoot()
        {
            var args = new[] { ShooterKitRoot.ArgumentName, "Assets\\Kit\\" };

            Assert.That(ShooterKitRoot.ResolveOutput(args, null), Is.EqualTo("Assets/Kit"));
        }

        [Test]
        public void ResolveOutputRejectsAMissingOrRepeatedArgument()
        {
            Assert.Throws<ArgumentException>(() => ShooterKitRoot.ResolveOutput(new[] { ShooterKitRoot.ArgumentName }, null));
            Assert.Throws<ArgumentException>(() => ShooterKitRoot.ResolveOutput(new[] { ShooterKitRoot.ArgumentName, "-quit" }, null));
            Assert.Throws<ArgumentException>(
                () => ShooterKitRoot.ResolveOutput(new[] { ShooterKitRoot.ArgumentName, "Assets/A", ShooterKitRoot.ArgumentName, "Assets/B" }, null));
        }

        [TestCase("Packages/com.omnistove.shooter-kit")]
        [TestCase("Assets")]
        [TestCase("ProjectSettings")]
        [TestCase("Assets/../Packages")]
        public void ResolveOutputRejectsRootsOutsideAssets(string root)
        {
            Assert.Throws<ArgumentException>(() => ShooterKitRoot.ResolveOutput(new[] { ShooterKitRoot.ArgumentName, root }, null));
            Assert.Throws<ArgumentException>(() => ShooterKitRoot.ResolveOutput(null, root));
        }

        [Test]
        public void ResolveBundledUsesThePackageWhenThereIsOne()
        {
            Assert.That(ShooterKitRoot.ResolveBundled("Packages/com.omnistove.shooter-kit"), Is.EqualTo("Packages/com.omnistove.shooter-kit"));
            Assert.That(ShooterKitRoot.ResolveBundled(null), Is.EqualTo(KitPaths.DefaultRoot));
            Assert.That(ShooterKitRoot.ResolveBundled(string.Empty), Is.EqualTo(KitPaths.DefaultRoot));
        }
    }
}
