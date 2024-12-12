using LiveChartsCore;
using LiveChartsCore.Defaults;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using RoboTactUSB;
using System.Reflection;
using System.Drawing;

namespace RoboTact
{
    public partial class MainWindow : Window
    {
        // Collections for each sensor's pressure data
        public ObservableCollection<ObservableValue>[] SensorTotalPressureSeries { get; set; } = new ObservableCollection<ObservableValue>[4];

        // Chart series data for each sensor
        public ISeries[] Series { get; set; }

        // RoboTact sensor and frames
        private RoboTactHub RoboTact_ = new RoboTactHub();
        private SensorFrame[] frames = new SensorFrame[4];

        // Timer for periodic updates
        private DispatcherTimer _timer;

        // Views for each tactile sensor
        private List<TactileView> views = new List<TactileView>();

        public MainWindow()
        {
            InitializeComponent();

            // Initialize sensor series for each sensor
            for (int i = 0; i < 4; i++)
            {
                SensorTotalPressureSeries[i] = new ObservableCollection<ObservableValue>();
                RoboTact_.sensors[i].SlipDetected += OnSlipDetected;
            }

            // Define chart series for each sensor
            Series = SensorTotalPressureSeries.Select((series, index) =>
                new LineSeries<ObservableValue>
                {
                    Values = series,
                    Name = $"Sensor {index + 1}",
                    Fill = null,
                    GeometrySize = 0
                }).ToArray();

            DataContext = this;

            // Set up event handlers and frames for sensors
            RoboTact_.RobotactAction += RoboTact__RobotactAction;
            InitializeFrames();
            InitializeViews();
            InitializeChart();
            InitializeTimer();
            foreach (var sensor in RoboTact_.sensors)
                sensor.ResetBaseline();
        }

        private void OnSlipDetected(object sender, SlipDetectedEventArgs e)
        {
            DynamicViewsPanel.Dispatcher.Invoke(() =>
            {
                views[e.SensorID].Background = System.Windows.Media.Brushes.Red;
                var timer = new System.Windows.Threading.DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(1);
                timer.Tick += (s, args) =>
                {
                    // Change the background back to white
                    views[e.SensorID].Background = System.Windows.Media.Brushes.AliceBlue;

                    // Stop and clean up the timer
                    timer.Stop();
                };

                // Start the timer
                timer.Start();
            });
        }

        // Event handler for RoboTact sensor data update
        private void RoboTact__RobotactAction(object sender, RoboTactHub.EventRobotactActionArgs e)
        {
            int sensorID = e.frame.SensorID;
            frames[sensorID] = e.frame;
        }

        // Initialize sensor frames with default values
        private void InitializeFrames()
        {
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = new SensorFrame(new int[15], 0, new byte[30]);
            }
        }

        // Initialize tactile views for each sensor
        private void InitializeViews()
        {
            for (int i = 0; i < 4; i++)
            {
                var newView = new TactileView { X = 100, Y = 100, Radius = 10 };
                views.Add(newView);
                DynamicViewsPanel.Children.Add(newView);
            }
        }

        // Set up chart properties
        private void InitializeChart()
        {
            chart.YAxes = new Axis[] { new Axis { MinLimit = 0, MaxLimit = 60 } };
        }

        // Configure and start the update timer
        private void InitializeTimer()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        // Timer tick handler: updates sensor data and views
        private void Timer_Tick(object sender, EventArgs e)
        {
            double[] scanRates = RoboTact_.sensors
                .Select(sensor => 10000 / sensor.deltaT.Average()) // Calculate scan rate
                .Where(rate => !double.IsInfinity(rate))           // Exclude infinity values
                .Select(rate => Math.Round(rate))                  // Round each valid scan rate
                .ToArray();

            // Calculate average of filtered scan rates
            double averageScanRate = scanRates.Length > 0 ? scanRates.Average() : 0;
            UpdateSensorSeries();
            UpdateViews(averageScanRate);
        }

        // Update pressure data series for each sensor
        private void UpdateSensorSeries()
        {
            for (int i = 0; i < SensorTotalPressureSeries.Length; i++)
            {
                SensorTotalPressureSeries[i].Add(new ObservableValue(frames[i].TotalPressure));
                LimitSeriesData(SensorTotalPressureSeries[i]);
            }
        }

        // Limit data points in a series to improve performance
        private void LimitSeriesData(ObservableCollection<ObservableValue> series)
        {
            if (series.Count > 50)
                series.RemoveAt(0);
        }

        // Update tactile views based on sensor data
        private void UpdateViews(double scanRate)
        {
            for (int i = 0; i < frames.Length; i++)
            {
                double x = 100;
                double y = 100;

                // Adjust position based on contact points if pressure is detected
                if (frames[i].TotalPressure > 1)
                {
                    x = 100 + frames[i].ContactX / 18.0 * 100;
                    y = 100 + frames[i].ContactY / -18.0 * 100;
                }

                // Update view properties
                int index = i;  // Avoid capturing loop variable in lambda
                DynamicViewsPanel.Dispatcher.Invoke(() =>
                {
                    views[index].X = x;
                    views[index].Y = y;
                    views[index].Radius = frames[index].TotalPressure * 1.5;
                    views[index].Radius = views[index].Radius < 3 ? 0 : views[index].Radius;

                    // Update window title with scan rate if sufficient data points exist
                    if (RoboTact_.sensors[0].deltaT.Count > 10 
                    || RoboTact_.sensors[1].deltaT.Count > 10 
                    || RoboTact_.sensors[2].deltaT.Count > 10 
                    || RoboTact_.sensors[3].deltaT.Count > 10)
                    {
                        this.Title = $"RoboTact {scanRate}Hz";
                    }
                });
            }
        }

        // Button click handler to reset baseline for each sensor
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            foreach (var sensor in RoboTact_.sensors)
                sensor.ResetBaseline();
        }
    }
}
