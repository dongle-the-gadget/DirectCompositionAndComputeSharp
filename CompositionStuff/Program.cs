using System;
using System.Runtime.InteropServices;
using ComputeSharp.D2D1.Interop;
using TerraFX.Interop.Windows;
using TerraFX.Interop.DirectX;
using static TerraFX.Interop.Windows.Windows;
using static TerraFX.Interop.DirectX.DirectX;

namespace CompositionStuff;

struct Program
{
    static ComPtr<ID3D11Device> direct3dDevice = default;
    static ComPtr<IDXGIDevice> dxgiDevice = default;
    static ComPtr<ID2D1Factory2> d2dFactory2 = default;
    static ComPtr<ID2D1Device> d2dDevice = default;
    static ComPtr<IDCompositionDesktopDevice> dcompDevice = default;
    static ComPtr<IDCompositionVirtualSurface> surface = default;
    static ComPtr<IDCompositionVisual> visual = default;
    static ComPtr<IDCompositionTarget> dcompTarget = default;
    static ComPtr<IDCompositionSurfaceFactory> surfaceFactory = default;
    static HWND myWnd;
    static CustomStopwatch stopwatch;
    static CustomSemaphore semaphore;
    static bool initialized = false;
    
    static unsafe void Main(string[] args)
    {
        myWnd = CreateWindowWrapper(
            "DirectComposition Window",
            "ExampleDirectComposition",
            WS.WS_EX_NOREDIRECTIONBITMAP | WS.WS_EX_OVERLAPPEDWINDOW,
            WS.WS_OVERLAPPEDWINDOW);

        stopwatch = new CustomStopwatch();
        semaphore = new CustomSemaphore(1, 1);
        InitializeDComp();
        SetBuffer(0f, new(1000, 640), false);
        ShowWindow(myWnd, SW.SW_SHOW);

        SetTimer(myWnd, 0, 1000 / 60, null);

        MSG msg;
        while (GetMessage(&msg, HWND.NULL, 0, 0))
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
        DisposeDComp();
        semaphore.Dispose();
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

    static unsafe HWND CreateWindowWrapper(string titleName, string className, int dwExStyle, int dwStyle)
    {
        HINSTANCE hInstance = GetModuleHandleW(null);

        fixed (char* lpClassName = className)
        {
            WNDCLASSW wc = new()
            {
                hInstance = hInstance,
                cbWndExtra = 0,
                lpszClassName = lpClassName,
                lpfnWndProc = &MyWndProc
            };

            RegisterClassW(&wc);
        }

        HWND hwnd;

        fixed (char* lpClassName = className)
        fixed (char* lpTitleName = titleName)
            hwnd = CreateWindowEx((uint)dwExStyle, lpClassName, lpTitleName,
                (uint)dwStyle, CW_USEDEFAULT, CW_USEDEFAULT, 1000, 640,
                HWND.NULL, HMENU.NULL, hInstance, null);

        return hwnd;
    }

    static unsafe void InitializeDComp()
    {
        D3D_FEATURE_LEVEL level = D3D_FEATURE_LEVEL.D3D_FEATURE_LEVEL_11_0;

        HRESULT hr;
        if ((hr = D3D11CreateDevice(
            null,
            D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_HARDWARE,
            HMODULE.NULL,
            (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            &level,
            1,
            D3D11.D3D11_SDK_VERSION,
            direct3dDevice.GetAddressOf(),
            null,
            null)) != S.S_OK)
        {
            // TODO: Provide WARP fallback.
            // TODO: Listen for hardware changes.
            ShowErrorAndFail(D3D11CreateDevice(
            null,
            D3D_DRIVER_TYPE.D3D_DRIVER_TYPE_WARP,
            HMODULE.NULL,
            (uint)D3D11_CREATE_DEVICE_FLAG.D3D11_CREATE_DEVICE_BGRA_SUPPORT,
            &level,
            1,
            D3D11.D3D11_SDK_VERSION,
            direct3dDevice.GetAddressOf(),
            null,
            null));
        }

        if ((hr = direct3dDevice.As(ref dxgiDevice)) != S.S_OK)
            ShowErrorAndFail(hr);

        if ((hr = D2D1CreateFactory(
            D2D1_FACTORY_TYPE.D2D1_FACTORY_TYPE_SINGLE_THREADED,
            __uuidof<ID2D1Factory2>(),
            null,
            (void**)d2dFactory2.GetAddressOf())) != S.S_OK)
            ShowErrorAndFail(hr);

        ShowErrorAndFail(d2dFactory2.Get()->CreateDevice(dxgiDevice.Get(), d2dDevice.GetAddressOf()));

        if ((hr = DCompositionCreateDevice3(
            (IUnknown*)d2dDevice.Get(),
            __uuidof<IDCompositionDesktopDevice>(),
            (void**)dcompDevice.GetAddressOf())) != S.S_OK)
            ShowErrorAndFail(hr);

        D2D1PixelShaderEffect.RegisterForD2D1Factory1<HelloWorld>(d2dFactory2.Get(), out _);
        ShowErrorAndFail(dcompDevice.Get()->CreateTargetForHwnd(myWnd, true, dcompTarget.GetAddressOf()));
        ShowErrorAndFail(dcompDevice.Get()->CreateVisual((IDCompositionVisual2**)visual.GetAddressOf()));
        ShowErrorAndFail(dcompDevice.Get()->CreateSurfaceFactory((IUnknown*)d2dDevice.Get(), surfaceFactory.GetAddressOf()));
        ShowErrorAndFail(surfaceFactory.Get()->CreateVirtualSurface(1000, 640, DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM, DXGI_ALPHA_MODE.DXGI_ALPHA_MODE_IGNORE, surface.GetAddressOf()));
        ShowErrorAndFail(visual.Get()->SetContent((IUnknown*)surface.Get()));

        ShowErrorAndFail(dcompTarget.Get()->SetRoot(visual.Get()));
        ShowErrorAndFail(dcompDevice.Get()->Commit());
        initialized = true;
    }

    static unsafe void SetBuffer(float time, int2 size, bool resize)
    {
        // If another frame is already being concurrently drawn, do nothing
        if (!semaphore.Wait(0))
        {
            return;
        }
        if (size.X == 0 || size.Y == 0)
        {
            semaphore.Release();
            return;
        }

        if (resize)
            CheckIfDeviceFailureOrThrowError(surface.Get()->Resize((uint)size.X, (uint)size.Y));
        using ComPtr<ID2D1DeviceContext> context = default;
        POINT point;
        CheckIfDeviceFailureOrThrowError(surface.Get()->BeginDraw(null, __uuidof<ID2D1DeviceContext>(), (void**)context.GetAddressOf(), &point));
        context.Get()->Clear();
        using ComPtr<ID2D1Effect> effect = default;
        D2D1PixelShaderEffect.CreateFromD2D1DeviceContext<HelloWorld>(context.Get(), (void**)effect.GetAddressOf());
        D2D1PixelShaderEffect.SetConstantBufferForD2D1Effect(effect.Get(), new HelloWorld(time, size));
        using ComPtr<ID2D1Image> image = default;
        effect.Get()->GetOutput(image.GetAddressOf());
        D2D_POINT_2F convertedOffset = new() { x = point.x, y = point.y };
        D2D_RECT_F rect = new() { top = 0, left = 0, right = size.X, bottom = size.Y };
        context.Get()->DrawImage(image.Get(), &convertedOffset, &rect, D2D1_INTERPOLATION_MODE.D2D1_INTERPOLATION_MODE_LINEAR, GetCompositeModeFromPrimitiveBlend(context.Get()->GetPrimitiveBlend()));
        CheckIfDeviceFailureOrThrowError(surface.Get()->EndDraw());
        CheckIfDeviceFailureOrThrowError(dcompDevice.Get()->Commit());

        static void CheckIfDeviceFailureOrThrowError(HRESULT hresult)
        {
            // If the device was removed or reset, recreate the device and all dependent resources.
            if (hresult == DXGI.DXGI_ERROR_DEVICE_REMOVED || hresult == DXGI.DXGI_ERROR_DEVICE_RESET)
            {
                DisposeDComp();
                InitializeDComp();
                return;
            }
            ShowErrorAndFail(hresult);
        }

        semaphore.Release();
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
                throw new ArgumentException("Invalid primitive blend mode.");
        }
    }

    [UnmanagedCallersOnly]
    public static unsafe LRESULT MyWndProc(HWND hWnd, uint Msg, WPARAM wParam, LPARAM lParam)
    {
        switch (Msg)
        {
            case WM.WM_CLOSE:
                PostQuitMessage(0);
                break;

            case WM.WM_SIZE:
                if (initialized)
                    SetBuffer((float)stopwatch.ElapsedSeconds, new((int)(lParam & 0xFFFF), (int)(lParam >> 16)), true);
                break;

            case WM.WM_TIMER:
                RECT rect;
                GetClientRect(myWnd, &rect);
                int2 size = new(rect.right, rect.bottom);
                SetBuffer((float)stopwatch.ElapsedSeconds, size, false);
                break;
        }

        return DefWindowProc(hWnd, Msg, wParam, lParam);
    }

    private static unsafe void ShowErrorAndFail(HRESULT hr)
    {
        if (hr == S.S_OK)
        {
            return;
        }
        // Show message: 'Initialization failed with error code: 0x{hr:X8}', then exit.
        string message = $"Initialization failed with error code: 0x{hr:X8}";
        fixed (char* lpText = message)
        {
            MessageBoxW(HWND.NULL, lpText, null, 0);
        }
        PostQuitMessage(0);
    }
}
