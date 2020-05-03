using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Input;
using Windows.UI.Core;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.UI;
using Microsoft.Graphics.Canvas.UI.Xaml;
using CatsHelpers.ColorMaps;

namespace CatsControls.PointsSet
{
    public class PointsSetRenderedEventArgs: EventArgs
    {
        public double FramesPerSecond { get; set; }
    }

    public sealed partial class PointsSetControl : UserControl
    {
        #region UserControl Initialization
        private const int MOUSE_WHEEL = 120;
        private const double wheelMagnifierRatio = 0.1;

        private static readonly Point defaultOrigin = new Point(400, 400);
        private static readonly double defaultScale = 0.005;

        private readonly FileSavePicker saveImagePicker;
        private readonly ContentDialog saveFileDialog;

        // Calculation
        private IPointsSetWorker _worker;
        // Colormap
        private ColorMap _colorMap;

        // Control render pipeline
        private readonly RenderPipeline renderPipeline;

        public PointsSetControl()
        {
            InitializeComponent();

            // Initialiaze render pipeline
            renderPipeline = new RenderPipeline(Canvas);
            // Hook to rendering events
            renderPipeline.StartRendering += RenderPipeline_StartRendering; ;
            renderPipeline.Rendered += RenderPipeline_Rendered;

            // Saved image file picker
            saveImagePicker = new FileSavePicker
            {
                SuggestedStartLocation = PickerLocationId.PicturesLibrary,
                SuggestedFileName = "New Image"
            };
            // Dropdown of file types the user can save the file as
            saveImagePicker.FileTypeChoices.Add("Image Png", new List<string>() { ".png" });
            saveImagePicker.FileTypeChoices.Add("Image Jpeg", new List<string>() { ".jpg", ".jpeg" });
            saveImagePicker.FileTypeChoices.Add("Image Bmp", new List<string>() { ".bmp" });
            saveImagePicker.FileTypeChoices.Add("Image Gif", new List<string>() { ".gif" });
            saveImagePicker.FileTypeChoices.Add("Image Tiff", new List<string>() { ".tif", ".tiff" });
            saveImagePicker.FileTypeChoices.Add("Image Jpeg XR", new List<string>() { ".jxr" });

            // Image file save error dialog
            saveFileDialog = new ContentDialog { CloseButtonText = "Ok" };
        }
        #endregion

        #region  Properties
        public bool RenderEnabled
        {
            get => renderPipeline.IsEnabled;
            set => renderPipeline.IsEnabled = value;
        }
        #endregion

        #region Origin dependency property
        private static void OnOriginChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PointsSetControl control = d as PointsSetControl;

            // Need to check that we have a real new value as Point is a structure
            if((Point)e.NewValue != (Point)e.OldValue) control.renderPipeline.Origin = (Point)e.NewValue;
        }
                
        public static readonly DependencyProperty OriginProperty = 
            DependencyProperty.Register(nameof(Origin), typeof(Point), typeof(PointsSetControl), new PropertyMetadata(defaultOrigin, OnOriginChanged));

        [Browsable(true)] [Category("Appearance")]
        [Description("Complex plan transformation origin (control coordinates).")]
        public Point Origin
        {
            get { return (Point)GetValue(OriginProperty); } 
            set { SetValue(OriginProperty, value); }
        }
        #endregion

        #region ScaleFactor dependency property
        private static void OnScaleFactorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PointsSetControl control = d as PointsSetControl;

            control.renderPipeline.Scale = (double)e.NewValue;
        }

        public static readonly DependencyProperty ScaleFactorProperty =
            DependencyProperty.Register(nameof(ScaleFactor), typeof(double), typeof(PointsSetControl), new PropertyMetadata(defaultScale, OnScaleFactorChanged));

        [Browsable(true)] [Category("Appearance")]
        [Description("Complex plan transformation scale.")]
        public double ScaleFactor
        {
            get { return (double)GetValue(ScaleFactorProperty); }
            set { SetValue(ScaleFactorProperty, value); }
        }
        #endregion

        #region Resolution dependency property
        private static void OnResolutionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            PointsSetControl control = d as PointsSetControl;

            if (control._worker != null) control._worker.Resolution = (double)e.NewValue / 100.0;
        }

        public static readonly DependencyProperty ResolutionProperty =
            DependencyProperty.Register(nameof(Resolution), typeof(double), typeof(PointsSetControl), new PropertyMetadata(0, OnResolutionChanged));

        [Browsable(true)] [Category("Behavior")]
        [Description("Calculation resolution from 0 (minimum resolution) to 100 (maximum resolution).")]
        public double Resolution
        {
            get { return Convert.ToDouble(GetValue(ResolutionProperty)); }
            set { SetValue(ResolutionProperty, value); }
        }
        #endregion

        #region Colormap write only property
        public void SetColorMap(ColorMap colorMap)
        {
            _colorMap = colorMap ?? throw new ArgumentNullException(nameof(colorMap));

            // Hook to ColorMap Inversed property changed event
            _colorMap.PropertyChanged += ColorMapPropertiesChangedEventHandler;

            renderPipeline.ColorMap = _colorMap;
        }

        private void ColorMapPropertiesChangedEventHandler(object sender, PropertyChangedEventArgs args)
        {
            if (args.PropertyName == "Inversed") renderPipeline.ColorMap = _colorMap;
        }
        #endregion

        #region Worker write only property
        public void SetWorker(IPointsSetWorker pointsSet)
        {
            _worker = pointsSet ?? throw new ArgumentNullException(nameof(pointsSet));

            // Hook to Worker Resolution property changed event
            ((PointsSetWorker)_worker).PropertyChanged += WorkerPropertiesChangedEventHandler;

            // Hook to each worker parameter property chnaged event
            foreach (KeyValuePair<string, PointsSetParameter> parameter in ((PointsSetWorker)_worker).Parameters)
            {
                parameter.Value.PropertyChanged += WorkerPropertiesChangedEventHandler;
            }

            renderPipeline.Worker = _worker;
        }

        private void WorkerPropertiesChangedEventHandler(object sender, PropertyChangedEventArgs args) => renderPipeline.Worker = _worker;
        #endregion

        #region Control Methods
        public async void SaveImageFileAsync()
        {
            StorageFile file = await saveImagePicker.PickSaveFileAsync();
            if (file != null)
            {
                var fileFormat = file.FileType switch
                {
                    "Image Png" => CanvasBitmapFileFormat.Png,
                    "Image Jpeg" => CanvasBitmapFileFormat.Jpeg,
                    "Image Bmp" => CanvasBitmapFileFormat.Bmp,
                    "Image Gif" => CanvasBitmapFileFormat.Gif,
                    "Image Tiff" => CanvasBitmapFileFormat.Tiff,
                    "Image Jpeg XR" => CanvasBitmapFileFormat.JpegXR,
                    _ => CanvasBitmapFileFormat.Png
                };
                // Prevent updates to the remote version of the file until
                // we finish making changes and call CompleteUpdatesAsync.
                CachedFileManager.DeferUpdates(file);
                // write to file
                using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                {
                    // Pass a stream to the control to stay in the security context of the FilePicker
                    await renderPipeline.SaveImageAsync(stream, CanvasBitmapFileFormat.Png).ConfigureAwait(false);
                }
                // Let Windows know that we're finished changing the file so
                // the other app can update the remote version of the file.
                // Completing updates may require Windows to ask for user input.
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status == FileUpdateStatus.Complete)
                {
                    // We are not in the UI thread
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                        saveFileDialog.Title = "Save Image Success";
                        saveFileDialog.Content = "Image was saved in " + file.Name;
                        await saveFileDialog.ShowAsync();
                    });
                }
                else
                {
                    // We are not in the UI thread
                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => {
                        saveFileDialog.Title = "Save Image Error";
                        saveFileDialog.Content = "Unable to save the image in " + file.Name + " Check destination and filename, and try again.";
                        await saveFileDialog.ShowAsync();
                    });
                }
            }
        }

        public PointValues GetValues(Point point)
        {
            PointValues values = renderPipeline.GetValues(point);

            values.PointValue /= _worker.Threshold;

            return values;
        }

        public void Reset()
        {
            renderPipeline.IsEnabled = false;
            Origin = new Point(PointsSet.ActualWidth / 2, PointsSet.ActualHeight / 2);
            ScaleFactor = 0.005;
            Resolution = 0;
            renderPipeline.IsEnabled = true;
        }
        #endregion

        #region Control Events
        [Browsable(true)] [Category("Action")]
        [Description("Invoked when PointsSet is ready to draw.")]
        // OnCreateResource method is provided by the Canvas. We propagate its event to the control  
        public event TypedEventHandler<CanvasControl, CanvasCreateResourcesEventArgs> CreateResources;

        [Browsable(true)]
        [Category("Action")]
        [Description("Invoked when PointsSet start rendering.")]
        // Raised by RenderPipeline
        public event EventHandler StartRendering;

        [Browsable(true)]
        [Category("Action")]
        [Description("Invoked when PointsSet is rendered.")]
        // Raised by RenderPipeline
        public event EventHandler<RenderEventArgs> Rendered;
        #endregion

        #region Pipeline Events
        // Propagate the pipeline events at control level
        private void RenderPipeline_StartRendering(object sender, EventArgs e) => StartRendering?.Invoke(this, e);
        private void RenderPipeline_Rendered(object sender, RenderEventArgs e) => Rendered?.Invoke(this, e);
        #endregion

        #region Canvas Events
        private void Canvas_CreateResources(CanvasControl sender, CanvasCreateResourcesEventArgs args)
        {
            if (args.Reason == CanvasCreateResourcesReason.FirstTime) Origin = new Point(sender.ActualWidth / 2, sender.ActualHeight / 2);
            
            // Propagate event to control's event.
            CreateResources?.Invoke(sender, args);
        }

        private void Canvas_SizeChanged(object sender, SizeChangedEventArgs args) => renderPipeline.Canvas = (CanvasControl)sender;

        private void Canvas_DoubleTapped(object sender, DoubleTappedRoutedEventArgs args)
        {
            Point clickedPoint = args.GetPosition((CanvasControl)sender);
            double width = ((CanvasControl)sender).ActualWidth;
            double height = ((CanvasControl)sender).ActualHeight;

            Origin = new Point(Origin.X + width / 2 - clickedPoint.X, Origin.Y + height / 2 - clickedPoint.Y);
        }

        private void Canvas_ManipulationDelta(object sender, ManipulationDeltaRoutedEventArgs args)
        {
            Origin = new Point(Origin.X + args.Delta.Translation.X, Origin.Y + args.Delta.Translation.Y);
        }

        private void Canvas_PointerWheelChanged(object sender, PointerRoutedEventArgs args)
        {
            PointerPoint pointerPoint = args.GetCurrentPoint((CanvasControl)sender);

            double magnifier = pointerPoint.Properties.MouseWheelDelta > 0 ? 1 - wheelMagnifierRatio : 1 + wheelMagnifierRatio;
            int magnifierPower = Math.Abs(pointerPoint.Properties.MouseWheelDelta) / MOUSE_WHEEL;
            for (int i = 2; i <= magnifierPower; i++) magnifier *= magnifier;

            // Transalte the origin to have the complex at the center of the canevas staying at the center
            double newScale = ScaleFactor * magnifier;
            Point newOrigin = new Point(
                (newScale - ScaleFactor) / newScale * pointerPoint.Position.X + ScaleFactor / newScale * Origin.X,
                (newScale - ScaleFactor) / newScale * pointerPoint.Position.Y + ScaleFactor / newScale * Origin.Y);

            renderPipeline.IsEnabled = false;
            Origin = newOrigin;
            ScaleFactor = newScale;
            renderPipeline.IsEnabled = true;
        }
        #endregion
    }
}
