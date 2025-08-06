using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using System.Windows.Threading;
using PacketSender.Packet;
using PacketSender.ViewModels;
using System.Text;
using System.IO;
using PacketSender.DLL;

namespace PacketSender
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly DispatcherTimer _searchTimer;

        public ObservableCollection<PacketDefinition> Packets { get; } = [];

        public ObservableCollection<PacketDefinition> FilteredPackets { get; } = [];

        private PacketDefinition? _selectedPacket;
        public PacketDefinition? SelectedPacket
        {
            get => _selectedPacket;
            set
            {
                _selectedPacket = value;
                OnPropertyChanged();

                PacketInstance.LoadPacket(value);
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();

                _searchTimer.Stop();
                _searchTimer.Start();
            }
        }

        public ICommand ClearSearchCommand { get; }
        public PacketInstanceViewModel PacketInstance { get; }

        public MainWindow()
        {
            InitializeComponent();
            PacketInstance = new PacketInstanceViewModel();
            PacketInstance.PacketSendRequested += OnPacketSendRequest;

            DataContext = this;

            _searchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _searchTimer.Tick += (_, _) =>
            {
                _searchTimer.Stop();
                FilterPackets();
            };

            ClearSearchCommand = new RelayCommand(ClearSearch);
            ClientProxySender.Instance.LoadClientProxySenderDll();
            if (ClientProxySender.Instance.Start("CoreOption.txt", "SessionGetterOption.txt") == false)
            {
                MessageBox.Show("Failed to start ClientProxySender. Run non send packet mode.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            LoadPackets();
        }

        private void LoadPackets()
        {
            try
            {
                var loadedPackets = PacketYamlLoader.LoadPacketsFromYaml("../../../PacketDefine.yml");
                foreach (var packet in loadedPackets)
                {
                    Packets.Add(packet);
                }

                FilterPackets();
            }
            catch (Exception e)
            {
                MessageBox.Show($"An error occurred while loading packets: {e.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterPackets()
        {
            FilteredPackets.Clear();

            if (string.IsNullOrWhiteSpace(SearchText))
            {
                foreach (var packet in Packets)
                {
                    FilteredPackets.Add(packet);
                }
            }
            else
            {
                var searchLower = SearchText.ToLowerInvariant();
                foreach (var packet in Packets)
                {
                    if (packet.PacketName.ToLowerInvariant().Contains(searchLower))
                    {
                        FilteredPackets.Add(packet);
                    }
                }
            }

            if (SelectedPacket != null && !FilteredPackets.Contains(SelectedPacket))
            {
                SelectedPacket = null;
            }
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void OnPacketSendRequest(int packetId, Dictionary<string, object> fieldValues)
        {
            try
            {
                var binarySerialized = SerializeToBinary(packetId, fieldValues);
                if (binarySerialized.Length == 0)
                {
                    MessageBox.Show("No data to send.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ClientProxySender.Instance.SendPacket(binarySerialized);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SendPacketRequest error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private byte[] SerializeToBinary(int packetId, Dictionary<string, object> fieldValues)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            writer.Write(packetId);
            foreach (var field in fieldValues)
            {
                SerializeFieldValue(writer, field.Value);
            }

            return stream.ToArray();
        }

        private void SerializeFieldValue(BinaryWriter writer, object value)
        {
            switch (value)
            {
                case int v: writer.Write(v); break;
                case float v: writer.Write(v); break;
                case double v: writer.Write(v); break;
                case bool v: writer.Write(v); break;
                case char v: writer.Write(v); break;
                case string v:
                    writer.Write(v.Length);
                    writer.Write(v);
                    break;
                case object[] v:
                    writer.Write(v.Length);
                    foreach (var item in v)
                    {
                        SerializeFieldValue(writer, item);
                    }
                    break;
                default:
                    writer.Write(value.ToString() ?? string.Empty);
                    break;
            }
        }

        private object DeserializeFieldValue(BinaryReader reader)
        {
            var typeMarker = reader.ReadByte();

            return typeMarker switch
            {
                1 => reader.ReadInt32(),
                2 => reader.ReadSingle(),
                3 => reader.ReadDouble(),
                4 => reader.ReadBoolean(),
                5 => reader.ReadString(),
                6 => DeserializeArray(reader),
                _ => reader.ReadString()
            };
        }

        private object[] DeserializeArray(BinaryReader reader)
        {
            var length = reader.ReadInt32();
            var array = new object[length];

            for (var i = 0; i < length; i++)
            {
                array[i] = DeserializeFieldValue(reader);
            }

            return array;
        }
    }

    public class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
    {
        private readonly Action _execute = execute ?? throw new ArgumentNullException(nameof(execute));

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter)
        {
            return canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute();
        }
    }
}