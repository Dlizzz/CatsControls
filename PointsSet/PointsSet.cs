using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using Windows.ApplicationModel.Resources;

namespace CatsControls
{
    /// <summary>
    /// Root class for points sets. A point sets must derive from this class and implement the IPointsSet interface.
    /// It provides the public properties to manage worker threshold between min / max values
    /// </summary>
    public class PointsSet: DependencyObject
    {
        // Backing store for Threshold public property
        protected int threshold;
        // Get resource loader for the application
        private readonly ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView("ErrorMessages");

        /// <summary>
        /// Create a PointsSet with the worker threshold between minimum and maximum value
        /// </summary>
        /// <param name="minThreshold">Minimum value for the threshold"/></param>
        /// <param name="maxThreshold">Maximum value for the threshold"/></param>
        public PointsSet(int minThreshold, int maxThreshold)
        {
            // Given values must be strictly greater than zero and max must bet greater than min
            if (minThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(minThreshold), resourceLoader.GetString("ValueNotStrictlyPositive"));
            if (maxThreshold <= 0) throw new ArgumentOutOfRangeException(nameof(maxThreshold), resourceLoader.GetString("ValueNotStrictlyPositive"));
            if (maxThreshold <= minThreshold) throw new ArgumentException(resourceLoader.GetString("MaxValueNotGreaterThanMinValue"), nameof(maxThreshold));

            MinThreshold = minThreshold;
            MaxThreshold = maxThreshold;

            // Default threshold is minimum
            threshold = minThreshold;
        }

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
        public int Threshold { get => threshold; }

        #region Resolution dependency property
        // On Resolution chnage callback
        // Throw ArgumentOutOfRange if value is not between 0 and 1
        private static void OnResolutionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PointsSet control = d as PointsSet;

            if ((double)e.NewValue < 0 || (double)e.NewValue > 1) throw new ArgumentOutOfRangeException(nameof(e), control.resourceLoader.GetString("ValueNotStrictlyPositive"));

            double delta = (double)e.NewValue * (control.MaxThreshold - control.MinThreshold);
            control.threshold = control.MinThreshold + Convert.ToInt32(delta);
        }

        /// <summary>
        /// Resolution dependency property identifier
        /// </summary>
        public static readonly DependencyProperty ResolutionProperty =
            DependencyProperty.Register(nameof(Resolution), typeof(double), typeof(PointsSet), new PropertyMetadata(0, OnResolutionChanged));

        /// <summary>
        /// Calculation resolution dependency property. 
        /// </summary>
        /// <value><see cref="double"/> The resolution as a percentage between 0 and 1</value>
        public double Resolution
        {
            get { return (double)GetValue(ResolutionProperty); }
            set { SetValue(ResolutionProperty, value); }
        }
        #endregion
    }
}
