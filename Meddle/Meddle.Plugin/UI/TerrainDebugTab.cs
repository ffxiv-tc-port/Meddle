using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using Meddle.Plugin.Models;
using Meddle.Plugin.Models.Structs;
using Meddle.Plugin.Services;
using Meddle.Plugin.UI.Windows;
using Meddle.Plugin.Utils;
using Microsoft.Extensions.Logging;

namespace Meddle.Plugin.UI;

public class TerrainDebugTab : ITab
{
    private readonly SigUtil sigUtil;
    private readonly MdlMaterialWindowManager mdlMaterialWindowManager;
    private readonly ILogger<TerrainDebugTab> log;
    private bool loggedExpand;

    public TerrainDebugTab(SigUtil sigUtil,
                           MdlMaterialWindowManager mdlMaterialWindowManager,
                           ILogger<TerrainDebugTab> log)
    {
        this.sigUtil = sigUtil;
        this.mdlMaterialWindowManager = mdlMaterialWindowManager;
        this.log = log;
    }


    public string Name => "地形偵錯";
    public int Order => 3;
    public MenuType MenuType => MenuType.Debug;

    public unsafe void Draw()
    {
        // 台服加固：這個分頁讀的 Terrain / ModelResourceHandle 偏移抄自對全球版寫的參考實作，
        // 沒有對台服客戶端逐欄驗過。偏移錯掉的失敗形式是靜默讀到別的記憶體，接著崩潰。
        // ⇒ 預設收起，使用者要自己展開才會走這段。
        var expanded = ImGui.CollapsingHeader("地形結構（進階偵錯）");
        using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1, 0.4f, 0.4f, 1)))
        {
            ImGui.TextWrapped(Constants.TcUnverifiedOffsetWarning);
        }

        if (!expanded) return;

        if (!loggedExpand)
        {
            loggedExpand = true;
            log.LogInformation("已展開「地形偵錯」分頁：此處的結構偏移未對台服驗證，若在此之後崩潰請一併回報。");
        }

        var world = sigUtil.GetLayoutWorld();
        if (world == null || world->ActiveLayout == null) return;
        foreach (var terrain in world->ActiveLayout->Terrains)
        {
            UiUtil.Text($"Terrain: {terrain.Item1} - {(nint)terrain.Item2.Value:X8}", $"{(nint)terrain.Item2.Value:X8}");
            var terrainPtr = terrain.Item2.Value;
            if (terrainPtr == null) continue;
            UiUtil.Text($"GfxTerrain: {(nint)terrainPtr->GfxTerrain:X8}", $"{(nint)terrainPtr->GfxTerrain:X8}");
            if (terrainPtr->GfxTerrain == null) continue;

            using var tree = ImRaii.TreeNode($"Terrain Resource Handle: {(nint)terrainPtr->GfxTerrain->ResourceHandle:X8}");
            if (tree)
            {
                var terrainStruct = (Terrain*)terrainPtr->GfxTerrain;

                // 台服加固：陣列指標判空 ＋ 數量上限，與材質視窗共用 Terrain.SanityMaxHandles。
                // ⚠️ 這只擋得掉「數量錯」與「陣列指標為 null」，擋不掉「基底指標本身是垃圾」。
                if (terrainStruct->ModelResourceHandles == null)
                {
                    ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), "ModelResourceHandles 為 null，略過。");
                    continue;
                }

                if (terrainStruct->ModelResourceHandleCount > Terrain.SanityMaxHandles)
                {
                    ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1),
                                      $"ModelResourceHandleCount = {terrainStruct->ModelResourceHandleCount}，" +
                                      $"超過上限 {Terrain.SanityMaxHandles}，結構偏移可能不適用於此客戶端版本，略過。");
                    continue;
                }

                var terrainModels = terrainStruct->SafeModelResourceHandlesSpan;
                for (var i = 0; i < terrainModels.Length; i++)
                {
                    var modelHandle = terrainModels[i].Value;
                    if (modelHandle == null)
                    {
                        UiUtil.Text($"Model Resource Handle {i}: null", $"ModelResourceHandle_{i}_null");
                        continue;
                    }
                    
                    var fileName = modelHandle->FileName.ParseString();
                    using var modelTree = ImRaii.TreeNode($"Model Resource Handle {i}: {(nint)terrainModels[i].Value:X8} - {fileName}");
                    if (modelTree)
                    {
                        UiUtil.Text($"File Name: {fileName}", fileName);
                        if (ImGui.Button($"Preview material"))
                        {
                            mdlMaterialWindowManager.AddTerrainMaterialWindow(modelHandle);
                        }

                        // ModelResourceHandleData 會在字串表長度離譜時擲 InvalidDataException
                        // （受管理例外，攔得到；AVE 攔不到，那條路只靠「留在偵錯視窗＋預設收起」）。
                        ModelResourceHandleData? modelData = null;
                        try
                        {
                            modelData = new ModelResourceHandleData(modelHandle->ModelData);
                        }
                        catch (Exception ex)
                        {
                            ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), $"讀取模型標頭失敗：{ex.Message}");
                            log.LogInformation(ex, "地形偵錯：讀取 ModelResourceHandleData 失敗（{FileName}）", fileName);
                        }

                        for (int j = 0; modelData.HasValue && j < modelData.Value.ModelHeader.MaterialCount; j++)
                        {
                            var materialHandle = modelHandle->MaterialResourceHandles[j];
                            if (materialHandle == null) continue;

                            var materialFileName = materialHandle->FileName.ParseString();
                            UiUtil.Text($"Material {j}: {materialFileName}", materialFileName);
                        }
                    }
                }
            }
        }
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}
