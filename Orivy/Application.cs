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
using System.Threading;
using System.Threading.Tasks;
using static Orivy.Native.Windows.Methods;

namespace Orivy;

/// <summary>
/// Application lifetime, message loop, and UI-thread async helpers.
/// </summary>
/// <remarks>
/// <para>
/// Orivy installs a UI <see cref="SynchronizationContext"/> for the message loop (WinForms-style).
/// After <c>await</c>, execution resumes on the UI thread — do any UI work directly.
/// </para>
/// <para>
/// Control properties such as <c>Text</c>, <c>Visible</c>, <c>Enabled</c>, colors, and
/// <c>ProgressBar.Value</c> may also be assigned from a background thread; Orivy marshals those
/// updates to the UI thread automatically. Prefer writing <c>label.Text = "…"</c> over
/// <see cref="BindToUiThread{T}(Action{T})"/> / <c>BeginInvoke</c>.
/// </para>
/// </remarks>
public class Application
{
    private static readonly List<WindowBase> _openForms = new();
    private static SKFont? _defaultFont;
    private static WindowBase _activeForm = null!;
    private static bool _dpiAwarenessSet;
    private static int _messageLoopDepth;
    private static OrivySynchronizationContext? _uiSynchronizationContext;

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
    /// Posts <paramref name="action"/> to a live window's UI thread. Theme transitions tick off
    /// the message loop after <c>ConfigureAwait(false)</c>; subscribers must not write styles or
    /// paints from that pool thread. With no HWND yet the action runs inline.
    /// </summary>
    internal static void PostToUiThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var window = FindUiInvokeTarget();
        if (window == null)
        {
            action();
            return;
        }

        window.BeginInvoke(action);
    }

    /// <summary>
    /// Finds a window with a live HWND that can marshal work onto the UI thread.
    /// </summary>
    internal static WindowBase? FindUiInvokeTarget()
    {
        if (_activeForm is { IsHandleCreated: true })
            return _activeForm;

        for (var i = 0; i < _openForms.Count; i++)
        {
            if (_openForms[i].IsHandleCreated)
                return _openForms[i];
        }

        return null;
    }

    /// <summary>
    /// Installs <see cref="OrivySynchronizationContext"/> on the current (UI) thread so
    /// <c>await</c> continuations resume on the UI message loop (WinForms-compatible).
    /// </summary>
    /// <remarks>
    /// Call only from the UI thread (<see cref="Controls.WindowBase"/> handle creation / message loop).
    /// Does not attach the context to thread-pool threads.
    /// </remarks>
    internal static void EnsureSynchronizationContext()
    {
        _uiSynchronizationContext ??= new OrivySynchronizationContext();

        if (SynchronizationContext.Current is OrivySynchronizationContext)
            return;

        SynchronizationContext.SetSynchronizationContext(_uiSynchronizationContext);
    }

    private static OrivySynchronizationContext GetUiSynchronizationContext()
        => _uiSynchronizationContext
           ?? throw new InvalidOperationException(
               "UI SynchronizationContext is not available yet. Create/show a window first.");

    /// <summary>
    /// Gets whether the caller is already on the UI thread that owns an open window.
    /// </summary>
    public static bool IsUiThread
    {
        get
        {
            var window = FindUiInvokeTarget();
            return window != null && window.CheckAccess();
        }
    }

    /// <summary>
    /// Yields once to the UI message loop, then continues on the UI thread.
    /// </summary>
    /// <remarks>
    /// Use between chunks of work that must stay on the UI thread so paint/input/animations
    /// can run without <c>Task.Run</c> / a background thread. A single long chunk still blocks
    /// until it returns; yield between steps, not instead of splitting work.
    /// </remarks>
    public static Task YieldAsync()
    {
        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var window = FindUiInvokeTarget();
        if (window == null)
        {
            // No HWND yet — nothing to pump; complete on the next thread-pool turn.
            ThreadPool.QueueUserWorkItem(_ => tcs.TrySetResult());
            return tcs.Task;
        }

        window.BeginInvoke(() => tcs.TrySetResult());
        return tcs.Task;
    }

    /// <summary>
    /// Runs <paramref name="action"/> on the UI thread (inline if already there).
    /// Use for arbitrary UI work from a background thread — not limited to progress updates.
    /// </summary>
    /// <example>
    /// <code>
    /// await Task.Run(async () =>
    /// {
    ///     var data = LoadData();
    ///     await Application.InvokeAsync(() =>
    ///     {
    ///         titleLabel.Text = data.Title;
    ///         list.Items.Clear();
    ///         // …any UI work
    ///     });
    /// });
    /// </code>
    /// </example>
    public static Task InvokeAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var window = FindUiInvokeTarget();
        if (window == null)
        {
            action();
            return Task.CompletedTask;
        }

        if (window.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        window.BeginInvoke(() =>
        {
            try
            {
                action();
                tcs.TrySetResult();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Runs <paramref name="func"/> on the UI thread and returns its result.
    /// </summary>
    public static Task<T> InvokeAsync<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        var window = FindUiInvokeTarget();
        if (window == null || window.CheckAccess())
            return Task.FromResult(func());

        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        window.BeginInvoke(() =>
        {
            try
            {
                tcs.TrySetResult(func());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Runs an async UI operation on the UI thread and returns when it completes.
    /// </summary>
    public static Task InvokeAsync(Func<Task> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        var window = FindUiInvokeTarget();
        if (window == null || window.CheckAccess())
            return func();

        return window.InvokeAsync(func);
    }

    /// <summary>
    /// Runs an async UI operation on the UI thread and returns its result.
    /// </summary>
    public static Task<T> InvokeAsync<T>(Func<Task<T>> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        var window = FindUiInvokeTarget();
        if (window == null || window.CheckAccess())
            return func();

        return window.InvokeAsync(func);
    }

    /// <summary>
    /// Wraps a callback so every invocation is posted to the UI thread — any UI work is allowed inside.
    /// </summary>
    /// <remarks>
    /// For one-shot / awaited UI work prefer <see cref="InvokeAsync(Action)"/>.
    /// Use this when a background API takes a synchronous callback (events, progress delegates, …).
    /// </remarks>
    public static Action BindToUiThread(Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var context = GetUiSynchronizationContext();
        return () => context.Post(_ => callback(), null);
    }

    /// <inheritdoc cref="BindToUiThread(Action)"/>
    public static Action<T> BindToUiThread<T>(Action<T> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var context = GetUiSynchronizationContext();
        return value => context.Post(_ => callback(value), null);
    }

    /// <inheritdoc cref="BindToUiThread(Action)"/>
    public static Action<T1, T2> BindToUiThread<T1, T2>(Action<T1, T2> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var context = GetUiSynchronizationContext();
        return (a, b) => context.Post(_ => callback(a, b), null);
    }

    /// <summary>
    /// Marks that a UI message loop is running on this thread (<see cref="Run"/> or modal
    /// <c>ShowDialog</c>) and ensures a UI <see cref="SynchronizationContext"/> is installed.
    /// </summary>
    internal static void EnterMessageLoop()
    {
        EnsureSynchronizationContext();
        _messageLoopDepth++;
        MessageLoop = true;
    }

    /// <summary>
    /// Leaves a nested UI message loop. Clears <see cref="MessageLoop"/> when the outermost loop exits.
    /// </summary>
    internal static void ExitMessageLoop()
    {
        if (_messageLoopDepth > 0)
            _messageLoopDepth--;

        if (_messageLoopDepth == 0)
            MessageLoop = false;
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
            EnterMessageLoop();
            window.Show();

            while (!ExitRequested)
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
            ExitMessageLoop();
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
    /// Gets the product description associated with the application (from AssemblyDescriptionAttribute).
    /// </summary>
    public static string ProductDescription =>
        GetEntryAttribute<AssemblyDescriptionAttribute>()?.Description
        ?? string.Empty;

    /// <summary>
    /// Gets the product version associated with the application (from AssemblyInformationalVersion,
    /// falling back to the assembly version).
    /// </summary>
    public static string ProductVersion =>
        GetEntryAttribute<AssemblyVersionAttribute>()?.Version 
        ?? GetEntryAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
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
    /// <summary>
    /// True once <see cref="Exit"/> has been called. A modal <c>ShowDialog()</c> runs its own nested
    /// GetMessage loop that only watches for its own Close() — it has no way to know an Exit() posted
    /// WM_QUIT elsewhere, and Win32 delivers a posted WM_QUIT to whichever GetMessage call happens to
    /// dequeue it, not necessarily the outermost Run() loop. Every loop in the framework should check
    /// this flag cooperatively so a dialog left open during Exit() doesn't strand the process running
    /// with no visible window.
    /// </summary>
    internal static bool ExitRequested { get; private set; }

    /// <summary>
    /// Informs all message pumps that they must terminate, then closes all open forms after the
    /// messages have been processed. Ends the <see cref="Run(WindowBase)"/> message loop.
    /// </summary>
    public static void Exit()
    {
        ExitRequested = true;

        // Close every open form first (fires FormClosing/FormClosed). Iterate a snapshot because
        // closing mutates _openForms.
        foreach (var form in _openForms.ToArray())
        {
            try { form.Close(); }
            catch (Exception ex) { Debug.WriteLine("Application.Exit: error closing form: " + ex); }
        }

        RaiseApplicationExit();

        // Post WM_QUIT so the Run() / ShowDialog() GetMessage loop returns 0 and unwinds. Belt and
        // suspenders alongside ExitRequested above — WM_QUIT still handles the common case cleanly
        // (and is what makes e.g. GetMessage return promptly instead of blocking), but ExitRequested
        // is what guarantees a nested ShowDialog() loop actually stops even if it dequeues messages
        // other than the WM_QUIT itself first.
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