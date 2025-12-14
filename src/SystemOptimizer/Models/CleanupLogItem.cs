using System.Windows.Media;

namespace SystemOptimizer.Models
{
    public class CleanupLogItem
    {
        public string Icon { get; set; } = "Info24"; // SymbolIcon name
        public string Message { get; set; } = string.Empty;
        public string StatusColor { get; set; } = "Gray";
        public bool IsBold { get; set; } = false;
    }
}
