using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace PacketSender.PacketLoader
{
    public class PacketField
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        
        [YamlMember(Alias = "Items")]
        public List<PacketField> Fields { get; set; } = [];
    }

    public class PacketDefinition
    {
        public string Type { get; set; } = string.Empty;
        public string PacketName { get; set; } = string.Empty;

        public List<PacketField> Items { get; set; } = [];
    }

    public class PacketRoot
    {
        [YamlMember(Alias = "Packet")]
        public List<PacketDefinition>? Packets { get; set; } = [];
    }

    public static class PacketYamlLoader
    {
        public static List<PacketDefinition> LoadPacketsFromYaml(string yamlPath)
        {
            if (File.Exists(yamlPath) == false)
            {
                throw new FileNotFoundException($"The specified YAML file does not exist: {yamlPath}");
            }

            var yamlContent = File.ReadAllText(yamlPath);
            var deserializer = new DeserializerBuilder()
                .Build();

            var root = deserializer.Deserialize<PacketRoot>(yamlContent);
            return root?.Packets ?? [];
        }
    }
}
