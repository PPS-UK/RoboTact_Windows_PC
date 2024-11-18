# RoboTact

RoboTact is a .NET8-based application designed to interface with tactile sensors, potentially for use in robotics applications that require tactile feedback. This repository includes the main application and USB interfacing code to manage and interpret sensor data.

## Project Structure

- **RoboTact.sln**: The solution file for the RoboTact project.
- **RoboTact**: Contains the main application, including UI and main logic.
  - `MainWindow.xaml`, `TactileView.xaml`: XAML files for UI structure.
  - `MainWindow.xaml.cs`, `TactileView.xaml.cs`: C# code-behind files for UI interactions and main logic.
  - `RoboTact.csproj`: Project file defining dependencies and build configurations.
- **RoboTactUSB**: Manages sensor data and USB communication for the RoboTact sensor.
  - `RoboTactSensor.cs`, `Sensor.cs`, `SensorFrame.cs`: Core files to handle sensor communication and data processing.
  - `RoboTactUSB.csproj`: Project file for the USB interfacing component.

## Getting Started

### Prerequisites

- **.NET8**: Ensure .NET8 SDK is installed on your system.
- **Visual Studio**: Recommended for development and debugging.

### Building the Project

1. Clone this repository
2. Open the RoboTact.sln solution file in Visual Studio.
3. Build the solution to compile the project.

### Running the Application

1. Run the project from Visual Studio, or execute the compiled .exe from the output directory.
2. Connect the tactile sensor via USB to enable sensor data acquisition.

## Usage
The RoboTact application provides a UI for visualizing tactile sensor data. This can be expanded or customized based on your specific sensor setup and requirements.

## License
This project is licensed under the MIT License.
