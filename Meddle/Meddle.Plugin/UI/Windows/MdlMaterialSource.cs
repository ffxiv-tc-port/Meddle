using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Layer;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using CharacterBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;
using DrawObject = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.DrawObject;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using MeddleBgObject = Meddle.Plugin.Models.Structs.BgObject;
using MeddleTerrain = Meddle.Plugin.Models.Structs.Terrain;
using ObjectType = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.ObjectType;

namespace Meddle.Plugin.UI.Windows;

public enum MdlMaterialSourceKind
{
    Model,
    ModelResourceHandle
}

/// <summary>
///     台服加固：材質編輯視窗不把 <c>Model*</c> / <c>ModelResourceHandle*</c> 存成欄位跨幀解參。
///     這些原生物件在換裝、離開視野、區域串流卸載時就會被遊戲釋放，
///     讀到已釋放的記憶體是 AccessViolationException——在 .NET Core 屬 corrupted-state exception，
///     <c>try/catch</c> 完全攔不到，直接把遊戲帶走。
///     ⇒ 只保存「識別資訊」，每一幀重新從遊戲自己的容器裡把指標找回來；找不到就關窗。
///     ⚠️ 這裡保存的 <see cref="nint" /> 只拿來做**指標值比對**，任何情況下都不解參。
/// </summary>
public abstract unsafe class MdlMaterialSource
{
    protected MdlMaterialSource(string key, string displayName)
    {
        Key = key;
        DisplayName = displayName;
    }

    /// <summary>視窗的唯一識別（同時當 ImGui 的 ### id）。</summary>
    public string Key { get; }

    /// <summary>建立當下抄下來的檔名，之後只拿來顯示，不再重新解參取得。</summary>
    public string DisplayName { get; }

    public abstract MdlMaterialSourceKind Kind { get; }

    /// <summary>
    ///     這一幀重新解析出來的指標。回傳 0 代表來源已經消失，呼叫端必須關窗，
    ///     絕對不可以退回去用上一幀的值。
    /// </summary>
    public abstract nint Resolve();
}

/// <summary>
///     角色底下的 <c>Model*</c>。識別＝(繪製物件位址, models 陣列索引)。
///     每幀從物件表把「還活著的」繪製物件重新找出來，再確認該索引上仍是同一個 model 指標。
///     ⚠️ 之所以不是只用 GameObjectId：材質視窗也可能開在武器／坐騎／時裝的繪製物件上，
///     那些不是角色自己的 DrawObject。用「這一幀從活物件身上讀到的指標」去比對才涵蓋得完整。
/// </summary>
public sealed unsafe class CharacterModelSource : MdlMaterialSource
{
    private readonly nint drawObjectAddress;
    private readonly nint modelAddress;
    private readonly int modelIndex;
    private readonly IObjectTable objectTable;

    public CharacterModelSource(
        IObjectTable objectTable, nint drawObjectAddress, nint modelAddress, int modelIndex, string displayName)
        : base($"{modelAddress:X8}", displayName)
    {
        this.objectTable = objectTable;
        this.drawObjectAddress = drawObjectAddress;
        this.modelAddress = modelAddress;
        this.modelIndex = modelIndex;
    }

    public override MdlMaterialSourceKind Kind => MdlMaterialSourceKind.Model;

    public override nint Resolve()
    {
        if (modelAddress == 0 || drawObjectAddress == 0 || modelIndex < 0) return 0;

        var cBase = FindLiveCharacterBase();
        if (cBase == null) return 0;

        var models = cBase->ModelsSpan;
        if (modelIndex >= models.Length) return 0;

        var model = models[modelIndex].Value;
        if (model == null) return 0;
        if ((nint)model != modelAddress) return 0;

        return modelAddress;
    }

    private CharacterBase* FindLiveCharacterBase()
    {
        foreach (var obj in objectTable)
        {
            var native = (GameObject*)obj.Address;
            if (native == null) continue;

            var hit = MatchDrawObject(native->DrawObject);
            if (hit != null) return hit;

            if (obj is not ICharacter) continue;
            var character = (Character*)native;

            for (var i = 0; i < character->DrawData.WeaponData.Length; i++)
            {
                hit = MatchDrawObject(character->DrawData.WeaponData[i].DrawObject);
                if (hit != null) return hit;
            }

            if (character->Mount.MountObject != null)
            {
                hit = MatchDrawObject(character->Mount.MountObject->GameObject.DrawObject);
                if (hit != null) return hit;
            }

            if (character->OrnamentData.OrnamentObject != null)
            {
                hit = MatchDrawObject(character->OrnamentData.OrnamentObject->Character.GameObject.DrawObject);
                if (hit != null) return hit;
            }
        }

        return null;
    }

    private CharacterBase* MatchDrawObject(DrawObject* drawObject)
    {
        if (drawObject == null) return null;
        if ((nint)drawObject != drawObjectAddress) return null;
        if (drawObject->Object.GetObjectType() != ObjectType.CharacterBase) return null;
        return (CharacterBase*)drawObject;
    }
}

/// <summary>
///     地形的 <c>ModelResourceHandle*</c>。沒有「物件 id」可以掛，
///     所以用退而求其次的做法：每幀確認這個 handle 仍在某個地形的 handle 陣列裡才解參。
/// </summary>
public sealed unsafe class TerrainModelSource : MdlMaterialSource
{
    private readonly nint handleAddress;

    public TerrainModelSource(nint handleAddress, string displayName)
        : base($"{handleAddress:X8}", displayName)
    {
        this.handleAddress = handleAddress;
    }

    public override MdlMaterialSourceKind Kind => MdlMaterialSourceKind.ModelResourceHandle;

    public override nint Resolve()
    {
        if (handleAddress == 0) return 0;

        var world = LayoutWorld.Instance();
        if (world == null || world->ActiveLayout == null) return 0;

        foreach (var (_, terrainPtr) in world->ActiveLayout->Terrains)
        {
            if (terrainPtr == null || terrainPtr.Value == null) continue;
            var gfxTerrain = terrainPtr.Value->GfxTerrain;
            if (gfxTerrain == null) continue;

            var terrain = (MeddleTerrain*)gfxTerrain;
            // 上限與判空都在 MeddleTerrain.SafeModelResourceHandlesSpan 裡（與地形偵錯分頁共用同一個常數）。
            var handles = terrain->SafeModelResourceHandlesSpan;
            for (var i = 0; i < handles.Length; i++)
            {
                var handle = handles[i].Value;
                if (handle != null && (nint)handle == handleAddress) return handleAddress;
            }
        }

        return 0;
    }
}

/// <summary>
///     場景 BgPart 的 <c>ModelResourceHandle*</c>。識別＝該 BgPart 的 layout instance 位址。
///     每幀重新走一次 layout，確認那個 instance 還在、而且它現在掛的仍是同一個 handle。
/// </summary>
public sealed unsafe class BgPartModelSource : MdlMaterialSource
{
    private readonly nint handleAddress;
    private readonly nint layoutInstanceAddress;

    public BgPartModelSource(nint layoutInstanceAddress, nint handleAddress, string displayName)
        : base($"{handleAddress:X8}", displayName)
    {
        this.layoutInstanceAddress = layoutInstanceAddress;
        this.handleAddress = handleAddress;
    }

    public override MdlMaterialSourceKind Kind => MdlMaterialSourceKind.ModelResourceHandle;

    public override nint Resolve()
    {
        if (handleAddress == 0 || layoutInstanceAddress == 0) return 0;

        var world = LayoutWorld.Instance();
        if (world == null) return 0;

        foreach (var (_, layoutPtr) in world->LoadedLayouts)
        {
            if (layoutPtr == null || layoutPtr.Value == null) continue;
            if (StillAttached(layoutPtr.Value)) return handleAddress;
        }

        if (StillAttached(world->GlobalLayout)) return handleAddress;

        return 0;
    }

    private bool StillAttached(LayoutManager* layout)
    {
        if (layout == null) return false;

        foreach (var (_, layerPtr) in layout->Layers)
        {
            if (layerPtr == null || layerPtr.Value == null) continue;
            foreach (var (_, instancePtr) in layerPtr.Value->Instances)
            {
                if (instancePtr == null || instancePtr.Value == null) continue;
                var instance = instancePtr.Value;
                if ((nint)instance != layoutInstanceAddress) continue;
                if (instance->Id.Type != InstanceType.BgPart) return false;

                var graphics = (MeddleBgObject*)((BgPartsLayoutInstance*)instance)->GraphicsObject;
                if (graphics == null || graphics->ModelResourceHandle == null) return false;
                return (nint)graphics->ModelResourceHandle == handleAddress;
            }
        }

        return false;
    }
}
