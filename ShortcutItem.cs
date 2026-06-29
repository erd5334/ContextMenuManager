namespace ContextMenuManager
{
    public class ShortcutItem
    {
        public string Id { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public string TargetType { get; set; } = string.Empty;      // "Background", "Directory", "AllFiles"
        public string TargetDisplay { get; set; } = string.Empty;   // "Boş Alan", "Klasör", "Tüm Dosyalar"
        public string Position { get; set; } = string.Empty;        // "Top", "Bottom", "Default"
        public string IconPath { get; set; } = string.Empty;
    }
}
