using System.IO;
using YamlDotNet.Serialization;

namespace PacketSender.Packet
{
    public class PacketField
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    public class PacketDefinition
    {
        public string Type { get; set; } = string.Empty;
        public string PacketName { get; set; } = string.Empty;
        public string Desc { get; set; } = string.Empty;
        public int PacketId { get; set; }

        public List<PacketField> Items { get; set; } = [];
    }

    public class PacketRoot
    {
        [YamlMember(Alias = "Packet")] public List<PacketDefinition>? Packets { get; set; } = [];
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
            var packets = root.Packets ?? [];

            AssignPacketIds(packets);

            return packets;
        }

        public static void AssignPacketIds(List<PacketDefinition> packets, int startId = 1)
        {
            for (var i = 0; i < packets.Count; i++)
            {
                packets[i].PacketId = startId + i;
            }
        }
    }
}
