using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace CatsControls.PointsSet
{
    /// <summary>
    /// Base class for points set calculation parameters
    /// </summary>
    public class PointsSetParameter : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.  
        // The CallerMemberName attribute that is applied to the optional propertyName  
        // parameter causes the property name of the caller to be substituted as an argument.  
        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Double points set calculation parameter
    /// </summary>
    public class PointsSetDoubleParameter : PointsSetParameter
    {
        private double _value;

        public PointsSetDoubleParameter(double min, double max, double defaultValue)
        {
            Maximum = max;
            Minimum = min;
            Default = defaultValue;
            _value = defaultValue;
        }

        public double Maximum { get; }
        public double Minimum { get; }
        public double Default { get; }

        public double Value
        {
            get => _value;
            set
            {
                _value = value;
                NotifyPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Complex points set calculation parameter
    /// </summary>
    public class PointsSetComplexParameter : PointsSetParameter
    {
        private double _real;
        private double _imaginary;

        public PointsSetComplexParameter(Complex min, Complex max, Complex defaultValue)
        {
            Maximum = max;
            Minimum = min;
            Default = defaultValue;
            _real = defaultValue.Real;
            _imaginary = defaultValue.Imaginary;
        }

        public Complex Maximum { get; }
        public Complex Minimum { get; }
        public Complex Default { get; }

        public Complex Value => new Complex(_real, _imaginary);

        public double Real
        {
            get => _real;
            set
            {
                _real = value;
                NotifyPropertyChanged();
            }
        }
        public double Imaginary
        {
            get => _imaginary;
            set
            {
                _imaginary = value;
                NotifyPropertyChanged();
            }
        }
    }
}
