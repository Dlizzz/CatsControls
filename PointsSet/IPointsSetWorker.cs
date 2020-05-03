using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;

namespace CatsControls.PointsSet
{
    public interface IPointsSetWorker
    {
        public int MinThreshold { get; }
        public int MaxThreshold { get; }
        public int Threshold { get; }
        public double Resolution { get; set; }

        public int PointsSetWorker(double ca, double cb);
    }
}
