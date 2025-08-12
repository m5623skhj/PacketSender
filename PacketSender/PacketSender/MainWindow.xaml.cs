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
using System.Windows.Controls;

namespace PacketSender
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        private readonly DispatcherTimer _searchTimer;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private Task? _packetReceiveTask;

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
                PacketViewModel.LoadPacket(value);
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
        public PacketInstanceViewModel PacketViewModel { get; }
        private readonly StringBuilder _logBuilder = new();
        private string _logText = string.Empty;
        private ScrollViewer? _logScrollViewer;

        public string LogText
        {
            get => _logText;
            set
            {
                _logText = value;
                OnPropertyChanged();
                Dispatcher.BeginInvoke(new Action(ScrollToBottom), DispatcherPriority.Background);
            }
        }

        public ICommand ClearLogCommand { get; private set; }

        public MainWindow()
        {
            InitializeComponent();
            PacketViewModel = new PacketInstanceViewModel();
            PacketViewModel.PacketSendRequested += OnPacketSendRequest;

            DataContext = this;
            _cancellationTokenSource = new CancellationTokenSource();

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
            ClearLogCommand = new RelayCommand(ClearLog);

            ClientProxySender.Instance.LoadClientProxySenderDll();
            if (ClientProxySender.Instance.Start("CoreOption.txt", "SessionGetterOption.txt") == false)
            {
                MessageBox.Show("Failed to start ClientProxySender. Run non send packet mode.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            LoadPackets();

            this.Loaded += (_, _) =>
            {
                _logScrollViewer = FindLogScrollViewer();
                StartPacketReceiver();
            };

            this.Closing += (_, _) =>
            {
                StopPacketReceiver();
            };
        }

        private void StartPacketReceiver()
        {
            _packetReceiveTask = Task.Run(async () =>
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (ClientProxySender.Instance.GetStreamDataFromStoredPacket(out var streamData))
                        {
                            if (streamData is { Length: > 0 })
                            {
                                Dispatcher.Invoke(() =>
                                {
                                    LogReceivedPacket(streamData);
                                });

                                await LogReceivedPacketToFileAsync(streamData);
                            }
                        }

                        await Task.Delay(10, _cancellationTokenSource.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            LogErrorToUi($"Packet receive error: {ex.Message}");
                        });

                        await Task.Delay(100, _cancellationTokenSource.Token);
                    }
                }
            }, _cancellationTokenSource.Token);
        }

        private void StopPacketReceiver()
        {
            try
            {
                _cancellationTokenSource.Cancel();
                _packetReceiveTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping packet receiver: {ex.Message}");
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }
        }

        private void LogReceivedPacket(byte[] packetData)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

            _logBuilder.AppendLine($"[{timestamp}] === PACKET RECEIVED ===");
            _logBuilder.AppendLine($"Binary Size: {packetData.Length} bytes");

            try
            {
                var parsedData = ParseReceivedPacket(packetData);
                if (parsedData != null)
                {
                    _logBuilder.AppendLine($"Packet ID: {parsedData.PacketId}");
                    _logBuilder.AppendLine($"Packet Name: {parsedData.PacketName}");
                    _logBuilder.AppendLine("Fields:");
                    
                    var packetDefinition = Packets.FirstOrDefault(p => p.PacketId == parsedData.PacketId);
                    if (packetDefinition != null)
                    {
                        foreach (var field in parsedData.Fields)
                        {
                            _logBuilder.AppendLine($"  {field.Key} : {FormatFieldValue(field.Value)}");
                        }
                    }
                    else
                    {
                        foreach (var field in parsedData.Fields)
                        {
                            _logBuilder.AppendLine($"  {field.Key}: {FormatFieldValue(field.Value)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logBuilder.AppendLine($"Parse error: {ex.Message}");
            }

            _logBuilder.AppendLine($"Hex: {BitConverter.ToString(packetData).Replace("-", " ")}");
            _logBuilder.AppendLine();

            LogText = _logBuilder.ToString();

            if (_logBuilder.Length <= 100000)
            {
                return;
            }

            var lines = LogText.Split('\n');
            var keepLines = lines.Skip(Math.Max(0, lines.Length - 500)).ToArray();
            _logBuilder.Clear();
            _logBuilder.AppendLine("[LOG TRUNCATED - Showing recent 500 lines]");
            _logBuilder.AppendLine();
            foreach (var line in keepLines)
            {
                _logBuilder.AppendLine(line);
            }
            LogText = _logBuilder.ToString();
        }

        private async Task LogReceivedPacketToFileAsync(byte[] packetData)
        {
            try
            {
                EnsureLogDirectory();
                var logFilePath = GetLogFilePath();

                var logEntry = new StringBuilder();
                logEntry.AppendLine($"=== PACKET RECEIVED at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===");
                logEntry.AppendLine($"Binary Size: {packetData.Length} bytes");

                try
                {
                    var parsedData = ParseReceivedPacket(packetData);
                    if (parsedData != null)
                    {
                        logEntry.AppendLine($"Packet ID: {parsedData.PacketId}");
                        logEntry.AppendLine($"Packet Name: {parsedData.PacketName}");
                        logEntry.AppendLine("Fields:");
                        
                        var packetDefinition = Packets.FirstOrDefault(p => p.PacketId == parsedData.PacketId);
                        if (packetDefinition != null)
                        {
                            foreach (var field in parsedData.Fields)
                            {
                                logEntry.AppendLine($"  {field.Key} : {FormatFieldValue(field.Value)}");
                            }
                        }
                        else
                        {
                            foreach (var field in parsedData.Fields)
                            {
                                logEntry.AppendLine($"  {field.Key}: {FormatFieldValue(field.Value)}");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logEntry.AppendLine($"Parse error: {ex.Message}");
                }

                logEntry.AppendLine($"Binary Data (Hex): {BitConverter.ToString(packetData).Replace("-", " ")}");
                logEntry.AppendLine($"Binary Data (Base64): {Convert.ToBase64String(packetData)}");
                logEntry.AppendLine();

                await File.AppendAllTextAsync(logFilePath, logEntry.ToString());
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LogErrorToUi($"File log error: {ex.Message}");
                });
            }
        }

        private ParsedPacketData? ParseReceivedPacket(byte[] packetData)
        {
            if (packetData.Length < 4)
            {
                return null;
            }

            using var stream = new MemoryStream(packetData);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            try
            {
                var packetId = reader.ReadInt32();
                var packetDefinition = Packets[packetId - 1];

                var fields = new Dictionary<string, object>();

                foreach (var item in packetDefinition.Items)
                {
                    try
                    {
                        if (stream.Position >= stream.Length)
                        {
                            break;
                        }

                        var value = DeserializeFieldValue(reader, item.Type);
                        fields[item.Name] = value;
                    }
                    catch (Exception ex)
                    {
                        fields[item.Name] = $"<PARSE_ERROR: {ex.Message}>";
                        System.Diagnostics.Debug.WriteLine($"Failed to parse field '{item.Name}': {ex.Message}");
                    }
                }

                return new ParsedPacketData { PacketId = packetId, PacketName = packetDefinition.PacketName, Fields = fields };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to parse packet: {ex.Message}");
                return null;
            }
        }

        private class ParsedPacketData
        {
            public int PacketId { get; init; }
            public string PacketName { get; init; }
            public Dictionary<string, object> Fields { get; init; } = new();
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

                LogPacketToUi(packetId, fieldValues, binarySerialized);
                LogPacketToFileAsync(packetId, fieldValues, binarySerialized);

                ClientProxySender.Instance.SendPacket(binarySerialized);
            }
            catch (Exception ex)
            {
                LogErrorToUi($"SendPacketRequest error: {ex.Message}");
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

        private static void SerializeFieldValue(BinaryWriter writer, object value)
        {
            switch (value)
            {
                case int v: writer.Write(v); break;
                case float v: writer.Write(v); break;
                case double v: writer.Write(v); break;
                case bool v: writer.Write(v); break;
                case char v: writer.Write(v); break;
                case string v:
                    writer.Write((ushort)v.Length);
                    var bytes = Encoding.UTF8.GetBytes(v);
                    writer.Write(bytes);
                    break;
                case object[] v:
                    writer.Write((short)v.Length);
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

        private object DeserializeFieldValue(BinaryReader reader, string fieldType)
        {
            try
            {
                return fieldType switch
                {
                    "int" => reader.ReadInt32(),
                    "float" => reader.ReadSingle(),
                    "double" => reader.ReadDouble(),
                    "bool" => reader.ReadBoolean(),
                    "std::string" => ReadStringWithLength(reader),
                    "array" => DeserializeArray(reader, "int"),
                    _ => throw new ArgumentException($"Unknown field type: {fieldType}")
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeserializeFieldValue error for type '{fieldType}': {ex.Message}");
                throw;
            }
        }

        private object[] DeserializeArray(BinaryReader reader, string elementType)
        {
            try
            {
                var length = reader.ReadInt32();
                
                if (length is < 0 or > 10000)
                {
                    throw new ArgumentException($"Invalid array length: {length}");
                }
                
                var array = new object[length];

                for (var i = 0; i < length; i++)
                {
                    array[i] = DeserializeFieldValue(reader, elementType);
                }

                return array;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DeserializeArray error: {ex.Message}");
                throw;
            }
        }

        private static string ReadStringWithLength(BinaryReader reader)
        {
            try
            {
                var length = reader.ReadUInt16();
                var bytes = reader.ReadBytes(length);
                return Encoding.UTF8.GetString(bytes);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReadStringWithLength error: {ex.Message}");
                throw;
            }
        }

        private void LogPacketToUi(int packetId, Dictionary<string, object> fieldValues, byte[] binaryData)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");

            _logBuilder.AppendLine($"[{timestamp}] === PACKET SENT ===");
            _logBuilder.AppendLine($"Packet ID: {packetId}");
            _logBuilder.AppendLine($"Packet Name: {SelectedPacket?.PacketName ?? "Unknown"}");
            _logBuilder.AppendLine($"Binary Size: {binaryData.Length} bytes");

            _logBuilder.AppendLine("Fields:");
            foreach (var field in fieldValues)
            {
                var valueStr = FormatFieldValue(field.Value);
                _logBuilder.AppendLine($"  {field.Key}: {valueStr}");
            }

            _logBuilder.AppendLine($"Hex: {BitConverter.ToString(binaryData).Replace("-", " ")}");
            _logBuilder.AppendLine();

            Dispatcher.Invoke(() =>
            {
                LogText = _logBuilder.ToString();

                if (_logBuilder.Length <= 100000)
                {
                    return;
                }

                var lines = LogText.Split('\n');
                var keepLines = lines.Skip(Math.Max(0, lines.Length - 500)).ToArray();
                _logBuilder.Clear();
                _logBuilder.AppendLine("[LOG TRUNCATED - Showing recent 500 lines]");
                _logBuilder.AppendLine();
                foreach (var line in keepLines)
                {
                    _logBuilder.AppendLine(line);
                }
                LogText = _logBuilder.ToString();
            });
        }

        private void LogErrorToUi(string errorMessage)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            _logBuilder.AppendLine($"[{timestamp}] ERROR: {errorMessage}");
            _logBuilder.AppendLine();

            Dispatcher.Invoke(() =>
            {
                LogText = _logBuilder.ToString();
            });
        }

        private void ClearLog()
        {
            _logBuilder.Clear();
            LogText = string.Empty;
            Console.WriteLine("Log cleared successfully");
        }

        private ScrollViewer? FindLogScrollViewer()
        {
            return FindName("LogScrollViewer") as ScrollViewer;
        }

        private static string FormatFieldValue(object value)
        {
            return value switch
            {
                object[] array => $"[{string.Join(", ", array.Select(FormatFieldValue))}]",
                string str => $"\"{str}\"",
                _ => value.ToString() ?? "null"
            };
        }

        private void ScrollToBottom()
        {
            try
            {
                _logScrollViewer?.ScrollToEnd();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Scroll error: {ex.Message}");
            }
        }

        private static string GetLogFilePath()
        {
            var exeDirectory = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
            return Path.Combine(exeDirectory, "Logs", $"PacketLog_{DateTime.Now:yyyyMMdd}.txt");
        }

        private static void EnsureLogDirectory()
        {
            try
            {
                var logFilePath = GetLogFilePath();
                var logDir = Path.GetDirectoryName(logFilePath);

                if (string.IsNullOrEmpty(logDir) || Directory.Exists(logDir))
                {
                    return;
                }

                Directory.CreateDirectory(logDir);
                Console.WriteLine($"Log directory created: {logDir}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to create log directory: {ex.Message}");
            }
        }

        private async void LogPacketToFileAsync(int packetId, Dictionary<string, object> fieldValues, byte[] binaryData)
        {
            try
            {
                EnsureLogDirectory();
                var logFilePath = GetLogFilePath();

                var logEntry = new StringBuilder();
                logEntry.AppendLine($"=== PACKET SENT at {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} ===");
                logEntry.AppendLine($"Packet ID: {packetId}");
                logEntry.AppendLine($"Packet Name: {SelectedPacket?.PacketName ?? "Unknown"}");
                logEntry.AppendLine($"Binary Size: {binaryData.Length} bytes");

                logEntry.AppendLine("Field Values:");
                foreach (var field in fieldValues)
                {
                    var valueStr = FormatFieldValue(field.Value);
                    logEntry.AppendLine($"  {field.Key}: {valueStr}");
                }

                logEntry.AppendLine($"Binary Data (Hex): {BitConverter.ToString(binaryData).Replace("-", " ")}");
                logEntry.AppendLine($"Binary Data (Base64): {Convert.ToBase64String(binaryData)}");
                logEntry.AppendLine();

                await File.AppendAllTextAsync(logFilePath, logEntry.ToString());
                Console.WriteLine($"Log written to file: {logFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to write log to file: {ex.Message}");
                LogErrorToUi($"File log error: {ex.Message}");
            }
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