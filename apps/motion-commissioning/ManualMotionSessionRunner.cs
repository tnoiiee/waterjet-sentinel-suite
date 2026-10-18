using Wjss.Runtime.Core.Motion.Manual;

namespace Wjss.MotionCommissioning;

/// <summary>
/// The single driver thread of one Manual Motion Commissioning session. It calls
/// <see cref="ManualMotionSession.Step"/> on one dedicated background thread, so a native call never
/// runs on the UI thread and the UI never blocks a Stop request. The loop ends when the session is
/// terminal (closed, or refused before opening).
///
/// The step cadence below is a software scheduling interval only. Every controller-facing timing
/// (observation interval, freshness, heartbeat, settle, timeouts) comes from the Owner-local profile.
///
/// SOURCE-AUTHORED IN ARENA; NOT COMPILED IN ARENA (the Owner-local build is the validation of record).
/// </summary>
internal sealed class ManualMotionSessionRunner : IDisposable
{
    private const int StepIntervalMilliseconds = 10;

    private readonly ManualMotionSession _session;
    private readonly Thread _thread;
    private volatile bool _abandoned;
    private int _started;

    internal ManualMotionSessionRunner(ManualMotionSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "WJSS manual motion session driver",
        };
    }

    /// <summary>Starts the driver thread once.</summary>
    internal void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _thread.Start();
        }
    }

    /// <summary>Waits for the driver loop to end. True when it ended (or never started).</summary>
    internal bool WaitForTermination(TimeSpan timeout)
    {
        if (Volatile.Read(ref _started) == 0)
        {
            return true;
        }

        return _thread.Join(timeout);
    }

    /// <summary>Abandons the loop without stepping further (used only after the session close was awaited).</summary>
    public void Dispose()
    {
        _abandoned = true;
    }

    private void Run()
    {
        while (!_abandoned)
        {
            try
            {
                _session.Step();
            }
            catch (InvalidOperationException)
            {
                // A concurrent Step is a programming error; never step from two threads.
                return;
            }

            if (_session.Snapshot.IsTerminal)
            {
                return;
            }

            Thread.Sleep(StepIntervalMilliseconds);
        }
    }
}
