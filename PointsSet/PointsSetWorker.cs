using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.ApplicationModel.Resources;

namespace CatsControls.PointsSet
{
    /// <summary>
    /// Root class for points sets. A point sets must derive from this class and implement the IPointsSet interface.
    /// It provides the public properties to manage worker threshold between min / max values
    /// </summary>
    public class PointsSetWorker : INotifyPropertyChanged
    {
        // Get resource loader for the application
        private readonly ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView("ErrorMessages");
        // Backing store for Threshold public property
        protected int _threshold;
        // Backing store for Resolution property
        private double _resolution;
        // backing store for the Parameters list property
        protected Dictionary<string, PointsSetParameter> _parameters;

        /// <summary>
        /// Create a PointsSet with the worker threshold between minimum and maximum value
        /// </summary>
        /// <param name="minThreshold">Minimum value for the threshold"/></param>
        /// <param name="maxThreshold">Maximum value for the threshold"/></param>
        public PointsSetWorker(int minThreshold, int maxThreshold)
        {
            // Given values must be strictly greater than zero and max must bet greater than min
            if (minThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(minThreshold), resourceLoader.GetString("ValueNotStrictlyPositive"));
            if (maxThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(maxThreshold), resourceLoader.GetString("ValueNotStrictlyPositive"));
            if (maxThreshold <= minThreshold) throw new ArgumentException(resourceLoader.GetString("MaxValueNotGreaterThanMinValue"), nameof(maxThreshold));

            MinThreshold = minThreshold;
            MaxThreshold = maxThreshold;

            // Default threshold is minimum
            _threshold = minThreshold;

            // Create parameters list
            _parameters = new Dictionary<string, PointsSetParameter>();
        }

        #region Properties
        /// <summary>
        /// Minimum value for the threshold/>
        /// </summary>
        /// <value><see cref="int"/> The value</value>
        public int MinThreshold { get; }
        /// <summary>
        /// Minimum value for the threshold/>
        /// </summary>
        /// <value><see cref="int"/> The value</value>
        public int MaxThreshold { get; }
        /// <summary>
        /// The current threshold/>
        /// </summary>
        /// <value><see cref="int"/> The value</value>
        public int Threshold => _threshold; 

        /// <summary>
        /// Calculation resolution property. 
        /// </summary>
        /// <value><see cref="double"/> The resolution as a percentage between 0 and 1</value>
        public double Resolution
        {
            get => _resolution;
            set 
            {
                if (value == _resolution) return;
                if (value < 0 || value > 1) throw new ArgumentOutOfRangeException(nameof(Resolution), resourceLoader.GetString("ValueNotStrictlyPositive"));
                _resolution = value;

                // Update threshold from new resolution
                double delta = value * (MaxThreshold - MinThreshold);
                _threshold = MinThreshold + Convert.ToInt32(delta);
                
                NotifyPropertyChanged();
            }
        }

        /// <summary>
        /// Dictionnary of calculation parameters
        /// </summary>
        /// <remarks>Read only dictionnary</remarks>
        public IReadOnlyDictionary<string, PointsSetParameter> Parameters => _parameters; 
        #endregion

        #region Events
        /// <summary>
        /// Property changed event
        /// </summary>
        /// <remarks>Implemented for Resolution property</remarks>
        public event PropertyChangedEventHandler PropertyChanged;

        // This method is called by the Set accessor of each property.  
        // The CallerMemberName attribute that is applied to the optional propertyName  
        // parameter causes the property name of the caller to be substituted as an argument.  
        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
