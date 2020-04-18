using System;
using System.Buffers;
using System.Numerics;
using System.ComponentModel;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Input;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Windows.ApplicationModel.Resources;
using CatsHelpers.ColorMaps;

// Pour en savoir plus sur le modèle d'élément Contrôle utilisateur, consultez la page https://go.microsoft.com/fwlink/?LinkId=234236

namespace CatsControls
{
    public interface IPointsSet
    {
        int PointSetWorker(double ca, double cb);
        int MaxValue { get; set; }
    }

    public sealed partial class PointsSetControl : UserControl, IDisposable
    {
        #region UserControl Initialization
        private static readonly ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView("CatsControls/ErrorMessages");
        private const int MOUSE_WHEEL = 120;
        private const int BYTES_PER_PIXEL = 4;
        private const double wheelMagnifierRatio = 0.1;
        private Point _origin;
        // Scale can't be zero
        private double _scale = 1;
        private double width, height;
        private Point center;
        private Size size;
        private int pointsCount;
        private IPointsSet _pointsSet;

        private ColorMap _colorMap;
        private byte[][] indexedColorMap;

        private readonly ArrayPool<byte> colorArrayPool = ArrayPool<byte>.Shared;
        private byte[] renderPixels;

        private readonly ArrayPool<int> doubleArrayPool = ArrayPool<int>.Shared;
        private int[] renderValues;

        private CanvasRenderTarget renderTarget;

        public PointsSetControl()
        {
            InitializeComponent();
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
            }
        }
        #endregion

        #region UserControl Events
        [Browsable(true)] [Category("Action")]
        [Description("Invoked when PointsSet is ready to draw.")]
        public event TypedEventHandler<CanvasControl, CanvasCreateResourcesEventArgs> CreateResources;
        #endregion

        #region UserControl Methods
        public void SetColorMap(ColorMap colorMap)
        {
            _colorMap = colorMap ?? throw new ArgumentNullException(nameof(colorMap));
            // Add event handler to render the PointsSet when the colormap is inversed
            _colorMap.PropertyChanged += Colormap_PropertyChanged;

            UpdateColorMap();
            Render();
        }

        public void SetWorker(IPointsSet pointsSet)
        {
            _pointsSet = pointsSet ?? throw new ArgumentNullException(nameof(pointsSet));

            UpdateColorMap();
            Calculate();
            Render();
        }
        #endregion

        #region UserControl Logic
        private void Calculate()
        {
            if (Canvas.ReadyToDraw && _pointsSet != null)
            {
                Parallel.For(0, pointsCount, (index) =>
                    {
                        var (ca, cb) = ToComplex(index);
                        renderValues[index] = _pointsSet.PointSetWorker(ca, cb);
                    }
                );
            }
        }

        private void Render()
        {
            if (!Canvas.ReadyToDraw 
                || renderPixels == null 
                || renderValues == null 
                || _pointsSet == null ) return;

            if (_colorMap == null)
            {
                using CanvasDrawingSession drawingSession = renderTarget.CreateDrawingSession();
                drawingSession.Clear(NamedColorMaps.TransparentColor);
            }
            else Parallel.For(0, pointsCount, RenderWorker);

            renderTarget.SetPixelBytes(renderPixels);
        }

        private void RenderWorker(int index)
        {
            if (renderValues[index] == _pointsSet.MaxValue)
            {
                Buffer.BlockCopy(NamedColorMaps.TransparentBytes, 0, renderPixels, index * BYTES_PER_PIXEL, 4);
            }
            else
            {
                Buffer.BlockCopy(indexedColorMap[renderValues[index]], 0, renderPixels, index * BYTES_PER_PIXEL, 4);
            }
        }

        private (double, double) ToComplex(double x, double y) => (_scale * (x - _origin.X), -_scale * (y - _origin.Y));

        private (double, double) ToComplex(int index)
        {
            // Transform bitmap index (per line, left to right) 
            // to bitmap coordinates (x left to right, y top to bottom, origin top left) 
            double y = Math.Truncate(index / width); // Euclidean division
            double x = index - y * width;

            return ToComplex(x, y);
        }

        private void AllocateRenderTarget()
        {
            if (Canvas.ReadyToDraw)
            {
                renderTarget?.Dispose();
                renderTarget = new CanvasRenderTarget(Canvas, size);

                if (renderPixels != null) colorArrayPool.Return(renderPixels);
                renderPixels = colorArrayPool.Rent(pointsCount * BYTES_PER_PIXEL);

                if (renderValues != null) doubleArrayPool.Return(renderValues);
                renderValues = doubleArrayPool.Rent(pointsCount);
            }
        }

        private void UpdateColorMap()
        {
            if (_pointsSet != null && _colorMap != null)
            {
                indexedColorMap = _colorMap.CreateIndexedBytesColorMap(_pointsSet.MaxValue + 1);
            }
        }
        #endregion

        #region Colormap Events
        private void Colormap_PropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(_colorMap.Inversed))
            {
                UpdateColorMap();
                Render();
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
        }

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            args.DrawingSession.DrawImage(renderTarget);
            // Drawing loop -> at 60 fps
            Canvas.Invalidate();
        }

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
        }

        private void Canvas_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs args)
        {
            _origin = new Point(
                _origin.X + args.Delta.Translation.X,
                _origin.Y + args.Delta.Translation.Y);

            Calculate();
            Render();
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
