using System;
using System.Threading;

namespace Orivy;

/// <summary>
/// Posts/sends work to the Orivy UI thread (window message loop).
/// With this installed, <c>await</c> continuations resume on the UI thread so callers
/// can run any UI code afterwards without manual marshaling (WinForms-compatible DX).
/// </summary>
internal sealed class OrivySynchronizationContext : SynchronizationContext
{
    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        // Always queue (never inline) — matches WinForms and avoids re-entrancy during WndProc.
        Application.PostToUiThread(() => d(state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);

        var window = Application.FindUiInvokeTarget();
        if (window == null || window.CheckAccess())
        {
            d(state);
            return;
        }

        Exception? caught = null;
        window.Invoke(() =>
        {
            try
            {
                d(state);
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });

        if (caught != null)
            throw caught;
    }

    public override SynchronizationContext CreateCopy() => this;
}
