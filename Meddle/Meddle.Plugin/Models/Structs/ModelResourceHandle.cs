using System.Runtime.InteropServices;
using FFXIVClientStructs.Interop;
using Meddle.Plugin.Utils;
using Meddle.Utils.Files.Structs.Model;

namespace Meddle.Plugin.Models.Structs;

[StructLayout(LayoutKind.Explicit, Size = 0x2A0)]
public unsafe struct MeddleModelResourceHandle
{
    [FieldOffset(0x0)] public FFXIVClientStructs.FFXIV.Client.System.Resource.Handle.ModelResourceHandle* Base;
    [FieldOffset(0xC8)] public byte* ModelData; // StringTable, ModelHeader ...
    public ModelResourceHandleData GetData() => new(Base->ModelData);
} 


public readonly struct ModelResourceHandleData
{
    /// <summary>
    ///     台服加固：字串表長度來自未對台服驗證的結構偏移。實務上模型字串表遠小於 64 KiB，
    ///     超過就當這段結構不是我們以為的東西，整段放棄，不要照著那個長度去讀。
    ///     ⚠️ 這只擋得掉「長度錯」；modelDataPtr 本身是垃圾指標時照樣會崩，
    ///     而 AccessViolationException 在 .NET Core 是 corrupted-state exception，攔不到。
    /// </summary>
    public const uint MaxStringTableSize = 0x10000;

    public readonly uint StringCount;
    public readonly uint StringTableSize;
    public readonly byte[] StringTableData;
    public readonly ModelHeader ModelHeader;

    public ModelResourceHandleData(Pointer<byte> modelDataPtr)
    {
        var offset = 0;
        StringCount = modelDataPtr.Read<uint>(ref offset);
        StringTableSize = modelDataPtr.Read<uint>(ref offset);
        if (StringTableSize > MaxStringTableSize)
        {
            throw new System.IO.InvalidDataException(
                $"模型字串表長度 {StringTableSize} 超過上限 {MaxStringTableSize}，結構偏移可能不適用於此客戶端版本。");
        }

        StringTableData = modelDataPtr.ReadSpan<byte>((int)StringTableSize, ref offset).ToArray();
        ModelHeader = modelDataPtr.Read<ModelHeader>(ref offset);
    }
} 
