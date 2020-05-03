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

        public PointsSetDoubleParameter(double minValue, double maxValue)
        {
            MaxValue = maxValue;
            MinValue = minValue;
        }

        public double MaxValue { get; }
        public double MinValue { get; }

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
        private double _realValue;
        private double _imaginaryValue;

        public PointsSetComplexParameter(Complex minValue, Complex maxValue)
        {
            MaxValue = maxValue;
            MinValue = minValue;
        }

        public Complex MaxValue { get; }
        public Complex MinValue { get; }

        public Complex Value
        {
            get => new Complex(_realValue, _imaginaryValue);
        }

        public double RealValue
        {
            get => _realValue;
            set
            {
                _realValue = value;
                NotifyPropertyChanged();
            }
        }
        public double ImaginaryValue
        {
            get => _imaginaryValue;
            set
            {
                _imaginaryValue = value;
                NotifyPropertyChanged();
            }
        }
    }
}
