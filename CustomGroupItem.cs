namespace ContextMenuManager
{
    public class CustomGroupItem
    {
        public string Id { get; set; } = string.Empty; // TargetType|CustomGroup_KeyName
        public string Name { get; set; } = string.Empty; // MUIVerb
        public string TargetType { get; set; } = string.Empty; // Background, Directory, AllFiles, etc.
        public string TargetDisplay { get; set; } = string.Empty; // Boş Alan, Klasör vb.
        public string IconPath { get; set; } = string.Empty;
        public string Position { get; set; } = string.Empty; // Default, Top, Bottom
    }
}
