namespace Meddle.Plugin.UI;

public class Constants
{
    public const string KoFiUrl = "https://ko-fi.com/ramen_au";
    public const string CarrdUrl = "https://meddle.carrd.co/";
    public const string DiscordUrl = "https://discord.gg/2jnZMNVM4p";
    public const string MeddleToolsUrl = "https://github.com/PassiveModding/MeddleTools";
    public const string MeddleUrl = "https://github.com/PassiveModding/Meddle";
    public const string MeddleBugReportUrl = "https://github.com/PassiveModding/Meddle/issues";

    /// <summary>
    ///     台服加固：場景／地形分頁用的結構偏移（RenderLight、EnvLighting、DecalItem、SgbFile、Terrain…）
    ///     抄自對全球版寫的參考實作，沒有對台服客戶端逐欄驗過。
    /// </summary>
    public const string TcUnverifiedOffsetWarning =
        "⚠️ 結構偏移未對台服驗證，展開可能導致崩潰；NPC 匯出不需要此分頁。";
}
