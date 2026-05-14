namespace SystemOptimizer.Models;

public class CleanupLogItem
{
    public string Icon { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public string StatusColor { get; set; } = "Gray";
    public bool IsBold { get; set; }
}
