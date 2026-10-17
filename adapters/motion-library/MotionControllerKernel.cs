using System.Runtime.InteropServices;

namespace Wjss.Adapters.MotionLibrary;

/// <summary>
/// The B1 identity surface's view of the operating-system loading and export-invocation
/// primitives. Internal and not part of any public surface: it exists so the complete B1 defect
/// class (missing export, wrong invocation order, failed close, failed unload, dead buffer,
/// late generation) is exercised by focused tests without any real native module and without any
/// controller. It exposes ONLY what the one-shot identity session needs: load one approved
/// module, read the loaded module path, resolve one named export, invoke the six authorized
/// identity exports, release one module. There is no raw-command, discovery, subscription or
/// motion concept here and none can be added through this interface.
///
/// This is a SIBLING of the probe seam (<see cref="INativeKernel"/>, which remains pinned to the
/// single version export). Both surfaces reuse the same restricted loader
/// (<see cref="NativeMethods"/>): there is exactly one loader in this boundary.
/// </summary>
internal interface IControllerKernel
{
    /// <summary>Loads one approved module by absolute path with the restricted flag set. Zero on failure.</summary>
    IntPtr LoadApprovedModule(string approvedAbsolutePath);

    /// <summary>The filesystem path the module was loaded from, or null when it cannot be determined.</summary>
    string? GetLoadedModulePath(IntPtr module);

    /// <summary>Resolves exactly one export by name in one loaded module. Zero when the export is absent.</summary>
    IntPtr ResolveExport(IntPtr module, string exportName);

    /// <summary>Releases one module handle. False when the release could not be performed.</summary>
    bool ReleaseModule(IntPtr module);

    /// <summary>Invokes gclib_force_gcaps with the zero byte meaning "no gcaps, direct only".</summary>
    void InvokeForceDirectMode(IntPtr export, byte directOnly);

    /// <summary>Invokes gclib_open once with the bounded controller-address buffer.</summary>
    int InvokeOpen(IntPtr export, byte[] addressBuffer, out IntPtr handle);

    /// <summary>Invokes gclib_close; the handle must come back zeroed on success.</summary>
    int InvokeClose(IntPtr export, ref IntPtr handle);

    /// <summary>Invokes gclib_connection_type.</summary>
    int InvokeConnectionType(IntPtr export, IntPtr handle, out int connectionType);

    /// <summary>Invokes gclib_revision_information into the caller's bounded buffer.</summary>
    int InvokeRevisionInformation(IntPtr export, IntPtr handle, byte[] buffer);

    /// <summary>Invokes gclib_serial_number.</summary>
    int InvokeSerialNumber(IntPtr export, IntPtr handle, out uint serialNumber);
}

/// <summary>
/// The production B1 kernel: thin, guarded, and only ever used by the Owner-invoked identity
/// harness. Every delegate below implements exactly the installed-header declaration recorded in
/// <see cref="MotionControllerApi"/>; no vendor error constant is inferred and no result value is
/// interpreted beyond the classification rules of the session. The delegates are declared for
/// the 64-bit Windows process the runner requires (the same ABI reasoning as the probe's version
/// delegate): no x86 calling-convention ambiguity exists at this boundary.
///
/// The vendor module is reachable ONLY through the restricted absolute-path loader of
/// <see cref="NativeMethods"/>; there is no static import of the vendor library and none may be
/// added (see the source-boundary tests).
/// </summary>
internal sealed class WindowsControllerKernel : IControllerKernel
{
    // Verbatim installed-header declarations (see MotionControllerApi):
    //     void         gclib_force_gcaps(bool on);
    //     gclib_result gclib_open(gclib_handle *h, const char *address);
    //     gclib_result gclib_close(gclib_handle *h);
    //     gclib_result gclib_connection_type(gclib_handle h, gclib_connection_t *type);
    //     gclib_result gclib_revision_information(gclib_handle h, char *rev_info, size_t len);
    //     gclib_result gclib_serial_number(gclib_handle h, uint32_t *serial_number);
    //
    // C `bool` marshals as one byte; gclib_result and gclib_connection_t are C enums (32-bit);
    // size_t is pointer-sized (UIntPtr on the 64-bit-only path); gclib_handle is a pointer.

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void ForceDirectModeExport(byte directOnly);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int OpenExport(out IntPtr handle, byte[] addressBuffer);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int CloseExport(ref IntPtr handle);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int ConnectionTypeExport(IntPtr handle, out int connectionType);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RevisionInformationExport(IntPtr handle, byte[] buffer, UIntPtr length);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SerialNumberExport(IntPtr handle, out uint serialNumber);

    /// <inheritdoc/>
    public IntPtr LoadApprovedModule(string approvedAbsolutePath) =>
        NativeMethods.LoadLibraryExW(approvedAbsolutePath, IntPtr.Zero, NativeMethods.ApprovedLoadFlags);

    /// <inheritdoc/>
    public string? GetLoadedModulePath(IntPtr module)
    {
        var buffer = new char[1024];
        var length = NativeMethods.GetModuleFileNameW(module, buffer, (uint)buffer.Length);
        if (length == 0 || length >= buffer.Length)
        {
            return null;
        }

        return new string(buffer, 0, (int)length);
    }

    /// <inheritdoc/>
    public IntPtr ResolveExport(IntPtr module, string exportName)
    {
        ArgumentNullException.ThrowIfNull(exportName);
        var wireName = new byte[exportName.Length + 1];
        _ = System.Text.Encoding.ASCII.GetBytes(exportName, 0, exportName.Length, wireName, 0);
        return NativeMethods.GetProcAddress(module, wireName);
    }

    /// <inheritdoc/>
    public bool ReleaseModule(IntPtr module) => NativeMethods.FreeLibrary(module);

    /// <inheritdoc/>
    public void InvokeForceDirectMode(IntPtr export, byte directOnly) =>
        Marshal.GetDelegateForFunctionPointer<ForceDirectModeExport>(export)(directOnly);

    /// <inheritdoc/>
    public int InvokeOpen(IntPtr export, byte[] addressBuffer, out IntPtr handle)
    {
        ArgumentNullException.ThrowIfNull(addressBuffer);
        return Marshal.GetDelegateForFunctionPointer<OpenExport>(export)(out handle, addressBuffer);
    }

    /// <inheritdoc/>
    public int InvokeClose(IntPtr export, ref IntPtr handle) =>
        Marshal.GetDelegateForFunctionPointer<CloseExport>(export)(ref handle);

    /// <inheritdoc/>
    public int InvokeConnectionType(IntPtr export, IntPtr handle, out int connectionType) =>
        Marshal.GetDelegateForFunctionPointer<ConnectionTypeExport>(export)(handle, out connectionType);

    /// <inheritdoc/>
    public int InvokeRevisionInformation(IntPtr export, IntPtr handle, byte[] buffer)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        return Marshal.GetDelegateForFunctionPointer<RevisionInformationExport>(export)(handle, buffer, (UIntPtr)buffer.Length);
    }

    /// <inheritdoc/>
    public int InvokeSerialNumber(IntPtr export, IntPtr handle, out uint serialNumber) =>
        Marshal.GetDelegateForFunctionPointer<SerialNumberExport>(export)(handle, out serialNumber);
}
