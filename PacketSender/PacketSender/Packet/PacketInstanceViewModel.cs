using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using PacketSender.Packet;

namespace PacketSender.ViewModels
{
    public class PacketFieldInstance : INotifyPropertyChanged
    {
        private string _value = string.Empty;

        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public bool IsArray { get; set; }
        public string ElementType { get; set; } = string.Empty;

        public string Value
        {
            get => _value;
            set
            {
                _value = value;
                OnPropertyChanged();
            }
        }


        public PacketFieldInstance(PacketField field)
        {
            Name = field.Name;
            Type = field.Type;

            if (IsVectorType(field.Type))
            {
                IsArray = true;
                ElementType = ExtractElementType(field.Type);
                Value = GetDefaultArrayValue(ElementType);
            }
            else
            {
                IsArray = false;
                ElementType = field.Type;
                Value = GetDefaultValue(field.Type);
            }
        }

        private bool IsVectorType(string type)
        {
            return type.Contains("std::vector<") || type.Contains("[]") || type.Contains("Array<");
        }

        private string ExtractElementType(string type)
        {
            if (type.Contains("std::vector<"))
            {
                var start = type.IndexOf('<') + 1;
                var end = type.LastIndexOf('>');
                return type.Substring(start, end - start);
            }
            else if (type.Contains("[]"))
            {
                return type.Replace("[]", "");
            }
            else if (type.Contains("Array<"))
            {
                var start = type.IndexOf('<') + 1;
                var end = type.LastIndexOf('>');
                return type.Substring(start, end - start);
            }
            return type;
        }

        private string GetDefaultArrayValue(string elementType)
        {
            return elementType.ToLower() switch
            {
                "int" => "[1, 2, 3]",
                "float" => "[1.0, 2.0, 3.0]",
                "double" => "[1.0, 2.0, 3.0]",
                "bool" => "[true, false, true]",
                "std::string" => "[\"item1\", \"item2\", \"item3\"]",
                _ => "[]"
            };
        }

        private string GetDefaultValue(string type)
        {
            return type.ToLower() switch
            {
                "int" => "0",
                "float" => "0.0",
                "double" => "0.0",
                "bool" => "false",
                "std::string" => "",
                _ => ""
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PacketInstanceViewModel : INotifyPropertyChanged
    {
        private PacketDefinition? _packetDefinition;

        public string PacketName { get; private set; } = string.Empty;
        public string PacketType { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;

        public ObservableCollection<PacketFieldInstance> Fields { get; set; } = [];

        public ICommand SendPacketCommand { get; }
        public ICommand ClearFieldsCommand { get; }

        private bool _isReadOnly { get; set; }

        public PacketInstanceViewModel()
        {
            SendPacketCommand = new RelayCommand(SendPacket, CanSendPacket);
            ClearFieldsCommand = new RelayCommand(ClearFields, () => Fields.Count > 0);
        }

        public void LoadPacket(PacketDefinition? packet)
        {
            _packetDefinition = packet;
            Fields.Clear();

            if (packet == null)
            {
                PacketName = string.Empty;
                PacketType = string.Empty;
                Description = string.Empty;
                OnPropertyChanged(nameof(PacketName));
                OnPropertyChanged(nameof(PacketType));
                OnPropertyChanged(nameof(Description));
                return;
            }

            PacketName = packet.PacketName;
            PacketType = packet.Type;
            Description = packet.Desc;
            _isReadOnly = PacketType == "ReplyPacket";

            foreach (var field in packet.Items)
            {
                Fields.Add(new PacketFieldInstance(field));
            }

            OnPropertyChanged(nameof(PacketName));
            OnPropertyChanged(nameof(PacketType));
            OnPropertyChanged(nameof(Description));
        }

        private bool CanSendPacket()
        {
            return _packetDefinition != null && (_isReadOnly == false);
        }

        private void SendPacket()
        {
            if (_packetDefinition == null)
            {
                return;
            }

            var packetData = new Dictionary<string, object>();

            foreach (var field in Fields)
            {
                var convertedValue = ConvertValue(field.Value, field.Type);
                packetData[field.Name] = convertedValue;
            }

            PacketSendRequested?.Invoke(packetData);
        }

        private object ConvertValue(string value, string type)
        {
            try
            {
                if (IsVectorType(type))
                {
                    return ParseArrayValue(value, ExtractElementType(type));
                }

                return type.ToLower() switch
                {
                    "int" => int.Parse(value),
                    "float" => float.Parse(value),
                    "double" => double.Parse(value),
                    "bool" => bool.Parse(value),
                    "std::string" => value,
                    _ => value
                };
            }
            catch
            {
                return GetDefaultValueObject(type);
            }
        }

        private bool IsVectorType(string type)
        {
            return type.Contains("std::vector<") || type.Contains("[]") || type.Contains("Array<");
        }

        private string ExtractElementType(string type)
        {
            if (type.Contains("std::vector<"))
            {
                var start = type.IndexOf('<') + 1;
                var end = type.LastIndexOf('>');
                return type.Substring(start, end - start);
            }

            if (type.Contains("[]"))
            {
                return type.Replace("[]", "");
            }

            if (!type.Contains("Array<")) return type;
            {
                var start = type.IndexOf('<') + 1;
                var end = type.LastIndexOf('>');
                return type.Substring(start, end - start);
            }

        }

        private object ParseArrayValue(string value, string elementType)
        {
            try
            {
                value = value.Trim();
                if (!value.StartsWith($"[") || !value.EndsWith($"]"))
                {
                    return Array.Empty<object>();
                }

                var content = value.Substring(1, value.Length - 2).Trim();
                if (string.IsNullOrEmpty(content))
                {
                    return Array.Empty<object>();
                }

                var elements = SplitArrayElements(content);

                return elements.Select(element => element.Trim().Trim('"'))
                    .Select(trimmedElement => (object)(elementType.ToLower() switch
                    {
                        "int" => int.Parse(trimmedElement),
                        "float" => float.Parse(trimmedElement),
                        "double" => double.Parse(trimmedElement),
                        "bool" => bool.Parse(trimmedElement),
                        "std::string" => trimmedElement,
                        _ => trimmedElement
                    })).ToArray();
            }
            catch
            {
                return Array.Empty<object>();
            }
        }

        private List<string> SplitArrayElements(string content)
        {
            var elements = new List<string>();
            var current = new StringBuilder();
            var inQuotes = false;
            var depth = 0;

            for (var i = 0; i < content.Length; i++)
            {
                var c = content[i];

                switch (c)
                {
                    case '"' when (i == 0 || content[i - 1] != '\\'):
                        inQuotes = !inQuotes;
                        current.Append(c);
                        break;
                    case '[' when !inQuotes:
                        depth++;
                        current.Append(c);
                        break;
                    case ']' when !inQuotes:
                        depth--;
                        current.Append(c);
                        break;
                    case ',' when !inQuotes && depth == 0:
                        elements.Add(current.ToString());
                        current.Clear();
                        break;
                    default:
                        current.Append(c);
                        break;
                }
            }

            if (current.Length > 0)
            {
                elements.Add(current.ToString());
            }

            return elements;
        }

        private static object GetDefaultValueObject(string type)
        {
            return type.ToLower() switch
            {
                "int" => 0,
                "float" => 0.0f,
                "double" => 0.0,
                "bool" => false,
                "std::string" => "",
                _ => ""
            };
        }

        private void ClearFields()
        {
            foreach (var field in Fields)
            {
                field.Value = GetDefaultValue(field.Type);
            }
        }

        private static string GetDefaultValue(string type)
        {
            return type.ToLower() switch
            {
                "int" => "0",
                "float" => "0.0",
                "double" => "0.0",
                "bool" => "false",
                "std::string" => "",
                _ => ""
            };
        }

        public event Action<Dictionary<string, object>>? PacketSendRequested;
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}