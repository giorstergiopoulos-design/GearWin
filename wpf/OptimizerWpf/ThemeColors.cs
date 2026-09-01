using System.Windows.Media;

namespace OptimizerWpf
{
    // One theme's full palette - mirrors the hashtable returned by Get-ThemeColors in Optimizer.ps1
    // (MainBg/TabBg/TabSelect/Text/SubText/StatusText/CardBg/CardBorder/BtnDefault/BtnHover/Accent).
    public record ThemeColors(
        string Name,
        Color MainBg,
        Color TabBg,
        Color TabSelect,
        Color Text,
        Color SubText,
        Color StatusText,
        Color CardBg,
        Color CardBorder,
        Color BtnDefault,
        Color BtnHover,
        Color Accent);
}
