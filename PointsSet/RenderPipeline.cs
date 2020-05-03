using System;
using System.Buffers;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;
using Windows.ApplicationModel.Resources;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using CatsHelpers.ColorMaps;

namespace CatsControls.PointsSet
{
    /// <summary>
    /// Structure used to get values from the current set of points
    /// </summary>
    public struct PointValues
    {
        // Real and imaginary parts of the point in the complex plan
        public double PointReal;
        public double PointImaginary;
        // Calculated value for the point
        public double PointValue;
    }

    public class RenderEventArgs : EventArgs
    {
        public double FramesPerSecond { get; set; }
    }

    class RenderPipeline : IDisposable
    {
        #region Initialization
       
        private const int BYTES_PER_PIXEL = 4;
        private static readonly ResourceLoader resourceLoader = ResourceLoader.GetForCurrentView("CatsControls/ErrorMessages");

        // Rendering surface
        private CanvasRenderTarget renderTarget;
        private readonly ArrayPool<byte> colorArrayPool = ArrayPool<byte>.Shared;
        private byte[] renderPixels;
        private readonly ArrayPool<int> doubleArrayPool = ArrayPool<int>.Shared;
        private int[] renderValues;

        // Calculation
        private IPointsSetWorker _worker;

        // Colormap
        private ColorMap _colorMap;
        private byte[][] indexedColorMap;

        // Geometry
        private CanvasControl _canvas;
        private double width, height;
        private int pointsCount;

        // Transform
        private Point _origin;
        private double _scale;

        // Counters
        private readonly Stopwatch pipelineWatch;

        // Events
        private readonly RenderEventArgs renderedEventArgs;

        // Process
        private enum PipelineTrigger { Geometry, Transformation, ColorMap, Calculation, All };
        private bool _isEnabled = true;

        public RenderPipeline(CanvasControl canvas)
        {
            _canvas = canvas;
            renderedEventArgs = new RenderEventArgs();

            // Watch for execution time counter
            pipelineWatch = new Stopwatch();

            // Hook to canvas create resource event to initialize pipeline when canvas is ready
            _canvas.CreateResources += Canvas_CreateResources;
        }
        #endregion

        #region Pipeline properties
        public IPointsSetWorker Worker
        {
            get => _worker;
            set
            {
                _worker = value ?? throw new ArgumentNullException(nameof(Worker));
                RunPipeline(PipelineTrigger.Calculation);
            }
        }

        public ColorMap ColorMap
        {
            get => _colorMap;
            set
            {
                _colorMap = value ?? throw new ArgumentNullException(nameof(ColorMap));
                RunPipeline(PipelineTrigger.ColorMap);
            }
        }

        public Point Origin
        {
            get => _origin;
            set
            {
                _origin = value;
                RunPipeline(PipelineTrigger.Transformation);
            }
        }

        public double Scale
        {
            get => _scale;
            set
            {
                if (value == 0) throw new ArgumentOutOfRangeException(nameof(Scale), resourceLoader.GetString("ValueNotZero"));

                _scale = value;
                RunPipeline(PipelineTrigger.Transformation);
            }
        }

        public CanvasControl Canvas
        {
            get => _canvas;
            set
            {
                _canvas = value ?? throw new ArgumentNullException(nameof(Canvas));

                width = _canvas.ActualSize.ToSize().Width;
                height = _canvas.ActualSize.ToSize().Height;
                pointsCount = Convert.ToInt32(width * height);
                RunPipeline(PipelineTrigger.Geometry);
            }
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                if (_isEnabled) RunPipeline(PipelineTrigger.All);
            }
        }
        #endregion

        #region Pipeline methods
        public PointValues GetValues(Point point)
        {
            PointValues values = new PointValues();

            (values.PointReal, values.PointImaginary) = ToComplex(point.X, point.Y);
            
            values.PointValue = renderValues[ToIndex(point)];

            return values;
        }

        public async Task SaveImageAsync(IRandomAccessStream stream, CanvasBitmapFileFormat fileFormat)
        {
            await renderTarget.SaveAsync(stream, fileFormat);
        }
        #endregion

        #region Pipeline events
        public event EventHandler StartRendering;

        public event EventHandler<RenderEventArgs> Rendered;
        #endregion

        #region Canvas events handlers
        private void Canvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            // Initialize geometry
            width = _canvas.ActualSize.ToSize().Width;
            height = _canvas.ActualSize.ToSize().Height;
            pointsCount = Convert.ToInt32(width * height);

            // Updated geometry pipeline
            pipelineWatch.Restart();
            AllocateRenderTarget();
            Calculate();
            Render();
            pipelineWatch.Stop();

            // Hook to draw event as renderTarget is ready
            _canvas.Draw += Canvas_Draw;
        }

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            // Be sure to have a valid renderTarget before hooking the handler
            args.DrawingSession.DrawImage(renderTarget);
            // Drawing loop -> at 60 fps
            _canvas.Invalidate();
        }
        #endregion

        #region Pipeline processes
        private void RunPipeline(PipelineTrigger trigger)
        {
            if (!_isEnabled) return;

            StartRendering?.Invoke(this, EventArgs.Empty);
            pipelineWatch.Restart();

            switch (trigger)
            {
                case PipelineTrigger.Geometry:
                    AllocateRenderTarget();
                    Calculate();
                    break;
                case PipelineTrigger.Transformation:
                    Calculate();
                    break;
                case PipelineTrigger.ColorMap:
                    AllocateColorMap();
                    break;
                case PipelineTrigger.Calculation:
                    AllocateColorMap();
                    Calculate();
                    break;
                case PipelineTrigger.All:
                    AllocateRenderTarget();
                    AllocateColorMap();
                    Calculate();
                    break;
            }
            Render();

            pipelineWatch.Stop();
            if (Rendered != null)
            {
                renderedEventArgs.FramesPerSecond = 1 / pipelineWatch.Elapsed.TotalSeconds;
                Rendered.Invoke(this, renderedEventArgs);
            }
        }

        private void AllocateRenderTarget()
        {
            // Make sure that we have a device attached to the Canvas
            if (_canvas.ReadyToDraw)
            {
                renderTarget?.Dispose();
                renderTarget = new CanvasRenderTarget(_canvas, _canvas.ActualSize.ToSize());

                if (renderPixels != null) colorArrayPool.Return(renderPixels);
                renderPixels = colorArrayPool.Rent(pointsCount * BYTES_PER_PIXEL);

                if (renderValues != null) doubleArrayPool.Return(renderValues);
                renderValues = doubleArrayPool.Rent(pointsCount);
            }
        }

        private void AllocateColorMap()
        {
            // Make sure that wa have all the needed depndencies
            if (_worker == null || _colorMap == null) return;

            indexedColorMap = _colorMap.CreateIndexedBytesColorMap(_worker.Threshold + 1);
        }
        
        private void Calculate()
        {
            // Make sure that wa have all the needed dependencies
            if (_worker == null || renderTarget == null) return;

            Parallel.For(0, pointsCount, (index) =>
                {
                    var (ca, cb) = ToComplex(index);
                    renderValues[index] = _worker.PointsSetWorker(ca, cb);
                }
            );
        }

        private void Render()
        {
            // Make sure that wa have all the needed depndencies
            if (_worker == null || renderTarget == null || _colorMap == null) return;

            Parallel.For(0, pointsCount, (index) =>
                {
                    if (renderValues[index] == _worker.Threshold)
                    {
                        System.Buffer.BlockCopy(NamedColorMaps.TransparentBytes, 0, renderPixels, index * BYTES_PER_PIXEL, 4);
                    }
                    else
                    {
                        System.Buffer.BlockCopy(indexedColorMap[renderValues[index]], 0, renderPixels, index * BYTES_PER_PIXEL, 4);
                    }
                }
            );

            renderTarget.SetPixelBytes(renderPixels);
        }
        #endregion

        #region Conversion functions
        private (double, double) ToComplex(double x, double y) => (_scale * (x - _origin.X), -_scale * (y - _origin.Y));

        private (double, double) ToComplex(int index)
        {
            // Transform bitmap index (per line, left to right) 
            // to bitmap coordinates (x left to right, y top to bottom, origin top left) 
            double y = Math.Truncate(index / width); // Euclidean division
            double x = index - y * width;

            return ToComplex(x, y);
        }

        private int ToIndex(Point point) => Convert.ToInt32(point.X + point.Y * width);
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

        ~RenderPipeline()
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
