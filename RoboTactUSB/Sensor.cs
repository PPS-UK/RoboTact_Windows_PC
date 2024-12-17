using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RoboTactUSB
{
    // Represents a sensor device that records data from multiple sensor elements and calculates positions and pressures
    public class Sensor
    {
        // Predefined positions of each sensor element (X, Y coordinates)
        private static readonly (int X, int Y)[] sensorPositions = new (int, int)[]
        {
            (0, 18), (-6, 12), (0, 12), (6, 12), (0, 6), (-6, 0),
            (0, 0), (6, 0), (0, -6), (-6, -12), (0, -12), (6, -12)
        };

        // Calibration baseline values for each sensor element
        public int[] Baseline { get; private set; } = new int[15];
        public List<SensorFrame> SensorData { get; } = new List<SensorFrame>(); // Recorded sensor data frames
        public List<int> deltaT = new List<int> { 0 };  // Time differences between frames

        private int timeOffset = 0; // Synchronization time offset
        private int lastTimeStamp = 0; // Last recorded timestamp
        private int id_; // Unique identifier for this sensor

        private const double SLIP_THRESHOLD = 0.2;  // Threshold for slip detection
        private const int STABILITY_WINDOW_SIZE = 5; // Number of frames to check for stability
        private Queue<double> accelerationHistory = new Queue<double>();

        public event EventHandler<SlipDetectedEventArgs> SlipDetected; // Event for slip detection

        private CancellationTokenSource slipDetectionCancellationTokenSource;

        private const double EXP_DECAY_FACTOR = 5; // Smoothing factor for the exponential decay filter
        private double[] filteredData = new double[12]; // Filtered values for the 12 sensor elements

        // Constructor: initializes the sensor with an ID, sets baseline, and starts slip detection
        public Sensor(int id)
        {
            ResetBaseline();
            id_ = id;
            slipDetectionCancellationTokenSource = new CancellationTokenSource();
            Task.Run(() => SlipDetectionLoop(slipDetectionCancellationTokenSource.Token));
        }

        // Continuously checks for slip events using the latest acceleration data
        private async Task SlipDetectionLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                if (SensorData.Count > 0)
                {
                    var latestFrame = SensorData[^1];
                    bool isSlipping = await Task.Run(() => IsSlipping(latestFrame.AccX, latestFrame.AccY, latestFrame.AccZ));

                    if (isSlipping)
                    {
                        OnSlipDetected();  // Trigger SlipDetected event if slip is detected
                    }
                }
                await Task.Delay(5, cancellationToken); // Short delay to reduce CPU usage
            }
        }

        // Stops the slip detection loop
        public void StopSlipDetection()
        {
            slipDetectionCancellationTokenSource.Cancel();
        }

        // Calculates contact position and total pressure from the pressure values of sensor elements
        public static (double CenterX, double CenterY, double TotalPressure) CalculatePositionAndTotalPressure(double[] pressures)
        {
            if (pressures.Length != sensorPositions.Length)
                throw new ArgumentException("Pressure array length must match the number of sensor elements.");

            double totalPressure = 0;
            double weightedX = 0;
            double weightedY = 0;

            // Accumulate total pressure and weighted positions
            for (int i = 0; i < pressures.Length; i++)
            {
                totalPressure += pressures[i];
                weightedX += sensorPositions[i].X * pressures[i];
                weightedY += sensorPositions[i].Y * pressures[i];
            }

            // Return zero values if small pressure is applied
            if (totalPressure < 3)
                return (0, 0, totalPressure);

            // Calculate weighted average positions
            double centerX = weightedX / totalPressure;
            double centerY = weightedY / totalPressure;

            return (centerX, centerY, totalPressure);
        }

        // Resets the baseline calibration values for each sensor element to zero
        public void ResetBaseline()
        {
            for (int i = 0; i < Baseline.Length; i++) Baseline[i] = 0;
        }

        // Checks if the sensor detects slipping based on acceleration data
        public bool IsSlipping(double accX, double accY, double accZ)
        {
            // Calculate magnitude of acceleration
            double magnitude = Math.Sqrt(accX * accX + accY * accY + accZ * accZ);

            // Keep track of recent magnitudes to assess stability
            accelerationHistory.Enqueue(magnitude);
            if (accelerationHistory.Count > STABILITY_WINDOW_SIZE)
            {
                accelerationHistory.Dequeue();
            }

            // Calculate variance of the recent acceleration magnitudes
            double mean = accelerationHistory.Average();
            double variance = accelerationHistory.Select(val => (val - mean) * (val - mean)).Average();

            // Check if variance exceeds threshold, indicating possible slippage
            return variance > SLIP_THRESHOLD;
        }

        // Processes a new data packet and returns a calibrated sensor frame
        public SensorFrame ProcessData(byte[] packet)
        {
            // Extract timestamp and frame ID from packet
            int timestamp = (packet[4] << 8) + packet[5];
            int[] data = new int[15];
            int frameID = packet[6];

            // Initialize time offset on the first packet
            if (timeOffset == 0)
            {
                timeOffset = timestamp;
            }

            // Adjust timestamp by time offset
            timestamp -= timeOffset;

            // Update deltaT list with time difference since last timestamp
            if (lastTimeStamp != 0)
            {
                if (timestamp < lastTimeStamp)
                    lastTimeStamp -= 65536;  // Adjust for timestamp wrap-around

                deltaT.Add(timestamp - lastTimeStamp);

                // Limit deltaT list to the last 50 entries
                if (deltaT.Count > 50)
                {
                    deltaT.RemoveAt(0);
                }
            }

            // Update last timestamp
            lastTimeStamp = timestamp;

            // Calibrate data based on baseline
            for (int i = 0; i < 15; i++)
            {
                data[i] = (packet[7 + i * 2] << 8) + packet[8 + i * 2];

                if (Baseline[i] == 0 && i < 12)
                {
                    Baseline[i] = data[i]; // Set baseline if not already set
                }

                // Adjust data based on baseline calibration
                data[i] = data[i] - Baseline[i];
            }

            // Create a new sensor frame with calibrated data
            SensorFrame frame = new SensorFrame(data, timestamp, packet);

            // Read accelerometer data (X, Y, Z) from the frame
            frame.AccX = ((short)data[12]) / 16383.00;
            frame.AccY = ((short)data[13]) / 16383.00;
            frame.AccZ = ((short)data[14]) / 16383.00;

            // Calibrate data for contact position and pressure calculation
            double[] calibratedData = new double[12];
            for (int i = 0; i < 12; i++)
            {
                double rawValue = data[i] / 2600.00 * 40.00;

                filteredData[i] = 1.00 * rawValue / EXP_DECAY_FACTOR + (1.00 - 1.00 / EXP_DECAY_FACTOR) * filteredData[i];

                // Avoid very low values affecting calculations
                if (filteredData[i] < 0.1)
                    filteredData[i] = 0;

                calibratedData[i] = Math.Round(filteredData[i], 2);
            }

            // Set frame metadata
            frame.SensorID = id_;
            frame.FrameID = frameID;

            // Calculate and set contact position and total pressure
            (frame.ContactX, frame.ContactY, frame.TotalPressure) = CalculatePositionAndTotalPressure(calibratedData);

            // Add the frame to the sensor data history
            SensorData.Add(frame);
            return frame;
        }

        // Raises the SlipDetected event
        protected virtual void OnSlipDetected()
        {
            SlipDetected?.Invoke(this, new SlipDetectedEventArgs(id_));
        }
    }

    // Event arguments for slip detection, containing sensor ID
    public class SlipDetectedEventArgs : EventArgs
    {
        public int SensorID { get; }

        public SlipDetectedEventArgs(int sensorID)
        {
            SensorID = sensorID;
        }
    }
}
