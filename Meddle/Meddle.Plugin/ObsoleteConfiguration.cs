using System.Numerics;
using Meddle.Plugin.Models;
using Meddle.Plugin.Models.Layout;
using Meddle.Plugin.UI.Layout;
using Microsoft.Extensions.Logging;

namespace Meddle.Plugin;

public partial class Configuration
{
    [Obsolete("Use LayoutConfig.WorldCutoffDistance instead")]
    public float WorldCutoffDistance { get; set; } = 100;
    
    [Obsolete("Use LayoutConfig.WorldDotColor instead")]
    public Vector4 WorldDotColor { get; set; } = new(1f, 1f, 1f, 0.5f);
    
    /// <summary>
    /// If enabled, pose will be included at 0 on the timeline under the 'pose' track.
    /// </summary>
    [Obsolete("Use ExportConfig.ExportPose", true)]
    public bool IncludePose { get; set; } = true;

    [Obsolete("Use ExportConfig.TextureMode", true)]
    public TextureMode TextureMode { get; set; } = TextureMode.Bake;
    
    /// <summary>
    /// GLTF = GLTF JSON
    /// GLB = GLTF Binary
    /// OBJ = Wavefront OBJ
    /// </summary>
    [Obsolete("Use ExportConfig.ExportType", true)]
    public ExportType ExportType { get; set; } = DefaultExportType;
    
    /// <param name="logger">
    ///     ⚠️ 一定要傳。<c>Migrate()</c> 是在 host 建起來之前呼叫的，那時 <c>Plugin.Logger</c>
    ///     還是 <c>NullLogger</c> ⇒ 不傳的話這裡所有的 LogInformation 都會靜默不見。
    /// </param>
    public void Migrate(ILogger? logger = null)
    {
        logger ??= Plugin.Logger;
#pragma warning disable CS0618 // Type or member is obsolete
        if (Version == 1)
        {
            logger.LogInformation("Migrating configuration from version 1 to 2");
            if (DisableAutomaticUiHide == false)
            {
                DisableAutomaticUiHide = true;
            }
            
            if (DisableCutsceneUiHide == false)
            {
                DisableCutsceneUiHide = true;
            }
            
            Version = 2;
            Save();
        }
        
        if (Version == 2)
        {
            logger.LogInformation("Migrating configuration from version 2 to 3");
            
            LayoutConfig.DrawTypes = LayoutWindow.DefaultDrawTypes;
            
            Version = 3;
            Save();
        }

        if (Version == 3)
        {
            logger.LogInformation("Migrating configuration from version 3 to 4");
            LayoutConfig.WorldDotColor = WorldDotColor;
            LayoutConfig.WorldCutoffDistance = WorldCutoffDistance;
            // 台服加固：刻意不加 Decal 與 EnvLighting（上游這裡會加）。兩個都是解析閘門，開了就會走
            // 未對台服驗證的自訂結構偏移（Decal：DecalLayoutInstance／Decal／DecalItem）；理由同 LayoutWindow.DefaultDrawTypes。

            Version = 4;
            Save();
        }

        // 台服加固：一次性遷移——把既有使用者的場景疊加層關掉一次。
        // ⚠️ 這是回退既有行為（使用者裁決過），所以只跑一次，跑過就記在 TcLayoutOverlayDefaultMigrated；
        //    使用者之後自己再打開就不會再被動到。
        if (!TcLayoutOverlayDefaultMigrated)
        {
            TcLayoutOverlayDefaultMigrated = true;
            if (LayoutConfig.DrawOverlay)
            {
                LayoutConfig.DrawOverlay = false;
                logger.LogInformation(
                    "台服加固：已關閉「場景」分頁的疊加層。疊加層會讓場景解析持續重跑，途中會讀取未對台服驗證的結構偏移；要用請自己在該分頁的 Overlay Options 重新勾選。");
            }

            Save();
        }
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
