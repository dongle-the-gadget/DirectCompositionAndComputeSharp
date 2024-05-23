using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace CompositionStuff;

unsafe readonly struct CustomStopwatch
{

    private readonly long _startingTicks;
    private readonly long _tickFrequency;

    public CustomStopwatch()
    {
        fixed (long* lpTickFrequency = &_tickFrequency)
        {
            QueryPerformanceFrequency((LARGE_INTEGER*)lpTickFrequency);
        }
        
        fixed (long* lpPerformanceCount = &_startingTicks)
        {
            QueryPerformanceCounter((LARGE_INTEGER*)lpPerformanceCount);
        }
    }

    public double ElapsedSeconds
    {
        get
        {
            long currentTicks;
            QueryPerformanceCounter((LARGE_INTEGER*)&currentTicks);
            return (currentTicks - _startingTicks) / (double)_tickFrequency;
        }
    }
}