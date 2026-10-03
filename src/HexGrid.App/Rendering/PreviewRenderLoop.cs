using System.Diagnostics;
using System.Drawing;

namespace HexGrid.App.Rendering;

/// <summary>
/// Renders preview frames on a background thread, one at a time, always for the newest view.
/// </summary>
/// <remarks>
/// Every member runs on the UI thread. Only the rasterising runs on the thread pool, with a
/// <see cref="SceneRasterizer"/> this loop owns, because the rasterizer caches GDI+ objects without
/// locking. Requests are never queued: after each frame the loop asks <c>nextRequest</c> again, so
/// however many views went by while a frame was drawn, only the latest one is rendered next.
/// </remarks>
/// <param name="nextRequest">The frame the view needs now, or null when the frame it has already covers it.</param>
/// <param name="deliver">Takes ownership of a finished frame.</param>
/// <param name="fail">Reports a render that failed for any reason other than being superseded.</param>
internal sealed class PreviewRenderLoop(
    Func<PreviewRequest?> nextRequest,
    Action<PreviewRequest, Bitmap> deliver,
    Action<Exception> fail) : IDisposable
{
    private readonly SceneRasterizer _rasterizer = new();
    private PreviewRequest? _inFlight;

    // SS066, IDISP003: _cancel only borrows the token source of the frame in flight, so Kick and
    // Dispose can stop it. RunAsync owns that source (its using declaration) and disposes it after
    // the frame ends, so this field never holds the last reference to anything undisposed.
#pragma warning disable SS066
    private CancellationTokenSource? _cancel;
#pragma warning restore SS066
    private bool _running;
    private bool _disposed;

    /// <summary>Starts rendering if the view needs a frame, or stops a frame the view no longer wants.</summary>
    public void Kick()
    {
        if (_disposed)
        {
            return;
        }

        if (!_running)
        {
            // RunAsync reports its own failures through fail, so there is nothing to observe here.
            _ = RunAsync();
            return;
        }

        // A frame of another scene or scale is useless once drawn, so stop it and let the newest one
        // start sooner. A frame at the same scale still fills in part of a pan, so it is left to finish.
        if (_inFlight is { } current && nextRequest() is { } next && !current.SameSceneAndScale(next))
        {
            _cancel?.Cancel();
        }
    }

    private async Task RunAsync()
    {
        // Each await below resumes through the context current here. Only the WinForms one brings
        // the loop back to the UI thread. Application.Run keeps it installed for every event handler.
        Debug.Assert(
            SynchronizationContext.Current is WindowsFormsSynchronizationContext,
            "PreviewRenderLoop must be started from the UI thread's message loop.");
        _running = true;
        try
        {
            while (!_disposed && nextRequest() is { } request)
            {
                // ConfigureAwait(true) here and below: the loop reads and writes UI state between
                // frames, so it has to resume on the UI thread. MA0004's library default would be wrong.
                await RenderOneAsync(request).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            // Nothing awaits this task, so a failure has to be reported here or it is lost. The loop
            // stops, and the next view change starts it again.
            if (!_disposed)
            {
                fail(ex);
            }
        }
        finally
        {
            _running = false;
            if (_disposed)
            {
                _rasterizer.Dispose();
            }
        }
    }

    private async Task RenderOneAsync(PreviewRequest request)
    {
        using var cancel = new CancellationTokenSource();
#pragma warning disable IDISP003 // borrowed, see the field
        _cancel = cancel;
#pragma warning restore IDISP003
        _inFlight = request;
        try
        {
            // IDISP001: the frame is either disposed below or handed to deliver, which owns it.
#pragma warning disable IDISP001
            Bitmap frame = await Task.Run(() => Render(request, cancel.Token), cancel.Token).ConfigureAwait(true);
#pragma warning restore IDISP001
            if (_disposed)
            {
                frame.Dispose();
            }
            else
            {
                deliver(request, frame);
            }
        }
        catch (OperationCanceledException) when (cancel.IsCancellationRequested)
        {
            // Superseded by a newer view, which the next pass of the loop renders.
        }
        finally
        {
#pragma warning disable IDISP003 // borrowed, see the field
            _cancel = null;
#pragma warning restore IDISP003
            _inFlight = null;
        }
    }

    private Bitmap Render(PreviewRequest request, CancellationToken cancellationToken) =>
        _rasterizer.RenderRegion(
            request.Scene, request.Background, request.Antialias, request.Scale, request.Region, request.MinStrokePx, cancellationToken);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cancel?.Cancel();

        // A frame still being drawn is using the rasterizer. RunAsync disposes it once that frame stops.
        if (!_running)
        {
            _rasterizer.Dispose();
        }
    }
}
