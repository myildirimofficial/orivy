using SkiaSharp;
using System.Buffers;
using System.Collections.Generic;
using System;
using System.Threading;
using SharedTimer = System.Timers.Timer;
using TimerElapsedEventArgs = System.Timers.ElapsedEventArgs;

namespace Orivy.Animation;

/// <summary>
///     Modern, optimized animation manager - based on ValueProvider
/// </summary>
public class AnimationManager : IDisposable
{
    private static readonly object s_sync = new();
    private static readonly List<AnimationManager> s_activeManagers = new();
    private static SharedTimer? s_sharedTimer;
    private static int s_tickInProgress;

    private readonly ValueProvider<double> _valueProvider;
    private object[] _animationData;
    private SKPoint _animationSource;
    private AnimationDirection _currentDirection;
    private bool _disposed;
    private bool _registered;

    public AnimationManager(bool singular = true)
    {
        Singular = singular;
        InterruptAnimation = true;
        Increment = 0.15;
        SecondaryIncrement = 0.15;
        AnimationType = AnimationType.EaseInOut;

        _valueProvider = new ValueProvider<double>(0, ValueFactories.DoubleFactory, EasingMethods.DefaultEase);
        _animationData = Array.Empty<object>();
    }

    public bool Singular { get; set; }
    public bool InterruptAnimation { get; set; }
    public double Increment { get; set; }
    public double SecondaryIncrement { get; set; }
    public AnimationType AnimationType { get; set; }

    public bool Running { get; private set; }

    public void Dispose()
    {
        if (_disposed) return;

        UnregisterFromSharedTimer();

        _disposed = true;
    }

    public event Action<object>? OnAnimationProgress;
    public event Action<object>? OnAnimationFinished;

    private static void EnsureSharedTimer()
    {
        if (s_sharedTimer != null)
            return;

        s_sharedTimer = new SharedTimer { Interval = 16, AutoReset = true };
        s_sharedTimer.Elapsed += OnSharedTick;
    }

    public void StartNewAnimation(AnimationDirection direction)
    {
        StartNewAnimation(direction, SKPoint.Empty, Array.Empty<object>());
    }

    public void StartNewAnimation(AnimationDirection direction, SKPoint source)
    {
        StartNewAnimation(direction, source, Array.Empty<object>());
    }

    public void StartNewAnimation(AnimationDirection direction, object[] data)
    {
        StartNewAnimation(direction, SKPoint.Empty, data);
    }

    public void StartNewAnimation(AnimationDirection direction, SKPoint source, object[]? data)
    {
        if (_disposed)
            return;

        if (Running && !InterruptAnimation)
            return;

        _currentDirection = direction;
        _animationSource = source;
        _animationData = data ?? Array.Empty<object>();
        UpdateEasingMethod();

        var target = direction == AnimationDirection.In || direction == AnimationDirection.InOutIn ? 1.0 : 0.0;
        var currentIncrement =
            direction == AnimationDirection.InOutOut || direction == AnimationDirection.InOutRepeatingOut
                ? SecondaryIncrement
                : Increment;
        var duration = Math.Abs(target - _valueProvider.CurrentValue) / currentIncrement * 16; // milliseconds

        _valueProvider.StartTransition(_valueProvider.CurrentValue, target,
            TimeSpan.FromMilliseconds(Math.Max(16, duration)));

        Running = true;
    RegisterWithSharedTimer();
    }

    public double GetProgress()
    {
        return GetProgress(0);
    }

    public double GetProgress(int index)
    {
        return _valueProvider.CurrentValue;
    }

    public SKPoint GetSource()
    {
        return GetSource(0);
    }

    public SKPoint GetSource(int index)
    {
        return _animationSource;
    }

    public object[] GetData()
    {
        return GetData(0);
    }

    public object[] GetData(int index)
    {
        return _animationData ?? Array.Empty<object>();
    }

    public int GetAnimationCount()
    {
        return Running ? 1 : 0;
    }

    public AnimationDirection Direction => _currentDirection;

    public AnimationDirection GetDirection()
    {
        return GetDirection(0);
    }

    public AnimationDirection GetDirection(int index)
    {
        return _currentDirection;
    }

    public void SetDirection(AnimationDirection direction)
    {
        _currentDirection = direction;
    }

    public void SetData(object[] data)
    {
        _animationData = data ?? Array.Empty<object>();
    }

    public void SetProgress(double progress)
    {
        progress = Math.Clamp(progress, 0, 1);
        _valueProvider.StartTransition(progress, progress, TimeSpan.Zero);
    }

    public bool IsAnimating()
    {
        return Running;
    }

    private static void OnSharedTick(object? sender, TimerElapsedEventArgs e)
    {
        if (Interlocked.Exchange(ref s_tickInProgress, 1) != 0)
            return;

        AnimationManager[] buffer;
        int count;

        lock (s_sync)
        {
            count = s_activeManagers.Count;
            if (count == 0)
            {
                s_sharedTimer?.Stop();
                Interlocked.Exchange(ref s_tickInProgress, 0);
                return;
            }

            buffer = ArrayPool<AnimationManager>.Shared.Rent(count);
            s_activeManagers.CopyTo(buffer, 0);
        }

        try
        {
            for (var i = 0; i < count; i++)
                buffer[i].TickCore();
        }
        finally
        {
            Array.Clear(buffer, 0, count);
            ArrayPool<AnimationManager>.Shared.Return(buffer);
            Interlocked.Exchange(ref s_tickInProgress, 0);
        }
    }

    private void TickCore()
    {
        if (_disposed || !Running)
        {
            UnregisterFromSharedTimer();
            return;
        }

        if (_valueProvider.Completed)
        {
            Running = false;
            UnregisterFromSharedTimer();

            OnAnimationFinished?.Invoke(this);
            return;
        }

        OnAnimationProgress?.Invoke(this);
    }

    private void RegisterWithSharedTimer()
    {
        lock (s_sync)
        {
            EnsureSharedTimer();
            if (!_registered)
            {
                s_activeManagers.Add(this);
                _registered = true;
            }

            if (s_sharedTimer != null && !s_sharedTimer.Enabled)
                s_sharedTimer.Start();
        }
    }

    private void UnregisterFromSharedTimer()
    {
        lock (s_sync)
        {
            if (_registered)
            {
                s_activeManagers.Remove(this);
                _registered = false;
            }

            if (s_activeManagers.Count == 0 && s_sharedTimer != null)
                s_sharedTimer.Stop();
        }
    }

    private void UpdateEasingMethod()
    {
        _valueProvider.EasingMethod = AnimationType switch
        {
            AnimationType.Linear => EasingMethods.Linear,
            AnimationType.EaseIn => EasingMethods.QuadraticEaseIn,
            AnimationType.EaseOut => EasingMethods.QuadraticEaseOut,
            AnimationType.EaseInOut => EasingMethods.QuadraticEaseInOut,
            AnimationType.CubicEaseIn => EasingMethods.CubicEaseIn,
            AnimationType.CubicEaseOut => EasingMethods.CubicEaseOut,
            AnimationType.CubicEaseInOut => EasingMethods.CubicEaseInOut,
            AnimationType.QuarticEaseIn => EasingMethods.QuarticEaseIn,
            AnimationType.QuarticEaseOut => EasingMethods.QuarticEaseOut,
            AnimationType.QuarticEaseInOut => EasingMethods.QuarticEaseInOut,
            _ => EasingMethods.DefaultEase
        };
    }

    public void Stop()
    {
        if (!Running) return;

        Running = false;
        UnregisterFromSharedTimer();
    }
}
