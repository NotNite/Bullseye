using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Direct3D9;
using Windows.Win32.UI.WindowsAndMessaging;
using Bullseye.Util;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Backends;
using Hexa.NET.ImGui.Backends.D3D9;
using Hexa.NET.ImGui.Backends.Win32;
using HexaGen.Runtime;
using Reloaded.Hooks.Definitions.X86;
using Serilog;
using IDirect3DDevice9 = Windows.Win32.Graphics.Direct3D9.IDirect3DDevice9;
using Utils = Bullseye.Util.Utils;

namespace Bullseye.Hooks;

public unsafe partial class ImGuiHooks : IDisposable {
    [Function(CallingConventions.Stdcall)]
    private delegate uint ReleaseDelegate(IDirect3DDevice9* device);

    [Function(CallingConventions.Stdcall)]
    private delegate HRESULT EndSceneDelegate(IDirect3DDevice9* device);

    [Function(CallingConventions.Stdcall)]
    private delegate HRESULT ResetDelegate(IDirect3DDevice9* device, D3DPRESENT_PARAMETERS* @params);

    [Function(CallingConventions.Stdcall)]
    private delegate nint WndProcDelegate(nint hwnd, uint msg, nint param1, nint param2);

    private readonly Interop interop;
    private readonly Config config;

    private readonly ITrackedHook<ReleaseDelegate> releaseHook;
    private readonly ITrackedHook<EndSceneDelegate> endSceneHook;
    private readonly ITrackedHook<ResetDelegate> resetHook;
    private ITrackedHook<WndProcDelegate>? wndProcHook;

    private bool releaseRecursion;
    private bool endSceneRecursion;
    private bool resetRecursion;

    private readonly IDirect3D9* d3d;
    private IDirect3DDevice9* mainDevice;
    private ImGuiContextPtr context;
    private HWND mainHwnd;

    // Make the cimgui imports stick around by using it directly at least once
    [DllImport("cimgui", CallingConvention = CallingConvention.Cdecl)]
    public static extern ImGuiContext* igCreateContext();

    public ImGuiHooks(Interop interop, Config config, IDirect3D9* d3d) {
        // Hexa.NET.ImGui thinks our static link will be in the main module lol
        ImGuiConfig.AotStaticLink = true;
        ImGuiImplConfig.AotStaticLink = true;

        var ctx = new NativeLibraryContext(Bullseye.Module.BaseAddress);

        ImGui.FreeApi();
        ImGuiImpl.FreeApi();
        ImGui.InitApi(ctx);
        ImGuiImpl.InitApi(ctx);

        if (d3d == null) throw new Exception("IDirect3D9 is null");
        this.interop = interop;
        this.config = config;
        this.d3d = d3d;

        var displayMode = new D3DDISPLAYMODE();
        this.d3d->GetAdapterDisplayMode(PInvoke.D3DADAPTER_DEFAULT, ref displayMode);

        var @params = new D3DPRESENT_PARAMETERS() {
            SwapEffect = D3DSWAPEFFECT.D3DSWAPEFFECT_DISCARD,
            Windowed = true
        };

        IDirect3DDevice9* dummyDevice = null;
        this.d3d->CreateDevice(PInvoke.D3DADAPTER_DEFAULT, D3DDEVTYPE.D3DDEVTYPE_NULLREF,
            this.mainHwnd, PInvoke.D3DCREATE_SOFTWARE_VERTEXPROCESSING,
            ref @params, &dummyDevice);
        if (dummyDevice == null) throw new Exception("Failed to create IDirect3DDevice9");
        var vtbl = *(IDirect3DDevice9.Vtbl**) dummyDevice;

        this.releaseHook = this.interop.CreateHook<ReleaseDelegate>((nint) vtbl->Release_3, this.ReleaseDetour);
        this.releaseHook.Enable();

        this.endSceneHook = this.interop.CreateHook<EndSceneDelegate>((nint) vtbl->EndScene_43, this.EndSceneDetour);
        this.endSceneHook.Enable();

        this.resetHook = this.interop.CreateHook<ResetDelegate>((nint) vtbl->Reset_17, this.ResetDetour);
        this.resetHook.Enable();

        dummyDevice->Release();
    }

    public void Dispose() {
        this.wndProcHook?.Dispose();
        this.resetHook.Dispose();
        this.endSceneHook.Dispose();
        this.releaseHook.Dispose();
        GC.SuppressFinalize(this);
    }

    private void Draw() {
        ImGui.ShowDemoWindow();
    }

    private uint ReleaseDetour(IDirect3DDevice9* device) {
        var ret = this.releaseHook.Original(device);

        if (device == this.mainDevice && ret == 0 && !this.context.IsNull && !this.releaseRecursion) {
            this.releaseRecursion = true;

            try {
                ImGuiImplD3D9.Shutdown();
                ImGuiImplWin32.Shutdown();
                ImGui.DestroyContext();

                this.wndProcHook?.Dispose();
                this.wndProcHook = null;
                this.context = new ImGuiContextPtr();
                this.mainDevice = null;
                this.mainHwnd = HWND.Null;
            } finally {
                this.releaseRecursion = false;
            }
        }

        return ret;
    }

    private HRESULT EndSceneDetour(IDirect3DDevice9* device) {
        if (!this.endSceneRecursion) {
            this.endSceneRecursion = true;

            try {
                if (this.context.IsNull) {
                    try {
                        this.InitImGui(device);
                    } catch (Exception e) {
                        Log.Error(e, "Error in InitImGui");
                    }
                }

                if (!this.context.IsNull && this.mainDevice == device && this.config.EnableOverlay) {
                    ImGuiImplD3D9.NewFrame();
                    ImGuiImplWin32.NewFrame();
                    ImGui.NewFrame();

                    try {
                        this.Draw();
                    } catch (Exception e) {
                        Log.Error(e, "Error in Draw");
                    }

                    var io = ImGui.GetIO();
                    io.MouseDrawCursor = ImGui.IsWindowHovered(ImGuiHoveredFlags.AnyWindow);

                    ImGui.EndFrame();
                    ImGui.Render();
                    var drawData = ImGui.GetDrawData();
                    if (!drawData.IsNull) ImGuiImplD3D9.RenderDrawData(drawData);
                }
            } finally {
                this.endSceneRecursion = false;
            }
        }

        return this.endSceneHook.Original(device);
    }

    private void InitImGui(IDirect3DDevice9* device) {
        Log.Debug("Initializing ImGui...");
        if (!this.context.IsNull) throw new Exception("ImGui context already exists");

        IDirect3DSwapChain9* swapchain = null;
        device->GetSwapChain(0, &swapchain);
        if (swapchain == null) throw new Exception("Swapchain is null");

        var creationParameters = new D3DDEVICE_CREATION_PARAMETERS();
        device->GetCreationParameters(ref creationParameters);

        this.mainHwnd = creationParameters.hFocusWindow;

        this.context = igCreateContext();
        if (this.context.IsNull) throw new Exception("ImGui context is null");

        var io = ImGui.GetIO();
        io.IniFilename =
            (byte*) Marshal.StringToHGlobalAnsi(Path.Combine(Bullseye.BullseyeDirectory, "imgui.ini") + "\0");
        io.Fonts.AddFontDefault();
        io.Fonts.Build();

        if (!ImGuiImplWin32.Init((nint) this.mainHwnd.Value)) throw new Exception("Failed to init ImGuiImplWin32");
        if (!ImGuiImplD3D9.Init(Utils.ToHexaDevice(device))) throw new Exception("Failed to init ImGuiImplD3D9");

        if (this.wndProcHook == null) {
            var wndProc = GetWindowLong(this.mainHwnd, (int) WINDOW_LONG_PTR_INDEX.GWL_WNDPROC);
            this.wndProcHook = this.interop.CreateHook<WndProcDelegate>(wndProc, this.WndProcDetour);
            this.wndProcHook.Enable();
        }

        this.mainDevice = device;
    }

    private HRESULT ResetDetour(IDirect3DDevice9* device, D3DPRESENT_PARAMETERS* @params) {
        if (!this.context.IsNull && device == this.mainDevice && !this.resetRecursion) {
            this.resetRecursion = true;

            try {
                ImGuiImplD3D9.InvalidateDeviceObjects();
                var result = this.resetHook.Original(device, @params);
                ImGuiImplD3D9.CreateDeviceObjects();
                return result;
            } finally {
                this.resetRecursion = false;
            }
        }

        return this.resetHook.Original(device, @params);
    }

    private nint WndProcDetour(nint hwnd, uint msg, nint param1, nint param2) {
        if (msg == PInvoke.WM_KEYDOWN && param1 == (nint) this.config.OverlayKey) {
            this.config.EnableOverlay = !this.config.EnableOverlay;
            this.config.Save();
        }

        if (hwnd == (nint) this.mainHwnd.Value && this.config.EnableOverlay) {
            var result = ImGuiImplWin32.WndProcHandler(hwnd, msg, (nuint) param1, param2);
            if (result != nint.Zero) return result;
        }

        return this.wndProcHook!.Original(hwnd, msg, param1, param2);
    }

    // CSWin32 failed me :pensive:
    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongA", SetLastError = true)]
    private static partial nint GetWindowLong(nint hwnd, int index);
}
