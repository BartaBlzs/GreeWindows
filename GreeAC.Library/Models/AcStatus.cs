using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GreeAC.Library.Models
{
    public class AcStatus : INotifyPropertyChanged
    {
        private bool _power;
        private int _mode;
        private int _temperature;
        private int _fanSpeed;
        private bool _turbo;
        private bool _quiet;
        private bool _light;
        private bool _health;
        private int _swingVertical;
        private int _swingHorizontal;

        public bool Power
        {
            get => _power;
            set { _power = value; OnPropertyChanged(); }
        }

        public int Mode
        {
            get => _mode;
            set { _mode = value; OnPropertyChanged(); OnPropertyChanged(nameof(ModeString)); }
        }

        public string ModeString => Mode switch
        {
            0 => "Auto",
            1 => "Cool",
            2 => "Dry",
            3 => "Fan",
            4 => "Heat",
            _ => "Unknown"
        };

        public int Temperature
        {
            get => _temperature;
            set { _temperature = value; OnPropertyChanged(); }
        }

        public int FanSpeed
        {
            get => _fanSpeed;
            set { _fanSpeed = value; OnPropertyChanged(); OnPropertyChanged(nameof(FanSpeedString)); }
        }

        public string FanSpeedString => FanSpeed switch
        {
            0 => "Auto",
            1 => "Low",
            2 => "Medium",
            3 => "High",
            _ => "Unknown"
        };

        public bool Turbo
        {
            get => _turbo;
            set { _turbo = value; OnPropertyChanged(); }
        }

        public bool Quiet
        {
            get => _quiet;
            set { _quiet = value; OnPropertyChanged(); }
        }

        public bool Light
        {
            get => _light;
            set { _light = value; OnPropertyChanged(); }
        }

        public bool Health
        {
            get => _health;
            set { _health = value; OnPropertyChanged(); }
        }

        public int SwingVertical
        {
            get => _swingVertical;
            set { _swingVertical = value; OnPropertyChanged(); }
        }

        public int SwingHorizontal
        {
            get => _swingHorizontal;
            set { _swingHorizontal = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}