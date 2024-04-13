using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct2D;
using Windows.Win32.Graphics.Direct3D11;
using Windows.Win32.Graphics.DirectComposition;
using Windows.Win32.Graphics.Dxgi;
using Windows.Win32.Graphics.Dxgi.Common;
using Windows.Win32.UI.WindowsAndMessaging;
using static Windows.Win32.PInvoke;
using static CompositionStuff.UuidOfTypeMethods;
using ComputeSharp.D2D1.Interop;
using Windows.Win32.Graphics.Direct2D.Common;

namespace CompositionStuff;

static class Program
{
    static unsafe void Main(string[] args)
    {
        myWnd = CreateWindowInternal(
            "DirectComposition Window",
            "ExampleDirectComposition",
            WINDOW_EX_STYLE.WS_EX_NOREDIRECTIONBITMAP | WINDOW_EX_STYLE.WS_EX_OVERLAPPEDWINDOW,
            WINDOW_STYLE.WS_OVERLAPPEDWINDOW);

        InitializeDComp();
        SetBuffer(0f, new(1000, 640));
        ShowWindow(myWnd, SHOW_WINDOW_CMD.SW_SHOW);

        

        stopwatch = new Stopwatch();
        timer = new Timer(
            callback: static _ => 
            {
                RECT rect;
                unsafe
                {
                    GetClientRect(myWnd, &rect);
                }
                int2 size = new(rect.right, rect.bottom);
                SetBuffer((float)stopwatch.Elapsed.TotalSeconds, size);
            },
            state: null,
            dueTime: TimeSpan.Zero,
            period: TimeSpan.FromSeconds(1 / 60.0));
        stopwatch.Start();

        MSG msg;
        while (GetMessage(&msg, HWND.Null, 0, 0))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }

        // Dispose of all resources
        stopwatch.Stop();
        timer.Dispose();
    }

    static void DisposeDComp()
    {
        initialized = false;
        dcompTarget.Dispose();
        visual.Dispose();
        surface.Dispose();
        surfaceFactory.Dispose();
        dcompDevice.Dispose();
        d2dDevice.Dispose();
        d2dFactory2.Dispose();
        dxgiDevice.Dispose();
        direct3dDevice.Dispose();
    }

    static unsafe HWND CreateWindowInternal(string titleName, string className, WINDOW_EX_STYLE dwExStyle, WINDOW_STYLE dwStyle)
    {
        HINSTANCE hInstance = GetModuleHandle(new PCWSTR());

        fixed (char* lpClassName = className)
        {
            WNDCLASSW wc = new()
            {
                hInstance = hInstance,
                cbWndExtra = 0,
                lpszClassName = lpClassName,
                lpfnWndProc = &MyWndProc
            };

            if (RegisterClass(&wc) == 0)
                throw new Win32Exception($"Failed to register window class. Error code {Marshal.GetLastPInvokeError()}");
        }

        HWND hwnd = CreateWindowEx(dwExStyle, className, titleName,
            dwStyle, CW_USEDEFAULT, CW_USEDEFAULT, 1000, 640,
            HWND.Null, HMENU.Null, hInstance, null);

        if (hwnd == HWND.Null)
            throw new Win32Exception($"Failed to create window. Error code {Marshal.GetLastPInvokeError()}");

        return hwnd;
    }

    static ComPtr<ID3D11Device> direct3dDevice = default;
    static ComPtr<IDXGIDevice2> dxgiDevice = default;
    static ComPtr<ID2D1Factory2> d2dFactory2 = default;
    static ComPtr<ID2D1Device> d2dDevice = default;
    static ComPtr<IDCompositionDesktopDevice> dcompDevice = default;
    static ComPtr<IDCompositionVirtualSurface> surface = default;
    static ComPtr<IDCompositionVisual> visual = default;
    static ComPtr<IDCompositionTarget> dcompTarget = default;
    static ComPtr<IDCompositionSurfaceFactory> surfaceFactory = default;
    static HWND myWnd;
    static Stopwatch stopwatch;
    static Timer timer;
    static volatile int isDrawing;
    static bool initialized = false;

    static unsafe void InitializeDComp()
    {
        HRESULT hr;
        if ((hr = D3D11CreateDevice(
            null,
            Windows.Win32.Graphics.Direct3D.D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
            HMODULE.Null,
            D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            null,
            0,
            D3D11_SDK_VERSION,
            direct3dDevice.GetAddressOf(),
            null,
            null)) != HRESULT.S_OK)
        {
            // TODO: Provide WARP fallback.
            // TODO: Listen for hardware changes.
            Marshal.ThrowExceptionForHR(D3D11CreateDevice(
            null,
            Windows.Win32.Graphics.Direct3D.D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_WARP,
            HMODULE.Null,
            D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            null,
            0,
            D3D11_SDK_VERSION,
            direct3dDevice.GetAddressOf(),
            null,
            null));
        }

        if ((hr = direct3dDevice.CopyTo(ref dxgiDevice)) != HRESULT.S_OK)
            Marshal.ThrowExceptionForHR(hr);

        if ((hr = D2D1CreateFactory(
            D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED,
            __uuidof<ID2D1Factory2>(),
            null,
            (void**)d2dFactory2.GetAddressOf())) != HRESULT.S_OK)
            Marshal.ThrowExceptionForHR(hr);

        d2dFactory2.Get()->CreateDevice((IDXGIDevice*)dxgiDevice.Get(), d2dDevice.GetAddressOf());

        if ((hr = DCompositionCreateDevice3(
            (Windows.Win32.System.Com.IUnknown*)d2dDevice.Get(),
            __uuidof<IDCompositionDesktopDevice>(),
            (void**)dcompDevice.GetAddressOf())) != HRESULT.S_OK)
            Marshal.ThrowExceptionForHR(hr);

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<HelloWorld>(d2dFactory2.Get(), out _);
        dcompDevice.Get()->CreateTargetForHwnd(myWnd, true, dcompTarget.GetAddressOf());
        dcompDevice.Get()->CreateVisual((IDCompositionVisual2**)visual.GetAddressOf());
        dcompDevice.Get()->CreateSurfaceFactory((Windows.Win32.System.Com.IUnknown*)d2dDevice.Get(), surfaceFactory.GetAddressOf());
        surfaceFactory.Get()->CreateVirtualSurface(1000, 640, DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM, DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_IGNORE, surface.GetAddressOf());
        visual.Get()->SetContent((Windows.Win32.System.Com.IUnknown*)surface.Get());

        dcompTarget.Get()->SetRoot(visual.Get());
        dcompDevice.Get()->Commit();
        initialized = true;
    }

    static unsafe void SetBuffer(float time, int2 size)
    {
        // If another frame is already being concurrently drawn, do nothing
        if (Interlocked.CompareExchange(ref isDrawing, 1, 0) == 1)
        {
            return;
        }
        try
        {
            if (size.X == 0 || size.Y == 0)
                return;

            surface.Get()->Resize((uint)size.X, (uint)size.Y);
            using ComPtr<ID2D1DeviceContext> context = default;
            System.Drawing.Point point;
            surface.Get()->BeginDraw(null, __uuidof<ID2D1DeviceContext>(), (void**)context.GetAddressOf(), &point);
            context.Get()->Clear();
            using ComPtr<ID2D1Effect> effect = default;
            D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<HelloWorld>(context.Get(), (void**)effect.GetAddressOf());
            D2D1PixelShaderEffect.SetConstantBufferForD2D1Effect(effect.Get(), new HelloWorld(time, size));
            using ComPtr<ID2D1Image> image = default;
            effect.Get()->GetOutput(image.GetAddressOf());
            D2D_POINT_2F convertedOffset = new() { x = point.X, y = point.Y };
            D2D_RECT_F rect = new() { top = 0, left = 0, right = size.X, bottom = size.Y };
            context.Get()->DrawImage(image.Get(), &convertedOffset, &rect, D2D1_INTERPOLATION_MODE.D2D1_INTERPOLATION_MODE_LINEAR, GetCompositeModeFromPrimitiveBlend(context.Get()->GetPrimitiveBlend()));
            surface.Get()->EndDraw();
            dcompDevice.Get()->Commit();
        }
        catch (Exception ex)
        {
            if (ex.HResult == -2005270523 || ex.HResult == -2005270521)
            {
                DisposeDComp();
                InitializeDComp();
            }
            else
                throw;
        }
        finally
        {
            isDrawing = 0;
        }
    }

    static D2D1_COMPOSITE_MODE GetCompositeModeFromPrimitiveBlend(D2D1_PRIMITIVE_BLEND blend)
    {
        switch (blend)
        {
            case D2D1_PRIMITIVE_BLEND.D2D1_PRIMITIVE_BLEND_SOURCE_OVER:
                return D2D1_COMPOSITE_MODE.D2D1_COMPOSITE_MODE_SOURCE_OVER;

            case D2D1_PRIMITIVE_BLEND.D2D1_PRIMITIVE_BLEND_COPY:
                return D2D1_COMPOSITE_MODE.D2D1_COMPOSITE_MODE_SOURCE_COPY;

            case D2D1_PRIMITIVE_BLEND.D2D1_PRIMITIVE_BLEND_ADD:
                return D2D1_COMPOSITE_MODE.D2D1_COMPOSITE_MODE_PLUS;

            default:
                throw new Exception();
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    public static unsafe LRESULT MyWndProc(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam)
    {
        switch (Msg)
        {
            case WM_CLOSE:
                PostQuitMessage(0);
                break;

            case WM_SIZE:
                if (initialized && stopwatch != null)
                    SetBuffer((float)stopwatch.Elapsed.TotalSeconds, new((int)(lParam & 0xFFFF), (int)(lParam >> 16)));
                break;
            
            case WM_GETMINMAXINFO:
                MINMAXINFO* mmi = (MINMAXINFO*)lParam.Value;
                mmi->ptMinTrackSize.X = Math.Max(1, mmi->ptMinTrackSize.X);
                mmi->ptMinTrackSize.Y = Math.Max(1, mmi->ptMinTrackSize.Y);
                return new LRESULT(0);
        }

        return DefWindowProc(hWnd, Msg, wParam, lParam);
    }
}
