using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace RoboTactUSB
{
    // Class representing a RoboTact sensor with serial communication and data processing
    public class RoboTactSensor
    {
        private SerialPort serialPort_; // Serial port for communication
        private const int responseCommandOffset = 0; // Offset for response commands

        private List<byte> incomingSerialBuffer_ = new List<byte>(); // Buffer for incoming serial data
        private List<byte[]> packetLet = new List<byte[]>(); // Temporary storage for packets
        private List<byte[]> processed = new List<byte[]>(); // Storage for processed packets

        // FIFO queue for managing responses based on commands
        private Dictionary<int, ConcurrentQueue<byte[]>> responseFifo_ = new Dictionary<int, ConcurrentQueue<byte[]>>();

        // List of associated sensor objects
        public List<Sensor> sensors = new List<Sensor> { new Sensor(0), new Sensor(1), new Sensor(2), new Sensor(3) };

        // Constructor initializes serial communication with sensor
        public RoboTactSensor()
        {
            // Attempt to find the COM port associated with the sensor's VID/PID
            string portName_ = FindComPortByVidPid("15C5", "0107");
            if (portName_ == null)
                return;

            // Configure the serial port
            serialPort_ = new SerialPort(
                   portName_,
                   115200,
                   Parity.None,
                   8,
                   StopBits.One)
            {
                ReadTimeout = 10,
                WriteTimeout = 10,
                ReadBufferSize = 400,
                DtrEnable = true,
                Handshake = Handshake.RequestToSend
            };

            // Event handler for data received from the sensor
            serialPort_.DataReceived += new SerialDataReceivedEventHandler(SerialDataReceived);

            // Open the serial port
            serialPort_.Open();
        }

        /// <summary>
        /// Finds the COM port associated with a specific VID and PID.
        /// </summary>
        /// <param name="vid">Vendor ID of the device.</param>
        /// <param name="pid">Product ID of the device.</param>
        /// <returns>Returns the COM port name if found, otherwise null.</returns>
        private string FindComPortByVidPid(string vid, string pid)
        {
            // Query for connected USB devices
            var query = "SELECT * FROM Win32_PnPEntity WHERE ConfigManagerErrorCode = 0";
            using (var searcher = new ManagementObjectSearcher(query))
            {
                foreach (var obj in searcher.Get().OfType<ManagementObject>())
                {
                    var pnpIDObj = obj.GetPropertyValue("PNPDeviceID");
                    if (pnpIDObj == null) continue;

                    string pnpID = pnpIDObj.ToString();
                    // Check if device VID and PID match
                    if (pnpID.Contains($"VID_{vid}") && pnpID.Contains($"PID_{pid}"))
                    {
                        var captionObj = obj.GetPropertyValue("Caption");
                        if (captionObj != null)
                        {
                            string caption = captionObj.ToString();
                            var parts = caption.Split(new string[] { "(COM" }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length > 1)
                            {
                                return "COM" + parts[1].Split(')')[0]; // Extract COM port number
                            }
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Sends a command packet to the sensor asynchronously.
        /// </summary>
        /// <param name="packet">Command packet to be sent.</param>
        /// <returns>Returns true if the command is sent successfully.</returns>
        public async Task<bool> SendCommand(byte[] packet)
        {
            // Check if the serial port is open before sending
            if (serialPort_ == null || !serialPort_.IsOpen)
            {
                return false;
            }

            // Send command packet asynchronously
            await serialPort_.BaseStream.WriteAsync(packet, 0, packet.Length).ConfigureAwait(false);
            await Task.Delay(50).ConfigureAwait(false); // Wait briefly for processing

            return true;
        }

        /// <summary>
        /// Closes the serial port connection if open.
        /// </summary>
        public void ClosePort()
        {
            if (serialPort_ != null && serialPort_.IsOpen)
            {
                serialPort_.Close();
            }
        }

        /// <summary>
        /// Event handler for incoming data on the serial port.
        /// Processes and validates data packets.
        /// </summary>
        private void SerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bytesToRead = serialPort_.BytesToRead;

            if (bytesToRead == 0)
                return;

            // Read incoming data into buffer
            byte[] buffer = new byte[bytesToRead];
            byte checksum = 0;
            serialPort_.Read(buffer, 0, buffer.Length);
            incomingSerialBuffer_ = buffer.ToList();

            // Ensure buffer has enough data to process
            if (incomingSerialBuffer_.Count < 38)
                return;

            // Process data packets within buffer
            for (int i = 0; i < incomingSerialBuffer_.Count; i++)
            {
                // Detect start of a valid packet
                if (incomingSerialBuffer_[i] == 0xFF && incomingSerialBuffer_[i + 1] == 0xFF)
                {
                    byte[] packet = new byte[38];
                    var packetRange = incomingSerialBuffer_.GetRange(i, 38);
                    packetRange.CopyTo(packet);
                    incomingSerialBuffer_.RemoveRange(0, i + 38);

                    // Calculate packet checksum for validation
                    checksum = 0;
                    for (int j = 0; j < 30; j++)
                    {
                        checksum += packet[j + 7];
                    }

                    // Validate packet checksum
                    if (checksum == packet[2])
                    {
                        // Create event arguments with processed frame data
                        EventRobotactActionArgs arg = new EventRobotactActionArgs
                        {
                            frame = sensors[packet[3]].ProcessData(packet)
                        };

                        // Raise event for new data packet
                        OnNewRobotactEvent(arg);
                        i = -1; // Reset index to continue processing
                    }
                }
            }
        }

        // Event argument class containing a sensor frame
        public class EventRobotactActionArgs : EventArgs
        {
            public SensorFrame frame { get; set; } // Frame of sensor data
        }

        // Event for new data packet processing
        public event EventHandler<EventRobotactActionArgs> RobotactAction;

        /// <summary>
        /// Raises the RobotactAction event when new data is received.
        /// </summary>
        protected virtual void OnNewRobotactEvent(EventRobotactActionArgs e)
        {
            RobotactAction?.Invoke(this, e);
        }
    }
}
