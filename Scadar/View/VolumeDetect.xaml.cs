using AForge.Video.DirectShow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Scadar.Material;
using System.ComponentModel;
using System.Drawing;
using Scadar.Model;
using System.Threading;
using AForge.Video;
using AForge.Imaging.Filters;
using AForge;
using AForge.Imaging;
using AForge.Math.Geometry;

namespace Scadar.View
{
    /// <summary>
    /// Interaction logic for VolumeDetect.xaml
    /// </summary>
    public partial class VolumeDetect : UserControl , INotifyPropertyChanged
    {

        #region Define Variable
        private FilterInfo _currentDevice;
        private bool _Original = true;
        private bool _Grayscaled = false;
        private bool _inverted;
        private bool _pickingColor;
        private bool _colorFiltered;
        private int _green;
        private int _red;
        private int _blue;
        private short _radius;
        #endregion


        #region Public properties

        public ObservableCollection<FilterInfo> VideoDevices { get; set; }

        public FilterInfo CurrentDevice
        {
            get { return _currentDevice; }
            set { _currentDevice = value; OnPropertyChanged("CurrentDevice"); }
        }
        public bool Original
        {
            get { return _Original; }
            set { _Original = value; OnPropertyChanged("Original"); }
        }
        public bool Grayscaled
        {
            get { return _Grayscaled; }
            set { _Grayscaled = value; OnPropertyChanged("Grayscaled"); }
        }
        public int Red
        {
            get { return _red; }
            set { _red = value; this.OnPropertyChanged("Red"); }
        }
       
        public int Blue
        {
            get { return _blue; }
            set { _blue = value; this.OnPropertyChanged("Blue"); }
        }
        public int Green
        {
            get { return _green; }
            set { _green = value; this.OnPropertyChanged("Green"); }
        }
        public short Radius
        {
            get { return _radius; }
            set { _radius = value; this.OnPropertyChanged("Radius"); }
        }
        public bool Inverted
        {
            get { return _inverted; }
            set { _inverted = value; this.OnPropertyChanged("Inverted"); }
        }
        public bool PickingColor
        {
            get { return _pickingColor; }
            set { _pickingColor = value; this.OnPropertyChanged("PickingColor"); }
        }
        public bool ColorFiltered
        {
            get { return _colorFiltered; }
            set { _colorFiltered = value; this.OnPropertyChanged("ColorFiltered"); }
        }
        






        #endregion
        private IVideoSource _videoSource;


        public VolumeDetect()
        {
            InitializeComponent();
            this.DataContext = this;
            GetVideoDevices();
            //this.Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            StopCamera();
        }

        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            StartCamera();
        }


        
        private void video_NewFrame(object sender, AForge.Video.NewFrameEventArgs eventArgs)
        {
            try
            {
                BitmapImage bi;
                using (var bitmap = (Bitmap)eventArgs.Frame.Clone())
                {
                    if (ColorFiltered)
                    {
                        new EuclideanColorFiltering(new AForge.Imaging.RGB((byte)Red, (byte)Green, (byte)Blue), Radius).ApplyInPlace(bitmap);
                    }
                    if (Grayscaled)
                    {
                        using (var grayscaledBitmap = Grayscale.CommonAlgorithms.BT709.Apply(bitmap))
                        {
                            bi = grayscaledBitmap.ToBitmapImage();
                        }
                    }
                    else // original
                    {
                        var corners = FindCorners(bitmap);
                        if (corners.Any())
                        {
                            PaintCorners(corners, bitmap);
                        }
                        bi = bitmap.ToBitmapImage();
                    }
                }
                bi.Freeze(); // avoid cross thread operations and prevents leaks
                Dispatcher.BeginInvoke(new ThreadStart(delegate { videoPlayer.Source = bi; }));
            }
            catch (Exception exc)
            {
                MessageBox.Show("Error on _videoSource_NewFrame:\n" + exc.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StopCamera();
            }
        }

        #region Prcess method 
        private List<IntPoint> FindCorners(Bitmap bitmap)
        {
            List<IntPoint> corners = new List<IntPoint>();
            using (var clone = bitmap.Clone() as Bitmap)
            {
                new EuclideanColorFiltering(new AForge.Imaging.RGB((byte)Red, (byte)Green, (byte)Blue), Radius).ApplyInPlace(clone);
                using (var grayscaledBitmap = Grayscale.CommonAlgorithms.BT709.Apply(clone))
                {
                    //new Threshold(Threshold).ApplyInPlace(grayscaledBitmap);
                    if (Inverted)
                    {
                        new Invert().ApplyInPlace(grayscaledBitmap);
                    }
                    BlobCounter blobCounter = new BlobCounter();
                    blobCounter.FilterBlobs = true;
                    blobCounter.MinWidth = 50;
                    blobCounter.MinHeight = 50;
                    blobCounter.ObjectsOrder = ObjectsOrder.Size;
                    blobCounter.ProcessImage(grayscaledBitmap);
                    Blob[] blobs = blobCounter.GetObjectsInformation();        
                    GrahamConvexHull hullFinder = new GrahamConvexHull();
                    for (int i = 0, n = blobs.Length; i < n; i++)
                    {
                        List<IntPoint> leftPoints, rightPoints;
                        List<IntPoint> edgePoints = new List<IntPoint>();
                        blobCounter.GetBlobsLeftAndRightEdges(blobs[i], out leftPoints, out rightPoints);
                        edgePoints.AddRange(leftPoints);
                        edgePoints.AddRange(rightPoints);
                        corners = hullFinder.FindHull(edgePoints);
                    }
                }
            }
            return corners;
        }

        private void PaintCorners(List<IntPoint> corners, Bitmap bitmap)
        {
            using (Graphics g = Graphics.FromImage(bitmap))
            using (System.Drawing.Pen bluePen = new System.Drawing.Pen(System.Drawing.Color.Blue, 2))
            {
                g.DrawPolygon(bluePen, ToPointsArray(corners));
            }
        }
        #endregion

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            StopCamera();
        }

        private void GetVideoDevices()
        {
            VideoDevices = new ObservableCollection<FilterInfo>();
            foreach (FilterInfo filterInfo in new FilterInfoCollection(FilterCategory.VideoInputDevice))
            {
                VideoDevices.Add(filterInfo);
            }
            if (VideoDevices.Any())
            {
                CurrentDevice = VideoDevices[0];
            }
            else
            {
                MessageBox.Show("No video sources found", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartCamera()
        {
            if (CurrentDevice != null)
            {
                _videoSource = new VideoCaptureDevice(CurrentDevice.MonikerString);
                _videoSource.NewFrame += video_NewFrame;
                _videoSource.Start();
            }
        }

        private void StopCamera()
        {
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.NewFrame -= new NewFrameEventHandler(video_NewFrame);
            }
        }

        private System.Drawing.Point[] ToPointsArray(List<IntPoint> points)
        {
            System.Drawing.Point[] array = new System.Drawing.Point[points.Count];

            for (int i = 0, n = points.Count; i < n; i++)
            {
                array[i] = new System.Drawing.Point(points[i].X, points[i].Y);
            }

            return array;
        }

        private void videoPlayer_MouseEnter(object sender, MouseEventArgs e)
        {
            if (PickingColor)
            {
                var cursor = ((FrameworkElement)App.Current.Resources["CursorPicker"]).Cursor;
                Cursor = cursor;
            }
        }
        private void videoPlayer_MouseLeave(object sender, MouseEventArgs e)
        {
            Cursor = Cursors.Arrow;
        }
  
        private void videoPlayer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (PickingColor)
            {
                var clickPoint = e.GetPosition(videoPlayer);
                var image = videoPlayer.Source as BitmapImage;             
                int stride = image.PixelWidth * 4;
                int size = image.PixelHeight * stride;
                byte[] pixels = new byte[size];
                image.CopyPixels(pixels, stride, 0);
                int index = ((int)clickPoint.Y) * stride + 4 * ((int)clickPoint.X);
                Blue = pixels[index];
                Green = pixels[index + 1];
                Red = pixels[index + 2];
                PickingColor = false;
                Cursor = Cursors.Arrow;
            }
        }





        #region INotifyPropertyChanged members


        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChangedEventHandler handler = this.PropertyChanged;
            if (handler != null)
            {
                var e = new PropertyChangedEventArgs(propertyName);
                handler(this, e);
            }
        }

        #endregion
    }
}

