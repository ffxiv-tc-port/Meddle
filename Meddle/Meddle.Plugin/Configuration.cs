using System.Text.Json.Serialization;
using Dalamud.Configuration;
using Dalamud.IoC;
using Dalamud.Plugin;
using Meddle.Plugin.Models;
using Meddle.Plugin.UI.Layout;
using Meddle.Plugin.UI.Windows;
using Meddle.Plugin.Utils;
using Meddle.Utils;
using Microsoft.Extensions.Logging;

namespace Meddle.Plugin;

public partial class Configuration : IPluginConfiguration
{
    public const ExportType DefaultExportType = ExportType.GLTF;
    
    [PluginService]
    [JsonIgnore]
    private IDalamudPluginInterface PluginInterface { get; set; } = null!;

    public int Version { get; set; } = 4;
    
    public bool OpenDebugMenuOnLoad { get; set; }
    public LogLevel MinimumNotificationLogLevel { get; set; } = LogLevel.Warning;
    public bool OpenOnLoad { get; set; }
    public bool DisableUserUiHide { get; set; }
    public bool DisableAutomaticUiHide { get; set; }
    public bool DisableCutsceneUiHide { get; set; } = true;
    public bool DisableGposeUiHide { get; set; } = true;
    public string ExportDirectory { get; set; } = Plugin.DefaultExportDirectory;
    public string SecretConfig { get; set; } = string.Empty;
    public bool DisplayDebugInfo { get; set; }
    public bool OpenFolderOnExport { get; set; } = true;

    /// <summary>
    ///     台服加固：「材質參數」分頁會把使用者輸入的值直接寫回遊戲的 CustomizeParameter／材質常數緩衝區，
    ///     上游自己在該分頁頂端標示「可能造成崩潰或其他問題」。台服版預設隱藏，要用得自己在設定頁打開。
    ///     ⚠️ 預設值只對全新安裝生效；既有使用者的設定檔已經寫入這個鍵時以檔案為準。
    /// </summary>
    public bool ShowAdvancedMemoryEditing { get; set; }

    /// <summary>
    ///     台服加固：一次性遷移旗標。場景疊加層（<see cref="LayoutWindow.LayoutConfig.DrawOverlay" />）
    ///     的預設值已改為關閉，但 Dalamud 的 <c>PluginConfigurations.SerializeConfig</c> 沒有設
    ///     <c>DefaultValueHandling</c>，既有使用者的設定檔已經寫著 <c>true</c>
    ///     ⇒ 改預設值對他們完全無效。用這個旗標把既有使用者關掉一次，之後他們自己再打開就不會再被動到。
    ///     ⚠️ 這是回退既有行為，所以只跑一次。
    /// </summary>
    public bool TcLayoutOverlayDefaultMigrated { get; set; }


    /// <summary>
    /// Used to hide names in the UI
    /// </summary>
    public string PlayerNameOverride { get; set; } = string.Empty;

    public LayoutWindow.LayoutConfig LayoutConfig { get; set; } = new();
    public ExportConfiguration ExportConfig { get; set; } = new();
    public UpdateWindow.UpdateConfig UpdateConfig { get; set; } = new();
    
    public class ExportConfiguration
    {
        public CacheFileType CacheFileTypes { get; set; }
        public ExportType ExportType { get; set; } = DefaultExportType;
        
        [Obsolete("TextureMode is only ever Raw as baking is no longer supported", true)]
        public TextureMode TextureMode { get; set; } = TextureMode.Raw;
        public SkeletonUtils.PoseMode PoseMode { get; set; } = SkeletonUtils.PoseMode.Local;
        
        [Obsolete("Use PoseMode instead", true)]
        public bool ExportPose { get; set; } = true;
        public bool RemoveAttributeDisabledSubmeshes { get; set; } = true;
        public bool SkipHiddenBgParts { get; set; }
        public bool UseDeformer { get; set; } = true;
        
        public bool LimitTerrainExportRange { get; set; }
        public float TerrainExportDistance { get; set; } = 500f;
        public bool EnableWindingFlip { get; set; }

        // public enum ExportRootAttachHandling
        // {
        //     PlayerAsAttachChild,
        //     Exclude,
        // }
        
        // public ExportRootAttachHandling RootAttachHandling { get; set; } = ExportRootAttachHandling.Exclude;
        
        public ExportConfiguration Clone()
        {
            return new ExportConfiguration
            {
                CacheFileTypes = CacheFileTypes,
                ExportType = ExportType,
                // ExportPose = ExportPose,
                // TextureMode = TextureMode,
                PoseMode = PoseMode,
                RemoveAttributeDisabledSubmeshes = RemoveAttributeDisabledSubmeshes,
                SkipHiddenBgParts = SkipHiddenBgParts,
                // RootAttachHandling = RootAttachHandling
                UseDeformer = UseDeformer,
                LimitTerrainExportRange = LimitTerrainExportRange,
                TerrainExportDistance = TerrainExportDistance,
                EnableWindingFlip = EnableWindingFlip
            };
        }

        public void SetDefaultCloneOptions()
        {
            RemoveAttributeDisabledSubmeshes = true;
            SkipHiddenBgParts = true;
            UseDeformer = true;
        }
        
        public void Apply(ExportConfiguration other)
        {
            CacheFileTypes = other.CacheFileTypes;
            ExportType = other.ExportType;
            // ExportPose = other.ExportPose;
            // TextureMode = other.TextureMode;
            PoseMode = other.PoseMode;
            RemoveAttributeDisabledSubmeshes = other.RemoveAttributeDisabledSubmeshes;
            SkipHiddenBgParts = other.SkipHiddenBgParts;
            // RootAttachHandling = other.RootAttachHandling;
            UseDeformer = other.UseDeformer;
            LimitTerrainExportRange = other.LimitTerrainExportRange;
            TerrainExportDistance = other.TerrainExportDistance;
            EnableWindingFlip = other.EnableWindingFlip;
        }
        
        public MeshBuilderOptions CreateMeshBuilderOptions()
        {
            return new MeshBuilderOptions
            {
                EnableWindingFlip = EnableWindingFlip
            };
        }
    }

    public event Action? OnConfigurationSaved;

    public void Save()
    {
        PluginInterface.SavePluginConfig(this);
        OnConfigurationSaved?.Invoke();
    }
}
