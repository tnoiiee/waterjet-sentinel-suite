using System.Runtime.InteropServices;

namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The raw operating-system loading surface of the native probe boundary. This is the ONLY file
/// that names kernel loading entry points; it is internal, it is never exposed to an application,
/// a runtime, a UI or a route, and it never references the vendor module by P/Invoke: the vendor
/// library is reachable ONLY through an explicit absolute-path load of the approved file, never
/// through import-table resolution, a PATH search or a machine-wide lookup.
///
/// Search-path isolation (the Owner evidence requires it: same-named copies of these modules
/// exist elsewhere on the workstation): the load passes an EXPLICIT restricted flag set —
/// the directory of the module being loaded, the application directory and System32, and
/// NOTHING else. Current directory, user directories, gcaps/GDK locations and the machine-wide
/// PATH are all excluded, so dependency resolution can never silently select a different copy.
/// </summary>
internal static class NativeMethods
{
    internal const int LoadLibrarySearchDllLoadDir = 0x00000100;
    internal const int LoadLibrarySearchApplicationDir = 0x00000200;
    internal const int LoadLibrarySearchSystem32 = 0x00000800;

    /// <summary>
    /// The exact restricted search set: the approved module's own directory, the application
    /// directory and System32. Deliberately EXCLUDES default-directory resolution (current
    /// directory and PATH), so a same-named copy in an unrelated vendor tool directory can never
    /// be selected.
    /// </summary>
    internal const int ApprovedLoadFlags =
        LoadLibrarySearchDllLoadDir | LoadLibrarySearchApplicationDir | LoadLibrarySearchSystem32;

    [DllImport("kernel32.dll", EntryPoint = "LoadLibraryExW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr LoadLibraryExW(string lpFileName, IntPtr hFile, int dwFlags);

    [DllImport("kernel32.dll", EntryPoint = "GetProcAddress", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string lpProcName);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleFileNameW", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    internal static extern uint GetModuleFileNameW(IntPtr hModule, [Out] char[] lpFilename, uint nSize);

    [DllImport("kernel32.dll", EntryPoint = "FreeLibrary", ExactSpelling = true, SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FreeLibrary(IntPtr hModule);
}

/// <summary>
/// The probe's view of the operating-system loading primitives. Internal and not part of any
/// public surface: it exists so the complete defect class of the native load (missing export,
/// wrong module path, failed unload, dead buffer) is exercised by focused tests without any real
/// native module, and so no caller can widen it. It exposes ONLY what the probe needs: load one
/// approved module, resolve one export, read the loaded module's path, invoke that export with a
/// bounded buffer, release one module. There is no open, close, discovery, command, callback or
/// status concept here and none can be added through this interface.
/// </summary>
internal interface INativeKernel
{
    /// <summary>Loads one approved module by absolute path with the restricted flag set. Zero on failure.</summary>
    IntPtr LoadApprovedModule(string approvedAbsolutePath);

    /// <summary>Resolves exactly one export by name in one loaded module. Zero when the export is absent.</summary>
    IntPtr ResolveExport(IntPtr module, string exportName);

    /// <summary>The filesystem path the module was loaded from, or null when it cannot be determined.</summary>
    string? GetLoadedModulePath(IntPtr module);

    /// <summary>Releases one module handle. False when the release could not be performed.</summary>
    bool ReleaseModule(IntPtr module);

    /// <summary>
    /// Invokes the authorized version export with the bounded, zero-filled buffer. The return
    /// value of the native call is delivered uninterpreted: the boundary implements ONLY what the
    /// exact installed-header declaration evidence supports, and no vendor error constant is
    /// inferred.
    /// </summary>
    int InvokeAuthorizedVersionExport(IntPtr exportAddress, byte[] versionBuffer);
}

/// <summary>The production kernel: thin, guarded, and only ever used by the Owner-local probe.</summary>
internal sealed class WindowsNativeKernel : INativeKernel
{
    // The vendor declaration implemented, verbatim from the exact installed header evidence
    // supplied by the Owner for the selected library release:
    //
    //     gclib_result gclib_version(char* version, size_t len);
    //
    // It requires no controller handle, opens no controller connection and returns the library
    // version into a caller-supplied output buffer. On 64-bit Windows there is exactly one
    // calling convention at the ABI level; the delegate is declared for that platform and the
    // probe runs only in a 64-bit Windows process (the runner's platform gate), so no x86
    // calling-convention ambiguity exists at this boundary. The 32-bit result is marshalled as
    // System.Int32 and is never interpreted against any inferred error constant.
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int AuthorizedVersionExport(byte[] versionBuffer, UIntPtr length);

    /// <inheritdoc/>
    public IntPtr LoadApprovedModule(string approvedAbsolutePath) =>
        NativeMethods.LoadLibraryExW(approvedAbsolutePath, IntPtr.Zero, NativeMethods.ApprovedLoadFlags);

    /// <inheritdoc/>
    public IntPtr ResolveExport(IntPtr module, string exportName) =>
        NativeMethods.GetProcAddress(module, exportName);

    /// <inheritdoc/>
    public string? GetLoadedModulePath(IntPtr module)
    {
        var buffer = new char[1024];
        var length = NativeMethods.GetModuleFileNameW(module, buffer, (uint)buffer.Length);
        if (length == 0 || length >= buffer.Length)
        {
            // Zero: the path could not be determined. Capacity-length: truncation, which is never
            // silently accepted as a path proof.
            return null;
        }

        return new string(buffer, 0, (int)length);
    }

    /// <inheritdoc/>
    public bool ReleaseModule(IntPtr module) => NativeMethods.FreeLibrary(module);

    /// <inheritdoc/>
    public int InvokeAuthorizedVersionExport(IntPtr exportAddress, byte[] versionBuffer)
    {
        ArgumentNullException.ThrowIfNull(versionBuffer);
        var export = Marshal.GetDelegateForFunctionPointer<AuthorizedVersionExport>(exportAddress);
        return export(versionBuffer, (UIntPtr)versionBuffer.Length);
    }
}
