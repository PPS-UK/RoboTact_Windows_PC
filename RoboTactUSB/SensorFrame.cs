using System;

namespace RoboTactUSB
{
    // Represents a frame of sensor data with associated metadata
    public class SensorFrame
    {
        // Raw sensor data values, read-only and initialized via constructor
        public int[] RawData { get; private set; }

        // Timestamp indicating when the data was captured
        public int Timestamp { get; private set; } = 0;

        // Unique identifier for the sensor
        public int SensorID { get; set; } = 0;

        // Identifier for this specific data frame
        public int FrameID { get; set; } = 0;

        // Raw packet data, private to encapsulate packet structure details
        private byte[] RawPacket;

        // X and Y coordinates for contact point on sensor surface
        public double ContactX { get; set; } = 0;
        public double ContactY { get; set; } = 0;

        // Accelerometer data for X, Y, and Z axes
        public double AccX { get; set; } = 0;
        public double AccY { get; set; } = 0;
        public double AccZ { get; set; } = 0;

        // Total pressure measured by the sensor
        public double TotalPressure { get; set; } = 0;

        // Constructor to initialize raw data, timestamp, and raw packet data
        public SensorFrame(int[] rawData, int timestamp, byte[] rawPacket)
        {
            RawData = rawData;
            Timestamp = timestamp;
            RawPacket = rawPacket;
        }
    }
}