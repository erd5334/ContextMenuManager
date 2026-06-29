namespace ContextMenuManager
{
    public class ShortcutItem
    {
        public string Id { get; set; } = string.Empty;
        public string Group { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
    }
}
