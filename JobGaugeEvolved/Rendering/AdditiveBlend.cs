using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Dalamud.Bindings.ImGui;

namespace JobGaugeEvolved.Rendering;

/// <summary>
/// Additive blending for ImGui draws: background + alpha × colour, like GIMP's Addition mode at that opacity.
/// </summary>
/// <remarks>
/// Dalamud's ImGui renderer blends with SrcAlpha / InvSrcAlpha and runs draw-list callbacks while rendering. A callback
/// switches the Direct3D 11 blend state to SrcAlpha / One for the draws that follow; ImGui's "reset render state"
/// callback (-8) then makes Dalamud restore its own state. The blend state is created once through the device's COM
/// vtable (indices from d3d11.h). If anything fails, <see cref="Available"/> is false and callers draw normally.
/// </remarks>
internal static unsafe class AdditiveBlend
{
    // ID3D11Device / ID3D11DeviceContext / IUnknown vtable slots.
    private const int DeviceCreateBlendState = 20;
    private const int DeviceGetImmediateContext = 40;
    private const int ContextOMSetBlendState = 35;
    private const int UnknownRelease = 2;

    private static nint context;
    private static nint blendState;
    private static bool tried;

    /// <summary>Whether additive drawing works (set up on first use).</summary>
    public static bool Available
    {
        get
        {
            if (!tried) {
                tried = true;
                Create();
            }

            return blendState != 0;
        }
    }

    /// <summary>Draws added to <paramref name="drawList"/> after this are blended additively, until <see cref="End"/>.</summary>
    public static void Begin(ImDrawListPtr drawList)
    {
        // The bindings type the callback as a plain function pointer; the renderer calls it as native code.
        delegate* unmanaged<ImDrawList*, ImDrawCmd*, void> callback = &SetAdditive;
        ImGuiNative.AddCallback(drawList.Handle, (delegate*<ImDrawList*, ImDrawCmd*, void>)(void*)callback, null);
    }

    /// <summary>Back to Dalamud's normal blending.</summary>
    public static void End(ImDrawListPtr drawList)
    {
        ImGuiNative.AddCallback(drawList.Handle, (delegate*<ImDrawList*, ImDrawCmd*, void>)(void*)(nint)ImDrawCallbackEnum.ResetRenderState, null);
    }

    public static void Dispose()
    {
        Release(ref blendState);
        Release(ref context);
        tried = false;
    }

    [UnmanagedCallersOnly]
    private static void SetAdditive(ImDrawList* list, ImDrawCmd* cmd)
    {
        if (context == 0 || blendState == 0) {
            return;
        }

        var vtable = *(nint**)context;
        ((delegate* unmanaged<nint, nint, float*, uint, void>)vtable[ContextOMSetBlendState])(context, blendState, null, 0xFFFFFFFF);
    }

    private static void Create()
    {
        try {
            var device = Service.PluginInterface.UiBuilder.DeviceHandle;
            if (device == 0) {
                Service.Log.Warning("No Direct3D device: highlights are drawn without additive blending");
                return;
            }

            var desc = default(BlendDesc);
            for (var i = 0; i < 8; i++) {
                desc.RenderTarget[i] = new RenderTargetBlendDesc
                {
                    BlendEnable = 1,
                    SrcBlend = BlendSrcAlpha,
                    DestBlend = BlendOne,
                    BlendOp = OpAdd,
                    SrcBlendAlpha = BlendZero,
                    DestBlendAlpha = BlendOne,
                    BlendOpAlpha = OpAdd,
                    RenderTargetWriteMask = 0x0F,
                };
            }

            var vtable = *(nint**)device;
            nint state = 0;
            var hr = ((delegate* unmanaged<nint, BlendDesc*, nint*, int>)vtable[DeviceCreateBlendState])(device, &desc, &state);
            if (hr < 0 || state == 0) {
                Service.Log.Warning($"Could not create an additive blend state (0x{hr:X8}): highlights are drawn without it");
                return;
            }

            nint ctx = 0;
            ((delegate* unmanaged<nint, nint*, void>)vtable[DeviceGetImmediateContext])(device, &ctx);
            if (ctx == 0) {
                Release(ref state);
                return;
            }

            context = ctx;
            blendState = state;
        }
        catch (Exception e) {
            Service.Log.Error(e, "Could not set up additive blending: highlights are drawn without it");
            Release(ref blendState);
            Release(ref context);
        }
    }

    private static void Release(ref nint com)
    {
        if (com != 0) {
            var vtable = *(nint**)com;
            ((delegate* unmanaged<nint, uint>)vtable[UnknownRelease])(com);
            com = 0;
        }
    }

    // D3D11_BLEND / D3D11_BLEND_OP values.
    private const int BlendZero = 1;
    private const int BlendOne = 2;
    private const int BlendSrcAlpha = 5;
    private const int OpAdd = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct RenderTargetBlendDesc
    {
        public int BlendEnable;
        public int SrcBlend;
        public int DestBlend;
        public int BlendOp;
        public int SrcBlendAlpha;
        public int DestBlendAlpha;
        public int BlendOpAlpha;
        public byte RenderTargetWriteMask;
    }

    [InlineArray(8)]
    private struct RenderTargets
    {
        private RenderTargetBlendDesc first;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BlendDesc
    {
        public int AlphaToCoverageEnable;
        public int IndependentBlendEnable;
        public RenderTargets RenderTarget;
    }
}
