using Meddle.Plugin.Models;

namespace Meddle.Plugin.UI;

public interface ITab : IDisposable
{
    public string Name { get; }
    public int Order { get; }
    //public bool DisplayTab { get; }
    public MenuType MenuType { get; }

    /// <summary>
    ///     台服加固：分頁可以整個不畫（連分頁標籤都不出現）。
    ///     預設 true＝維持原本行為；只有明確要藏起來的分頁才覆寫。
    /// </summary>
    public bool Enabled => true;

    public void Draw();
}
