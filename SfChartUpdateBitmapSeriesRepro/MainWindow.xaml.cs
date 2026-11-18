using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace SfChartUpdateBitmapSeriesRepro;

public sealed class DataPoint(double x, double y)
{
    public double X { get; } = x;
    public double Y { get; } = y;
}

/// <summary>
/// Adds a batch of points with a single Reset notification instead of one Add per point.
/// SfChart handles Reset for bitmap series by calling SfChart.UpdateBitmapSeries.
/// </summary>
public sealed class BatchCollection<T> : ObservableCollection<T>
{
    private bool _isBatchUpdate;

    public void AddBatch(IEnumerable<T> items, int maxCount)
    {
        _isBatchUpdate = true;
        foreach (var item in items)
            Add(item);
        while (Count > maxCount)
            RemoveAt(0);
        _isBatchUpdate = false;

        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        if (!_isBatchUpdate)
            base.OnCollectionChanged(e);
    }
}

public partial class MainWindow : Window
{
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private readonly BatchCollection<DataPoint> _data = [];
    private readonly Random _random = new(1);
    private readonly ManualResetEventSlim _stopResizing = new();
    private double _nextX;
    private int _exceptionCount;

    public MainWindow()
    {
        InitializeComponent();
        Series.ItemsSource = _data;

        Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;

        // Add 20 points every 10 ms as one batch (one Reset notification).
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(AddBatch));
        timer.Start();

        SourceInitialized += (_, _) => StartResizing();
        Closed += (_, _) => _stopResizing.Set();
    }

    private void AddBatch()
    {
        var batch = new List<DataPoint>();
        for (var i = 0; i < 20; i++)
        {
            batch.Add(new DataPoint(_nextX, Math.Sin(_nextX / 50) * 100 + _random.NextDouble() * 10));
            _nextX++;
        }

        _data.AddBatch(batch, 5000);
    }

    /// <summary>
    /// Resizes the window from another thread. SetWindowPos sends the size messages synchronously to the UI thread,
    /// where they are dispatched while WriteableBitmap.Lock() waits for the render thread. This is the same kind of
    /// re-entrant layout that happens when a host window resizes or a Remote Desktop session reconnects.
    /// </summary>
    private void StartResizing()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var resizer = new Thread(() =>
        {
            var big = true;
            while (!_stopResizing.IsSet)
            {
                SetWindowPos(hwnd, IntPtr.Zero, 0, 0, big ? 1200 : 1180, big ? 800 : 780, SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE);
                big = !big;
                Thread.Sleep(1);
            }
        })
        { IsBackground = true };
        resizer.Start();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        // Handled only so that the sample keeps running and counting; without this the application crashes.
        e.Handled = true;
        _exceptionCount++;
        Status.Text = $"Unhandled exceptions: {_exceptionCount} (first one below)";
        if (_exceptionCount == 1)
            FirstException.Text = e.Exception.ToString();
    }
}
