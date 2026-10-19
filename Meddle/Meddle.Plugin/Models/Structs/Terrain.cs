using System.Runtime.InteropServices;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Interop;

namespace Meddle.Plugin.Models.Structs;

[StructLayout(LayoutKind.Explicit, Size = 0x1F0)]
public unsafe struct Terrain
{
    [FieldOffset(0x00)] public FFXIVClientStructs.FFXIV.Client.Graphics.Scene.Terrain* Base;
    [FieldOffset(0x90)] public ResourceHandle* TerrainResourceHandle;
    [FieldOffset(0x98)] public ModelResourceHandle** ModelResourceHandles; // Pointer to an array of ModelResourceHandle pointers
    [FieldOffset(0xA0)] public uint ModelResourceHandleCount; // Number of ModelResourceHandles in the array
    
    public Span<Pointer<ModelResourceHandle>> ModelResourceHandlesSpan => new(ModelResourceHandles, (int)ModelResourceHandleCount);

    /// <summary>
    ///     台服加固：陣列長度來自未對台服驗證的結構偏移，數值離譜時直接放棄，不要照著走。
    ///     ⚠️ 這只擋得掉「數量錯」；基底指標本身是垃圾時照樣會 AccessViolationException，
    ///     那種情況 try/catch 攔不到 ⇒ 這段不是防護。
    /// </summary>
    public const int SanityMaxHandles = 4096;

    /// <summary>數量離譜或陣列指標為 null 時回空 span。</summary>
    public Span<Pointer<ModelResourceHandle>> SafeModelResourceHandlesSpan =>
        ModelResourceHandles == null || ModelResourceHandleCount > SanityMaxHandles
            ? Span<Pointer<ModelResourceHandle>>.Empty
            : ModelResourceHandlesSpan;
}
