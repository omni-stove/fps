using System;
using System.IO;

namespace Fps.EosSdk
{
    public static class EosSdkBuild
    {
        public const string LogTag = "[Eos]";

#if EOS_SDK
        public static bool Included => true;

        public static string NativeLibraryFile => Epic.OnlineServices.Common.LIBRARY_NAME;

        public static string NativeVersion() => Epic.OnlineServices.Version.VersionInterface.GetVersion();
#else
        public static bool Included => false;

        public static string NativeLibraryFile => null;

        public static string NativeVersion() => throw new InvalidOperationException("the EOS C# SDK is not included in this build");
#endif

        public static bool HasNativeLibrary(string directory)
        {
            ArgumentNullException.ThrowIfNull(directory);

            return NativeLibraryFile != null && File.Exists(Path.Combine(directory, NativeLibraryFile));
        }

        public static string LogLine(string directory)
        {
            if (!Included)
            {
                return LogTag + " sdk=missing (place the EOS C# SDK in dotnet/ThirdParty/EOSSDK and rebuild)";
            }

            if (!HasNativeLibrary(directory))
            {
                return LogTag + " sdk=included library=" + NativeLibraryFile
                    + " (missing; place it in dotnet/ThirdParty/EOSSDK/Bin/<rid> and rebuild)";
            }

            return LogTag + " sdk=included library=" + NativeLibraryFile + " version=" + NativeVersion();
        }
    }
}
