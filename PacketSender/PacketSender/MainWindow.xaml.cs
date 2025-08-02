using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Collections.ObjectModel;
using PacketSender.PacketLoader;
using System.Windows.Threading;

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

        public MainWindow()
        {
            InitializeComponent();
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