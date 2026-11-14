using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace Fps.EosSdk.Tests
{
    // EosSdk.props の判定は MSBuild の評価でしか確かめられないので、偽の SDK のフォルダを作って dotnet msbuild に評価させる
    public sealed class EosSdkPropsTests
    {
        private string _sdkDir;

        [SetUp]
        public void SetUp()
        {
            _sdkDir = Path.Combine(Path.GetTempPath(), "fps-eossdk-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_sdkDir);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.Delete(_sdkDir, true);
        }

        [Test]
        public void AnEmptyFolderIsNotAnSdk()
        {
            var result = Evaluate("linux-x64");

            Assert.That(Property(result, "EosSdkAvailable"), Is.EqualTo("false"));
            Assert.That(Property(result, "DefineConstants"), Does.Not.Contain("EOS_SDK"));
            Assert.That(NativeLinks(result), Is.Empty);
        }

        [Test]
        public void NativeLibrariesWithoutSourcesAreNotAnSdk()
        {
            Place("Bin/linux-x64/libEOSSDK-Linux-Shipping.so");

            Assert.That(Property(Evaluate("linux-x64"), "EosSdkAvailable"), Is.EqualTo("false"));
        }

        [TestCase("linux-x64", "EOS_PLATFORM_LINUX")]
        [TestCase("linux-arm64", "EOS_PLATFORM_LINUX")]
        [TestCase("osx-arm64", "EOS_PLATFORM_OSX")]
        [TestCase("win-x64", "EOS_PLATFORM_WINDOWS_64")]
        [TestCase("win-arm64", "EOS_PLATFORM_WINDOWS_ARM64")]
        public void SourcesAnywhereUnderSourceAreAnSdkForTheTargetPlatform(string rid, string platform)
        {
            Place("Source/Generated/Platform/PlatformInterface.cs");

            var result = Evaluate(rid);

            Assert.That(Property(result, "EosSdkAvailable"), Is.EqualTo("true"));
            Assert.That(Property(result, "DefineConstants").Split(';'), Does.Contain("EOS_SDK").And.Contain(platform));
        }

        [Test]
        public void OnlyTheTargetRidsLibrariesAreCopied()
        {
            Place("Source/Common.cs");
            Place("Bin/linux-x64/libEOSSDK-Linux-Shipping.so");
            Place("Bin/osx-arm64/libEOSSDK-Mac-Shipping.dylib");

            Assert.That(NativeLinks(Evaluate("linux-x64")), Is.EqualTo(Names("libEOSSDK-Linux-Shipping.so")));
        }

        [Test]
        public void TheLinuxArm64LibraryIsCopiedUnderTheNameTheSdkLoads()
        {
            Place("Source/Common.cs");
            Place("Bin/linux-arm64/libEOSSDK-LinuxArm64-Shipping.so");

            Assert.That(NativeLinks(Evaluate("linux-arm64")), Is.EqualTo(Names("libEOSSDK-Linux-Shipping.so")));
        }

        [Test]
        public void TheProjectBuildsWithoutTheSdk()
        {
            var artifacts = Path.Combine(_sdkDir, "artifacts");

            var (code, output, error) = Dotnet(
                "build", ProjectPath(), "--nologo", "-v", "q", "--disable-build-servers",
                "--artifacts-path", artifacts, "-p:EosSdkDir=" + _sdkDir);

            Assert.That(code, Is.Zero, output + error);
        }

        // 定数配列を直接渡すと CA1861 になるので params で受ける
        private static string[] Names(params string[] names) => names;

        private void Place(string relativePath)
        {
            var path = Path.Combine(_sdkDir, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, string.Empty);
        }

        private JsonElement Evaluate(string rid)
        {
            var (code, output, error) = Dotnet(
                "msbuild", ProjectPath(), "--nologo",
                "-getProperty:EosSdkAvailable", "-getProperty:DefineConstants", "-getItem:EosSdkNative",
                "-p:EosSdkDir=" + _sdkDir, "-p:EosSdkRid=" + rid);
            Assert.That(code, Is.Zero, output + error);

            using var document = JsonDocument.Parse(output);
            return document.RootElement.Clone();
        }

        private static string Property(JsonElement result, string name)
        {
            return result.GetProperty("Properties").GetProperty(name).GetString();
        }

        private static string[] NativeLinks(JsonElement result)
        {
            if (!result.GetProperty("Items").TryGetProperty("EosSdkNative", out var items))
            {
                return Array.Empty<string>();
            }

            return items.EnumerateArray().Select(item => item.GetProperty("Link").GetString()).ToArray();
        }

        private static string ProjectPath()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var project = Path.Combine(dir.FullName, "Fps.EosSdk", "Fps.EosSdk.csproj");
                if (File.Exists(project))
                {
                    return project;
                }
            }

            throw new FileNotFoundException("Fps.EosSdk.csproj is not above " + AppContext.BaseDirectory);
        }

        private static (int Code, string Output, string Error) Dotnet(params string[] args)
        {
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in args)
            {
                start.ArgumentList.Add(arg);
            }

            using var process = Process.Start(start);
            var error = process.StandardError.ReadToEndAsync();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output, error.Result);
        }
    }
}
