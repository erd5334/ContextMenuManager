namespace ContextMenuManager
{
    public class ShellExtensionItem
    {
        public string KeyName { get; set; } = string.Empty;
        public string Clsid { get; set; } = string.Empty;
        public string RegistryPath { get; set; } = string.Empty;
        public string TargetDisplay { get; set; } = string.Empty; // "Tüm Dosyalar", "Klasör", "Boş Alan"
        public bool IsBlocked { get; set; }
    }
}
