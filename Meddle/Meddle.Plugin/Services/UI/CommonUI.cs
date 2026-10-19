using System.Numerics;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using Dalamud.Bindings.ImGui;
using Meddle.Plugin.Utils;
using ObjectKind = Dalamud.Game.ClientState.Objects.Enums.ObjectKind;

namespace Meddle.Plugin.Services.UI;

public class CommonUi : IDisposable, IService
{
    private readonly IObjectTable objectTable;
    private readonly IClientState clientState;
    private readonly Configuration config;
    private bool selectTarget;

    public CommonUi(IClientState clientState, IObjectTable objectTable, Configuration config)
    {
        this.clientState = clientState;
        this.objectTable = objectTable;
        this.config = config;
    }
    
    /// <summary>
    ///     台服加固：物件表回傳的 IGameObject 是「每個欄位預先配置、每次存取才重新指向」的共用實例
    ///     （Dalamud ObjectTable.CachedEntry.Update()），而 IsValid() 只檢查有沒有登入。
    ///     所以跨幀保存 ICharacter 有兩種失敗形式，兩種都不會拋例外：
    ///     ①該欄位換了別的角色 → 靜默指到錯的人；②該欄位空了 → Update() 提早回 null、
    ///     包裝物件留著上一次的位址 → 解參用就是 use-after-free（AccessViolationException，try/catch 攔不到）。
    ///     ⇒ 正解是只保存 GameObjectId，每一幀重查一次。
    /// </summary>
    public ICharacter? ResolveCharacter(ulong gameObjectId)
    {
        if (gameObjectId == 0) return null;
        return objectTable.SearchById(gameObjectId) as ICharacter;
    }

    /// <summary>
    ///     判斷某個原生 DrawObject 指標這一幀是否還掛在物件表上的某個角色底下。
    ///     用來取代「拿上一幀存下來的 DrawObject* 直接解參用」。
    /// </summary>
    public unsafe bool IsLiveDrawObject(void* drawObject)
    {
        if (drawObject == null) return false;
        foreach (var obj in objectTable)
        {
            var native = (GameObject*)obj.Address;
            if (native == null) continue;
            if (native->DrawObject == drawObject) return true;
        }

        return false;
    }

    public unsafe ICharacter[] GetCharacters(ObjectUtil.ValidationFlags flags = ObjectUtil.ValidationFlags.None)
    {
        if (clientState.LocalPlayer != null)
        {
            return objectTable.OfType<ICharacter>()
                              .Where(obj => obj.IsValid() && obj.IsValidCharacterBase(flags))
                              .OrderBy(c => clientState.GetDistanceToLocalPlayer(c).LengthSquared())
                              .ToArray();
        }
        else
        {
            // login/char creator produces "invalid" characters but are still usable I guess
            return objectTable.OfType<ICharacter>()
                              .Where(obj => obj.IsValidHuman(flags))
                              .OrderBy(c => clientState.GetDistanceToLocalPlayer(c).LengthSquared())
                              .ToArray();
        }
    }

    /// <summary>
    ///     台服加固：呼叫端保存的是 GameObjectId 清單，不是 ICharacter（理由見 ResolveCharacter）。
    ///     回傳值是「這一幀」重新解析出來的角色，用完就丟。
    /// </summary>
    public unsafe List<ICharacter> DrawMultiCharacterSelect(List<ulong> selectedCharacterIds, ObjectUtil.ValidationFlags flags = ObjectUtil.ValidationFlags.None)
    {
        ICharacter[] objects = GetCharacters(flags);

        ImGui.Text("選擇角色");
        var selected = new List<ICharacter>();
        var notSelected = new List<ICharacter>();
        foreach (var character in objects)
        {
            if (selectedCharacterIds.Contains(character.GameObjectId))
            {
                selected.Add(character);
            }
            else
            {
                notSelected.Add(character);
            }
        }

        foreach (var character in selected.OrderBy(x => x.GameObjectId))
        {
            var displayText = GetCharacterDisplayText(character, true, false);
            // remove button
            using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button(FontAwesomeIcon.Times.ToIconString() + $"##RemoveChar{character.GameObjectId}"))
                {
                    selectedCharacterIds.Remove(character.GameObjectId);
                }
            }
            ImGui.SameLine();
            using var col = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudYellow);
            ImGui.Text(displayText);
        }

        using var combo = ImRaii.Combo("##MultiCharacter", $"已選 {selectedCharacterIds.Count} 個");
        if (combo)
        {
            foreach (var character in notSelected)
            {
                var displayText = GetCharacterDisplayText(character, false, true);
                var contains = selectedCharacterIds.Contains(character.GameObjectId);
                using var col = ImRaii.PushColor(ImGuiCol.Text, contains ? ImGuiColors.DalamudYellow : ImGuiColors.DalamudWhite);
                if (ImGui.Selectable(displayText, contains, ImGuiSelectableFlags.DontClosePopups))
                {
                    if (contains)
                    {
                        selectedCharacterIds.Remove(character.GameObjectId);
                    }
                    else
                    {
                        selectedCharacterIds.Add(character.GameObjectId);
                    }
                }
            }
        }
        
        // 台服加固：只留這一幀還能在物件表裡解析出來的 id
        foreach (var id in selectedCharacterIds.ToArray())
        {
            if (ResolveCharacter(id) is not { } live || !live.IsValidCharacterBase())
            {
                selectedCharacterIds.Remove(id);
            }
        }

        return selected;
    }

    /// <summary>
    ///     台服加固：呼叫端保存的是 GameObjectId，不是 ICharacter（理由見 ResolveCharacter）。
    ///     回傳值是「這一幀」重新解析出來的角色，用完就丟、不要存成欄位。
    /// </summary>
    public unsafe ICharacter? DrawCharacterSelect(ref ulong selectedCharacterId, ObjectUtil.ValidationFlags flags = ObjectUtil.ValidationFlags.None)
    {
        ICharacter[] objects = GetCharacters(flags);

        var selectedCharacter = ResolveCharacter(selectedCharacterId);
        selectedCharacter ??= objects.FirstOrDefault() ?? clientState.LocalPlayer;

        ImGui.Text("選擇角色");
        if (selectedCharacter?.IsValid() == false && clientState.LocalPlayer != null)
        {
            selectedCharacter = null;
        }

        var preview = selectedCharacter != null ? GetCharacterDisplayText(selectedCharacter, true, true) : "None";
        using (var combo = ImRaii.Combo("##Character", preview))
        {
            if (combo)
            {
                foreach (var character in objects)
                {
                    if (ImGui.Selectable(GetCharacterDisplayText(character, false, true)))
                    {
                        selectedCharacter = character;
                    }
                }
            }
        }
        
        ImGui.SameLine();
        ImGui.Checkbox("Select Target", ref selectTarget);
        
        if (selectTarget)
        {
            if (clientState.LocalPlayer is {TargetObject: not null})
            {
                var target = clientState.LocalPlayer.TargetObject;
                if (target is ICharacter targetCharacter && targetCharacter.IsValidCharacterBase())
                {
                    selectedCharacter = targetCharacter;
                }
                else
                {
                    ImGui.Text("Target is not a valid character");
                }
            }
        }

        if (selectedCharacter != null)
        {
            var charPtr = (Character*)selectedCharacter.Address;
            var drawObj = charPtr->DrawObject;
            if (drawObj != null && !drawObj->IsVisible)
            {
                using (ImRaii.PushColor(ImGuiCol.Text, new Vector4(1, 1, 0, 1)))
                {
                    ImGui.Text("Character is not visible");
                }

                if (clientState.IsGPosing)
                {
                    ImGui.SameLine();        
                    using (ImRaii.PushFont(UiBuilder.IconFont))
                    {
                        ImGui.Text(FontAwesomeIcon.QuestionCircle.ToIconString());
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.BeginTooltip();
                        ImGui.Text(
                            "When in gpose, copies of targetable actors are created, " +
                            "if you're not expecting this, try selecting the character from the list again");
                        ImGui.EndTooltip();
                    }
                }
            }
        }

        selectedCharacterId = selectedCharacter?.GameObjectId ?? 0;
        return selectedCharacter;
    }
    
    public unsafe string GetCharacterDisplayText(IGameObject obj, bool includeDistance, bool includeId)
    {
        if (!obj.IsValid())
        {
            return $"Invalid Object";
        }
        
        string suffix = includeId 
            ? $"##{obj.Address}" 
            : string.Empty;
        
        var drawObject = ((GameObject*)obj.Address)->DrawObject;
        if (drawObject == null)
            return $"Invalid Character{suffix}";

        if (drawObject->Object.GetObjectType() != ObjectType.CharacterBase)
            return $"Invalid Character{suffix}";

        var modelType = ((CharacterBase*)drawObject)->GetModelType();

        var name = obj.Name.TextValue;
        if (obj.ObjectKind == ObjectKind.Player && !string.IsNullOrWhiteSpace(config.PlayerNameOverride))
        {
            name = config.PlayerNameOverride;
        }
        
        string prefix = config.DisplayDebugInfo 
            ? $"[{obj.Address:X8}:{obj.GameObjectId:X}]" 
            : string.Empty;
        string distanceText = includeDistance
            ? $" - {clientState.GetDistanceToLocalPlayer(obj).Length():0}y"
            : string.Empty;
        
        return
            $"{prefix}[{obj.ObjectKind}][{modelType}] - " +
            $"{(string.IsNullOrWhiteSpace(name) ? "Unnamed" : name)}" +
            $"{distanceText}{suffix}";
    }

    public void Dispose()
    {
    }
}
