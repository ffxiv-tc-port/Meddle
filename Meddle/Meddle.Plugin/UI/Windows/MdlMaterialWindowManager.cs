using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics.Render;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Interop;
using Meddle.Plugin.Utils;
using Meddle.Utils.Files;
using Meddle.Utils.Files.SqPack;
using Microsoft.Extensions.Logging;
using CharacterBase = FFXIVClientStructs.FFXIV.Client.Graphics.Scene.CharacterBase;

namespace Meddle.Plugin.UI.Windows;

public class MdlMaterialWindowManager : IDisposable
{
    private readonly WindowSystem windowSystem;
    private readonly SqPack pack;
    private readonly IClientState clientState;
    private readonly IObjectTable objectTable;
    private readonly ILogger<MdlMaterialWindowManager> log;
    private readonly Dictionary<string, MdlMaterialWindow> materialWindows = new();
    private readonly Dictionary<string, ShpkFile> shpkCache = new();
    public ShpkFile GetShpkFile(string path)
    {
        if (!shpkCache.TryGetValue(path, out var shpk))
        {
            var shpkData = pack.GetFileOrReadFromDisk(path);
            if (shpkData != null)
            {
                shpk = new ShpkFile(shpkData);
                shpkCache[path] = shpk;
            }
            else
            {
                throw new Exception($"Failed to load {path}");
            }
        }
        
        return shpk;
    }
    
    public MdlMaterialWindowManager(
        WindowSystem windowSystem, SqPack pack, IClientState clientState, IObjectTable objectTable,
        ILogger<MdlMaterialWindowManager> log)
    {
        this.windowSystem = windowSystem;
        this.pack = pack;
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.log = log;
        // 台服加固：視窗本身已經改成「每幀重新解析、解析不到就自動關」（見 MdlMaterialSource），
        // 換區與登出的整批關窗仍然保留——那是最便宜、最不會漏的第一道閘門。
        this.clientState.TerritoryChanged += OnTerritoryChanged;
        this.clientState.Logout += OnLogout;
    }

    private void OnTerritoryChanged(ushort territory) => CloseAll();

    private void OnLogout(int type, int code) => CloseAll();

    public void CloseAll()
    {
        foreach (var window in materialWindows.Values.ToArray())
        {
            window.IsOpen = false;
            windowSystem.RemoveWindow(window);
        }

        materialWindows.Clear();
    }

    public void Dispose()
    {
        clientState.TerritoryChanged -= OnTerritoryChanged;
        clientState.Logout -= OnLogout;
        CloseAll();
    }
        
    public void RemoveMaterialWindow(MdlMaterialWindow window)
    {
        materialWindows.Remove(window.Id);
        windowSystem.RemoveWindow(window);
    }

    /// <summary>地形偵錯：ModelResourceHandle 來自地形的 handle 陣列，每幀以「還在不在陣列裡」重驗。</summary>
    public unsafe void AddTerrainMaterialWindow(Pointer<ModelResourceHandle> model)
    {
        if (model == null || model.Value == null) return;
        AddMaterialWindow(new TerrainModelSource((nint)model.Value, model.Value->FileName.ParseString()));
    }

    /// <summary>場景 BgPart：以該 BgPart 的 layout instance 位址當識別，每幀重走 layout 重驗。</summary>
    public unsafe void AddBgPartMaterialWindow(nint layoutInstanceAddress, Pointer<ModelResourceHandle> model)
    {
        if (model == null || model.Value == null) return;
        AddMaterialWindow(new BgPartModelSource(layoutInstanceAddress, (nint)model.Value,
                                                model.Value->FileName.ParseString()));
    }

    /// <summary>角色模型：以 (繪製物件位址, models 陣列索引) 當識別，每幀從物件表重查。</summary>
    public unsafe void AddCharacterMaterialWindow(Pointer<CharacterBase> cBase, Pointer<Model> model)
    {
        if (cBase == null || cBase.Value == null) return;
        if (model == null || model.Value == null || model.Value->ModelResourceHandle == null) return;

        var models = cBase.Value->ModelsSpan;
        var modelIndex = -1;
        for (var i = 0; i < models.Length; i++)
        {
            if (models[i].Value == model.Value)
            {
                modelIndex = i;
                break;
            }
        }

        if (modelIndex < 0)
        {
            log.LogInformation("模型不在該角色的 models 陣列中，材質視窗不開啟：{Model}",
                               model.Value->ModelResourceHandle->FileName.ParseString());
            return;
        }

        AddMaterialWindow(new CharacterModelSource(objectTable, (nint)cBase.Value, (nint)model.Value, modelIndex,
                                                   model.Value->ModelResourceHandle->FileName.ParseString()));
    }

    private void AddMaterialWindow(MdlMaterialSource source)
    {
        var window = new MdlMaterialWindow(this, log, source);
        if (!materialWindows.TryAdd(window.Id, window)) return;
        windowSystem.AddWindow(window);
        window.IsOpen = true;
    }
}
