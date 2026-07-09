using Orivy.Controls;
using Orivy.Extensions;
using Orivy.Native.Windows;
using Orivy.Rendering;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using static Orivy.Native.Windows.Methods;

namespace Orivy;

public class Application
{
    private static readonly List<WindowBase> _openForms = new();
    private static SKFont? _defaultFont;
    private static WindowBase _activeForm = null!;
    private static bool _dpiAwarenessSet;

    /// <summary>
    /// Static constructor ensures DPI awareness is set before ANY window is created.
    /// This runs the first time any Application member is accessed, including
    /// when WindowBase constructor references Application indirectly.
    /// </summary>
    static Application()
    {
        EnableDpiAwareness();
        _defaultFont = CreateDefaultFont();
    }

    internal static SKFont SharedDefaultFont => _defaultFont ??= CreateDefaultFont();

    public static SKFont DefaultFont
    {
        get => SharedDefaultFont.CloneFont();
        set
        {
            var replacement = (value ?? CreateDefaultFont()).CloneFont();

            if (_defaultFont.FontEquals(replacement))
            {
                replacement.Dispose();
                return;
            }

            _defaultFont?.Dispose();
            _defaultFont = replacement;
            NotifyDefaultFontChanged();
        }
    }

    public static IReadOnlyList<WindowBase> OpenForms => _openForms.AsReadOnly();

    public static WindowBase ActiveForm
    {
        get => _activeForm;
        internal set
        {
            if (_activeForm == value) return;
            _activeForm = value;
        }
    }

    internal static IntPtr GetNotifyIconWindowHandle()
    {
        return _activeForm?.Handle ?? IntPtr.Zero;
    }

    /// <summary>
    /// Sets the process DPI awareness to Per-Monitor V2 (Win10 1703+),
    /// falling back to Per-Monitor (Win8.1+).
    /// Call before creating any windows.
    /// </summary>
    public static void EnableDpiAwareness()
    {
        if (_dpiAwarenessSet)
            return;

        _dpiAwarenessSet = true;

        if (Environment.OSVersion.Version >= new Version(10, 0, 15063))
        {
            try
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
                return;
            }
            catch (EntryPointNotFoundException) { }
        }

        try
        {
            SetProcessDpiAwareness(2); // PROCESS_PER_MONITOR_DPI_AWARE
        }
        catch (EntryPointNotFoundException) { }
    }

    internal static void RegisterForm(WindowBase form)
    {
        if (form == null || _openForms.Contains(form))
            return;

        _openForms.Add(form);
        _activeForm = form;
    }

    internal static void UnregisterForm(WindowBase form)
    {
        if (form == null)
            return;

        _openForms.Remove(form);

        if (_activeForm == form)
            _activeForm = _openForms.LastOrDefault()!;
    }

    internal static void SetActiveForm(WindowBase form)
    {
        if (form == null || !_openForms.Contains(form))
            return;

        _activeForm = form;
    }

    /// <summary>
    /// Gets or sets the default rendering backend for all windows.
    /// Set this before creating any windows to affect the initial renderer selection.
    /// </summary>
    public static RenderBackend RenderBackend { get; set; } = ResolveDefaultRenderBackend();

    private static RenderBackend ResolveDefaultRenderBackend()
    {
        return OperatingSystem.IsWindows() ? RenderBackend.OpenGL : RenderBackend.Software;
    }

    /// <summary>
    /// Gets a value indicating whether a message loop exists on this thread.
    /// </summary>
    public static bool MessageLoop { get; private set; }

    public static void Run(WindowBase window)
	{
		try
		{
            EnableDpiAwareness();

            if (!window.IsHandleCreated)
                window.CreateHandle();

            // Set before Show() so Application.MessageLoop is already true during Load/Shown handlers.
            MessageLoop = true;
            window.Show();

            while (true)
			{
                // Raise Idle right before the loop would block on an empty queue (WinForms semantics).
                if (Idle != null && !PeekMessage(out _, IntPtr.Zero, 0, 0, 0 /* PM_NOREMOVE */))
                    RaiseIdle();

                if (GetMessage(out MSG msg, IntPtr.Zero, 0, 0) <= 0) // 0 = WM_QUIT, -1 = error
                    break;

				try
				{
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
				catch (Exception e)
				{
                    Debug.WriteLine("Exception in message loop: " + e.ToString());
				}
            }
        }
		catch (Exception ex)
		{
            DefWindowProc(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero);
            Debug.WriteLine("Exception in Application.Run: " + ex.ToString());
		}
        finally
        {
            MessageLoop = false;
            RaiseApplicationExit();
        }
    }

    public static void DoEvents()
    {
        while (PeekMessage(out var msg, IntPtr.Zero, 0, 0, 0x0001)) // PM_REMOVE
        {
            TranslateMessage(ref msg);
            DispatchMessage(ref msg);
        }
    }

    private static SKFont CreateDefaultFont()
    {
        return CreateUiFont(ResolveDefaultTypeface(), 9.75f);
    }

    internal static SKFont CreateUiFont(SKTypeface? typeface, float size)
    {
        var font = new SKFont(typeface ?? SKTypeface.Default, size);
        ApplyPreferredFontRendering(font);
        return font;
    }

    internal static void ApplyPreferredFontRendering(SKFont font)
    {
        ArgumentNullException.ThrowIfNull(font);

        font.Subpixel = true;
        font.Edging = SKFontEdging.SubpixelAntialias;
        font.Hinting = SKFontHinting.Full;
        font.LinearMetrics = true;
    }

    private static SKTypeface ResolveDefaultTypeface()
    {
        var defaultTypeFace = SKTypeface.Default;
        return SKTypeface.FromFamilyName("Inter")
            ?? (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? SKTypeface.FromFamilyName("Segoe UI")
                : SKTypeface.FromFamilyName("Segoe UI Variable"))
            ?? defaultTypeFace;
    }

    private static void NotifyDefaultFontChanged()
    {
        for (var i = 0; i < _openForms.Count; i++)
        {
            _openForms[i].HandleDefaultFontChanged();
        }
    }

    public static void Restart()
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = Environment.ProcessPath,
            Arguments = Environment.CommandLine,
            UseShellExecute = true
        });
        Environment.Exit(0);
    }

    #region WinForms-compatible application info & lifecycle

    /// <summary>
    /// Occurs when the application is about to shut down (after the message loop exits, or when
    /// <see cref="Exit()"/> is called).
    /// </summary>
    public static event EventHandler? ApplicationExit;

    /// <summary>
    /// Occurs when the application finishes processing and is about to enter the idle state
    /// (raised from <see cref="Idle"/>-aware pumps such as <see cref="DoEvents"/>).
    /// </summary>
    public static event EventHandler? Idle;

    private static bool _applicationExitRaised;

    /// <summary>
    /// Gets the full path of the executable that started the application, including the file name.
    /// </summary>
    public static string ExecutablePath =>
        Environment.ProcessPath
        ?? (TryGetProcessMainModulePath() is { Length: > 0 } p ? p : null)
        ?? Assembly.GetEntryAssembly()?.Location
        ?? string.Empty;

    /// <summary>
    /// Gets the path for the executable file that started the application, not including the file name.
    /// </summary>
    public static string StartupPath => Path.GetDirectoryName(ExecutablePath) ?? string.Empty;

    /// <summary>
    /// Gets the product name associated with the application (from AssemblyProductAttribute).
    /// </summary>
    public static string ProductName =>
        GetEntryAttribute<AssemblyProductAttribute>()?.Product
        ?? EntryAssemblyName;

    /// <summary>
    /// Gets the product version associated with the application (from AssemblyInformationalVersion,
    /// falling back to the assembly version).
    /// </summary>
    public static string ProductVersion =>
        GetEntryAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
        ?? "1.0.0.0";

    /// <summary>
    /// Gets the company name associated with the application (from AssemblyCompanyAttribute).
    /// </summary>
    public static string CompanyName =>
        GetEntryAttribute<AssemblyCompanyAttribute>()?.Company
        ?? EntryAssemblyName;

    /// <summary>
    /// Gets the path for the application data shared among all users
    /// (CommonApplicationData\CompanyName\ProductName\ProductVersion).
    /// </summary>
    public static string CommonAppDataPath => BuildAppDataPath(Environment.SpecialFolder.CommonApplicationData);

    /// <summary>
    /// Gets the path for the application data of the current, roaming user
    /// (ApplicationData\CompanyName\ProductName\ProductVersion).
    /// </summary>
    public static string UserAppDataPath => BuildAppDataPath(Environment.SpecialFolder.ApplicationData);

    /// <summary>
    /// Gets the path for the application data of the current, non-roaming user
    /// (LocalApplicationData\CompanyName\ProductName\ProductVersion).
    /// </summary>
    public static string LocalUserAppDataPath => BuildAppDataPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>
    /// Informs all message pumps that they must terminate, then closes all open forms after the
    /// messages have been processed. Ends the <see cref="Run(WindowBase)"/> message loop.
    /// </summary>
    public static void Exit()
    {
        // Close every open form first (fires FormClosing/FormClosed). Iterate a snapshot because
        // closing mutates _openForms.
        foreach (var form in _openForms.ToArray())
        {
            try { form.Close(); }
            catch (Exception ex) { Debug.WriteLine("Application.Exit: error closing form: " + ex); }
        }

        RaiseApplicationExit();

        // Post WM_QUIT so the Run() / ShowDialog() GetMessage loop returns 0 and unwinds.
        PostQuitMessage(0);
    }

    /// <summary>
    /// Exits the message loop on the current thread and closes all windows on the thread.
    /// Equivalent to <see cref="Exit()"/> in Orivy's single-UI-thread model.
    /// </summary>
    public static void ExitThread() => Exit();

    /// <summary>
    /// WinForms compatibility no-op. Orivy renders its own controls, so there is no Win32 common
    /// control visual-styles context to enable. Provided so migrated Program.cs code compiles.
    /// </summary>
    public static void EnableVisualStyles() { }

    /// <summary>
    /// WinForms compatibility no-op. Orivy always renders text via SkiaSharp, so there is no
    /// GDI/GDI+ text-rendering default to configure. Provided so migrated Program.cs code compiles.
    /// </summary>
    public static void SetCompatibleTextRenderingDefault(bool defaultValue) { }

    internal static void RaiseIdle()
    {
        Idle?.Invoke(null, EventArgs.Empty);
    }

    private static void RaiseApplicationExit()
    {
        if (_applicationExitRaised)
            return;

        _applicationExitRaised = true;
        ApplicationExit?.Invoke(null, EventArgs.Empty);
    }

    private static string EntryAssemblyName =>
        Assembly.GetEntryAssembly()?.GetName().Name ?? "Application";

    private static T? GetEntryAttribute<T>() where T : Attribute =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<T>();

    private static string? TryGetProcessMainModulePath()
    {
        try { return Process.GetCurrentProcess().MainModule?.FileName; }
        catch { return null; }
    }

    private static string BuildAppDataPath(Environment.SpecialFolder root)
    {
        var basePath = Environment.GetFolderPath(root);
        var company = SanitizePathSegment(CompanyName);
        var product = SanitizePathSegment(ProductName);
        var version = SanitizePathSegment(ProductVersion);
        return Path.Combine(basePath, company, product, version);
    }

    private static string SanitizePathSegment(string segment)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return "_";

        foreach (var c in Path.GetInvalidFileNameChars())
            segment = segment.Replace(c, '_');

        return segment.Trim();
    }

    #endregion
}