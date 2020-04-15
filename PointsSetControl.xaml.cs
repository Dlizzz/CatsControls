using System;
using System.Buffers;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.ApplicationModel.Resources;
using CatsHelpers.ColorMaps;
using System.ComponentModel;

// Pour en savoir plus sur le modèle d'élément Contrôle utilisateur, consultez la page https://go.microsoft.com/fwlink/?LinkId=234236

namespace CatsControls
{
    public interface IPointsSet
    {
        double PointSetWorker(Complex c);
    }

    public sealed partial class PointsSetControl : UserControl, IDisposable
    {
        private static readonly ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView("CatsControls/ErrorMessages");
        private const int MOUSE_WHEEL = 120;
        private const double wheelMagnifierRatio = 0.1;

        #region UserControl Initialization
        private Point _origin;
        private double _scale;
        private double width, height;
        private Point center;
        private Size size;
        private int pointsCount;
        private ColorMap _colorScale;
        private IPointsSet _pointsSet;

        private readonly ArrayPool<Color> colorArrayPool;
        private Color[] renderPixels;

        private readonly ArrayPool<double> doubleArrayPool;
        private double[] renderValues;

        private CanvasRenderTarget renderTarget;

        public PointsSetControl()
        {
            InitializeComponent();

            colorArrayPool = ArrayPool<Color>.Shared;
            doubleArrayPool = ArrayPool<double>.Shared;

            // Scale can't be zero
            _scale = 1;
        }
        #endregion
          
        #region UserControl Properties
        [Browsable(false)]
        public Point Origin
        {
            get => _origin;
            set
            {
                _origin = value;

                Calculate();
                Render();
                Canvas.Invalidate();
            }
        }

        [Browsable(true)] [Category("Behavior")]
        [Description("Define scale factor for the complex plan.")]
        public double ScaleFactor
        {
            get => _scale;
            set
            {
                if (value == 0) throw new ArgumentOutOfRangeException(nameof(ScaleFactor), resourceLoader.GetString("ValueNotZero"));
                _scale = value;

                Calculate();
                Render();
                Canvas.Invalidate();
            }
        }
        #endregion

        #region UserControl Events
        [Browsable(true)] [Category("Action")]
        [Description("Invoked when PointsSet is ready to draw.")]
        public event TypedEventHandler<CanvasControl, CanvasCreateResourcesEventArgs> CreateResources;
        #endregion

        #region UserControl Methods
        public void SetColorScale(ColorMap colorScale)
        {
            _colorScale = colorScale ?? throw new ArgumentNullException(nameof(colorScale));

            Render();
            Canvas.Invalidate();
        }

        public void SetWorker(IPointsSet pointsSet)
        {
            _pointsSet = pointsSet ?? throw new ArgumentNullException(nameof(pointsSet));

            Calculate();
            Render();
            Canvas.Invalidate();
        }
        #endregion

        #region UserControl Logic
        private void Calculate()
        {
            if (Canvas.ReadyToDraw && _pointsSet != null) Parallel.For(0, pointsCount, Worker);
        }

        private void Worker(int index)
        {
            Complex c = ToComplex(index);

            renderValues[index] = _pointsSet.PointSetWorker(c);
        }

        private void Render()
        {
            if (!Canvas.ReadyToDraw || renderPixels == null || renderValues == null) return;

            if (_colorScale == null)
            {
                Parallel.For(0, pointsCount, (index) => renderPixels[index] = ColorsCollection.Transparent);
            }
            else
            {
                Parallel.For(0, pointsCount, (index) =>
                    renderPixels[index] = (renderValues[index] == 0)
                    ? ColorsCollection.Transparent
                    : _colorScale.GetInterpolatedColor(renderValues[index]));
            }

            renderTarget.SetPixelColors(renderPixels);
        }

        private Complex ToComplex(Vector2 point) => new Complex(_scale * (point.X - _origin.X), -_scale * (point.Y - _origin.Y));

        private Complex ToComplex(int index)
        {
            // Transform bitmap index (per line, left to right) 
            // to bitmap coordinates (x left to right, y top to bottom, origin top left) 
            int lineCount = index / Convert.ToInt32(width); // Euclidean division

            float x = Convert.ToSingle(index - lineCount * width);
            float y = Convert.ToSingle(lineCount);

            return ToComplex(new Vector2(x, y));
        }

        private void AllocateRenderTarget()
        {
            if (Canvas.ReadyToDraw)
            {
                renderTarget?.Dispose();
                renderTarget = new CanvasRenderTarget(Canvas, size);

                if (renderPixels != null) colorArrayPool.Return(renderPixels);
                renderPixels = colorArrayPool.Rent(pointsCount);

                if (renderValues != null) doubleArrayPool.Return(renderValues);
                renderValues = doubleArrayPool.Rent(pointsCount);
            }
        }
        #endregion

        #region Canvas Events
        private void Canvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            width = sender.ActualWidth;
            height = sender.ActualHeight;
            size = sender.ActualSize.ToSize();
            pointsCount = Convert.ToInt32(width * height);
            center = new Point(width / 2, height / 2);           
            if (args.Reason == CanvasCreateResourcesReason.FirstTime) _origin = center;

            AllocateRenderTarget();

            // Call PointsSet CreateResources event before entering the pipeline
            CreateResources?.Invoke(sender, args);
            Calculate();
            Render();
            Canvas.Invalidate();
        }

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args) => args.DrawingSession.DrawImage(renderTarget);

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs args)
        {
            if (Canvas.ReadyToDraw)
            {
                width = args.NewSize.Width;
                height = args.NewSize.Height;
                size = args.NewSize;
                pointsCount = Convert.ToInt32(width * height);
                center = new Point(width / 2, height / 2);

                AllocateRenderTarget();
                Calculate();
                Render();
                Canvas.Invalidate();
            }
        }

        private void Canvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
        {
            Point clickedPoint = args.GetPosition(Canvas);

            _origin = new Point(
                _origin.X + width / 2 - clickedPoint.X,
                _origin.Y + height / 2 - clickedPoint.Y);

            Calculate();
            Render();
            Canvas.Invalidate();
        }

        private void Canvas_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs args)
        {
            _origin = new Point(
                _origin.X + args.Delta.Translation.X,
                _origin.Y + args.Delta.Translation.Y);

            Calculate();
            Render();
            Canvas.Invalidate();
        }

        private void Canvas_PointerWheelChanged(object sender, PointerRoutedEventArgs args)
        {
            PointerPoint pointerPoint = args.GetCurrentPoint(Canvas);

            int magnifierPower = Math.Abs(pointerPoint.Properties.MouseWheelDelta) / MOUSE_WHEEL;
            double magnifier = pointerPoint.Properties.MouseWheelDelta > 0 ? 1 - wheelMagnifierRatio : 1 + wheelMagnifierRatio;
            for (int i = 2; i <= magnifierPower; i++) magnifier *= magnifier;

            // Transalte the origin to have the complex at the center of the canevas staying at the center
            double newScale = _scale * magnifier;
            _origin = new Point(
                (newScale - _scale) / newScale * pointerPoint.Position.X + _scale / newScale * _origin.X,
                (newScale - _scale) / newScale * pointerPoint.Position.Y + _scale / newScale * _origin.Y);
            _scale = newScale;

            Calculate();
            Render();
            Canvas.Invalidate();
        }
        #endregion

        #region IDisposable Support
        private bool disposedValue = false; // Pour détecter les appels redondants

        void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    renderTarget?.Dispose();
                }

                if (renderPixels != null) colorArrayPool.Return(renderPixels);
                if (renderValues != null) doubleArrayPool.Return(renderValues);

                disposedValue = true;
            }
        }

        ~PointsSetControl()
        {
            // Ne modifiez pas ce code. Placez le code de nettoyage dans Dispose(bool disposing) ci-dessus.
            Dispose(false);
        }

        // Ce code est ajouté pour implémenter correctement le modèle supprimable.
        public void Dispose()
        {
            // Ne modifiez pas ce code. Placez le code de nettoyage dans Dispose(bool disposing) ci-dessus.
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
