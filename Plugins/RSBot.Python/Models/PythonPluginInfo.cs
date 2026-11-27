namespace RSBot.Python.Models
{
    public class PythonPluginInfo
    {
        public string Id { get; init; }
        public string FileName { get; init; }
        public string FullPath { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public string Author { get; init; }
        public string Version { get; init; }
        public bool IsEnabled { get; set; }
    }
}
