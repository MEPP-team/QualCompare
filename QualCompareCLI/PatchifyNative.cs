using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace QualCompareCLI;

internal static class PatchifyNative
{
    static PatchifyNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(PatchifyNative).Assembly, ResolveNativeLibrary);
    }

    [DllImport("patchify_c", EntryPoint = "patchify_process_image", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int PatchifyProcessImage(string imagePath);

    [DllImport("patchify_c", EntryPoint = "patchify_process_folder", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    private static extern int PatchifyProcessFolder(string folderPath);

    public static int ProcessImage(string imagePath) => PatchifyProcessImage(imagePath);

    public static int ProcessFolder(string folderPath) => PatchifyProcessFolder(folderPath);

    private static IntPtr ResolveNativeLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "patchify_c", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        foreach (var candidate in GetCandidatePaths())
        {
            if (!File.Exists(candidate))
                continue;

            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
                return handle;
        }

        return IntPtr.Zero;
    }

    private static string[] GetCandidatePaths()
    {
        var nativeFileName = OperatingSystem.IsWindows()
            ? "patchify_c.dll"
            : OperatingSystem.IsMacOS()
                ? "libpatchify_c.dylib"
                : "libpatchify_c.so";

        var baseDirectory = AppContext.BaseDirectory;
        var currentDirectory = Directory.GetCurrentDirectory();
        var parentDirectory = Directory.GetParent(baseDirectory)?.FullName;

        var candidates = new List<string>();

        // Documented override (see README "Native library deployment"):
        // QUALCOMPARE_PATCHIFY_PATH holds the full path to the native library.
        var envPath = Environment.GetEnvironmentVariable("QUALCOMPARE_PATCHIFY_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
            candidates.Add(envPath);

        candidates.Add(Path.Combine(baseDirectory, nativeFileName));
        candidates.Add(Path.Combine(baseDirectory, "..", nativeFileName));
        if (parentDirectory is not null)
            candidates.Add(Path.Combine(parentDirectory, nativeFileName));
        candidates.Add(Path.Combine(currentDirectory, nativeFileName));
        candidates.Add(Path.Combine(currentDirectory, "patchify", "build", "Debug", nativeFileName));
        candidates.Add(Path.Combine(currentDirectory, "patchify", "build", "Release", nativeFileName));
        candidates.Add(Path.Combine(currentDirectory, "patchify", "build", "MinSizeRel", nativeFileName));
        candidates.Add(Path.Combine(currentDirectory, "patchify", "build", "RelWithDebInfo", nativeFileName));

        return candidates.ToArray();
    }
}