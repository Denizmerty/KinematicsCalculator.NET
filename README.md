# Kinematics Calculator .NET

A simple desktop application built with WinUI 3 and C# (.NET 8 / .NET 9 - update as appropriate) for calculating variables in constant acceleration kinematics (SUVAT equations).

## Features

*   Calculates one unknown kinematic variable (Displacement, Initial Velocity, Final Velocity, Acceleration, Time) given three known variables.
*   Supports various common units for each variable (e.g., meters, feet, km/h, mph, seconds, minutes).
*   Automatic unit conversion to SI units for calculations.
*   User-friendly interface using WinUI 3 controls.
*   Input validation and clear status messages (including warnings for potentially inconsistent inputs or indeterminate results).
*   Mica backdrop effect for modern Windows look and feel.
*   Minimum window size enforcement.

## Prerequisites (for Building and Running from Source)

*   **Windows 10 (version 1809 or later) or Windows 11**
*   **Visual Studio 2022 (or later)** with the following workloads installed:
    *   .NET desktop development
    *   Universal Windows Platform development (Includes WinUI 3 Project Templates and SDK)
*   **.NET 8 SDK** (or newer, update as needed - usually included with recent Visual Studio versions)
*   **Windows App SDK Runtime** (Should be installed by Visual Studio or when running the app for the first time if packaged)

## Building and Running

1.  **Clone the repository:**
    ```bash
    git clone https://github.com/Denizmerty/KinematicsCalculator.NET.git
    cd KinematicsCalculator.NET
    ```
2.  **Open the solution:** Double-click `KinematicsCalculator.NET.sln` to open it in Visual Studio.
3.  **Restore NuGet packages:** Visual Studio should do this automatically. If not, right-click the solution in Solution Explorer and select "Restore NuGet Packages".
4.  **Select Build Configuration:** Choose `Debug` or `Release` and the appropriate platform (`x64` is recommended).
5.  **Build the solution:** Press `Ctrl+Shift+B` or go to `Build` > `Build Solution`.
6.  **Run the application:** Press `F5` or click the "Start" button (which should show `KinematicsCalculator.NET (Package)` or `KinematicsCalculator.NET (Unpackaged)` depending on your launch profile).

## Usage

1.  Launch the application.
2.  Enter exactly **three** known kinematic values into the corresponding text boxes (Displacement, Initial Velocity, Final Velocity, Acceleration, Time).
3.  Select the appropriate unit for each value entered using the dropdown menus next to the text boxes.
4.  Select the variable you wish to **calculate** from the "Calculate:" dropdown menu. The input field for this variable will become disabled.
5.  Click the "Calculate" button.
6.  The result will be displayed in the "Result" section, using the units selected for the target variable.
7.  Status messages (success, warnings, errors) will appear in the InfoBar at the bottom.
8.  Use the "Clear" button to reset all input fields and the result.

## Dependencies

*   [.NET 8](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (Update if you are using .NET 9 or other)
*   [Windows App SDK 1.7](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/) (Update version if needed)
*   [WinUIEx 2.5.1](https://github.com/dotMorten/WinUIEx) (Update version if needed)

## License

This project is licensed under the [GNU General Public License v3.0](LICENSE).