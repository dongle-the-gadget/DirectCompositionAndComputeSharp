using System;
using TerraFX.Interop.Windows;
using static TerraFX.Interop.Windows.Windows;

namespace CompositionStuff;

unsafe readonly struct CustomSemaphore : IDisposable
{
    readonly HANDLE semaphoreHandle;

    public CustomSemaphore(int initialCount, int maximumCount)
    {
        semaphoreHandle = CreateSemaphoreW(null, initialCount, maximumCount, null);
    }

    public bool Wait(uint milliseconds)
    {
        return WaitForSingleObject(semaphoreHandle, milliseconds) == WAIT.WAIT_OBJECT_0;
    }

    public void Release()
    {
        ReleaseSemaphore(semaphoreHandle, 1, null);
    }

    public void Dispose()
    {
        CloseHandle(semaphoreHandle);
    }
}