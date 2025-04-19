using Microsoft.UI;
using Microsoft.UI.Xaml;
using WinUIEx;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Diagnostics;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

namespace KinematicsCalculator.NET
{
    public sealed partial class MainWindow : WindowEx
    {
        private const string VarDisplacement = "displacement";
        private const string VarInitialVelocity = "initial_velocity";
        private const string VarFinalVelocity = "final_velocity";
        private const string VarAcceleration = "acceleration";
        private const string VarTime = "time";

        private readonly Dictionary<string, (string DisplayName, string UnitCategory)> _varDetails = new()
        {
            { VarDisplacement, ("Displacement (Δx)", "Length") },
            { VarInitialVelocity, ("Initial Velocity (v₀)", "Velocity") },
            { VarFinalVelocity, ("Final Velocity (v)", "Velocity") },
            { VarAcceleration, ("Acceleration (a)", "Acceleration") },
            { VarTime, ("Time (t)", "Time") }
        };

        // Stores unit conversion factors relative to SI base units (e.g., meters, m/s)
        // Using tuples (ToSI, FromSI) avoids repeated division calculations
        private readonly Dictionary<string, Dictionary<string, (double ToSI, double FromSI)>> _units = new()
        {
            {
                "Length", new Dictionary<string, (double, double)>
                {
                    { "m", (1.0, 1.0) },
                    { "ft", (0.3048, 1 / 0.3048) },
                    { "km", (1000.0, 1 / 1000.0) },
                    { "mi", (1609.34, 1 / 1609.34) }
                }
            },
            {
                "Velocity", new Dictionary<string, (double, double)>
                {
                    { "m/s", (1.0, 1.0) },
                    { "ft/s", (0.3048, 1 / 0.3048) },
                    { "km/h", (1000.0 / 3600.0, 3600.0 / 1000.0) },
                    { "mph", (1609.34 / 3600.0, 3600.0 / 1609.34) }
                }
            },
            {
                "Acceleration", new Dictionary<string, (double, double)>
                {
                    { "m/s²", (1.0, 1.0) },
                    { "ft/s²", (0.3048, 1 / 0.3048) }
                }
            },
            {
                "Time", new Dictionary<string, (double, double)>
                {
                    { "s", (1.0, 1.0) },
                    { "min", (60.0, 1 / 60.0) },
                    { "h", (3600.0, 1 / 3600.0) }
                }
            }
        };

        // Maps variable keys to their corresponding input TextBox and unit ComboBox
        private Dictionary<string, (TextBox Input, ComboBox Unit)> _inputControls = default!;

        private const double Epsilon = 1e-9; // Tolerance for floating-point comparisons
        private const int MinWindowWidth = 500; // Prevent UI elements from becoming unusable at very small sizes
        private const int MinWindowHeight = 680; // Prevent UI elements from becoming unusable at very small sizes

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "Kinematics Calculator";
            InitializeAppWindow();
            InitializeInputControlsMap(); // Must happen before controls are used
            PopulateComboBoxes();
            UpdateControlStates(); // Set initial enabled/disabled state
            ShowStatus("Enter 3 known values and select the variable to calculate.", InfoBarSeverity.Informational, "Ready");
        }

        private void InitializeAppWindow()
        {
            try
            {
                // WinUI 3 requires interop to get the AppWindow for advanced manipulation
                IntPtr hWnd = WindowNative.GetWindowHandle(this);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);
                if (appWindow != null)
                {
                    // Set a reasonable default size on startup
                    appWindow.Resize(new SizeInt32(600, 700));
                    appWindow.Changed += AppWindow_Changed;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error initializing AppWindow: {ex.Message}");
            }
        }

        // Enforces minimum window size if the user tries to resize below limits
        private void AppWindow_Changed(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (args.DidSizeChange && sender != null)
            {
                SizeInt32 size = sender.Size;
                int newWidth = size.Width < MinWindowWidth ? MinWindowWidth : size.Width;
                int newHeight = size.Height < MinWindowHeight ? MinWindowHeight : size.Height;
                if (newWidth != size.Width || newHeight != size.Height)
                {
                    // Resize only if necessary to avoid redundant calls or potential resize loops
                    sender.Resize(new SizeInt32(newWidth, newHeight));
                }
            }
        }

        // Creates the mapping between variable keys and UI controls for easy access
        private void InitializeInputControlsMap()
        {
            _inputControls = new Dictionary<string, (TextBox, ComboBox)>
            {
                { VarDisplacement, (InputDisplacementTextBox, UnitDisplacementComboBox) },
                { VarInitialVelocity, (InputInitialVelocityTextBox, UnitInitialVelocityComboBox) },
                { VarFinalVelocity, (InputFinalVelocityTextBox, UnitFinalVelocityComboBox) },
                { VarAcceleration, (InputAccelerationTextBox, UnitAccelerationComboBox) },
                { VarTime, (InputTimeTextBox, UnitTimeComboBox) }
            };

            // Set the Header for each TextBox based on the display name
            foreach (var kvp in _varDetails)
            {
                if (_inputControls.TryGetValue(kvp.Key, out var controls))
                {
                    controls.Input.Header = kvp.Value.DisplayName;
                }
            }
        }

        // Fills the unit dropdowns and the calculation choice dropdown
        private void PopulateComboBoxes()
        {
            foreach (var kvp in _varDetails)
            {
                if (_inputControls.TryGetValue(kvp.Key, out var controls) &&
                    _units.TryGetValue(kvp.Value.UnitCategory, out var unitDict))
                {
                    controls.Unit.ItemsSource = unitDict.Keys.ToList();
                    controls.Unit.SelectedIndex = 0;
                }
            }

            CalculateChoiceComboBox.ItemsSource = _varDetails
                .Values.Select(d => d.DisplayName)
                .ToList();
            CalculateChoiceComboBox.SelectedIndex = 0;
        }

        private string? GetVarKeyFromDisplay(string displayName)
            => _varDetails.FirstOrDefault(kvp => kvp.Value.DisplayName == displayName).Key;

        private void CalculateChoiceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Guard against calls during initial setup before _inputControls is ready
            if (_inputControls != null)
            {
                UpdateControlStates();
                ClearResultAndStatus(); // Clear previous results when the target changes
            }
        }

        // Enables/disables and styles input fields based on which variable is selected for calculation
        private void UpdateControlStates()
        {
            var selected = CalculateChoiceComboBox.SelectedItem as string;
            if (selected == null || _inputControls == null) return;

            string targetKey = GetVarKeyFromDisplay(selected)!;
            foreach (var kvp in _inputControls)
            {
                bool isTarget = kvp.Key == targetKey;
                var (input, _) = kvp.Value;
                input.IsEnabled = !isTarget;
                input.Header = _varDetails[kvp.Key].DisplayName;

                if (isTarget)
                {
                    input.Text = string.Empty;
                    input.PlaceholderText = "Calculated Value";
                    input.BorderBrush = (SolidColorBrush)
                        Application.Current.Resources["AccentFillColorDefaultBrush"];
                    input.BorderThickness = new Thickness(2);
                }
                else
                {
                    input.PlaceholderText = "Enter value";
                    input.BorderBrush = (SolidColorBrush)
                        Application.Current.Resources["ControlStrokeColorDefaultBrush"];
                    input.BorderThickness = (Thickness)
                        Application.Current.Resources["TextControlBorderThemeThickness"];
                }
            }
        }

        // Clears all input fields, resets units, and calculation choice
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var (input, unit) in _inputControls.Values)
            {
                if (input.IsEnabled) input.Text = string.Empty;
                unit.SelectedIndex = 0;
            }

            CalculateChoiceComboBox.SelectedIndex = 0;
            UpdateControlStates();
            ClearResultAndStatus();
            ShowStatus("Fields cleared. Enter new values.", InfoBarSeverity.Informational, "Cleared");

            _inputControls.Values
                .FirstOrDefault(ctrls => ctrls.Input.IsEnabled)
                .Input?.Focus(FocusState.Programmatic);
        }

        // Hides the result display area
        private void ClearResultAndStatus()
        {
            ResultVariableTextBlock.Text = "";
            ResultValueTextBlock.Text = "";
            ResultUnitTextBlock.Text = "";
            ResultBorder.Visibility = Visibility.Collapsed;
            ClearStatus();
        }

        private void ShowStatus(string message, InfoBarSeverity severity, string? title = null)
        {
            StatusInfoBar.Title = title ?? severity.ToString();
            StatusInfoBar.Message = message;
            StatusInfoBar.Severity = severity;
            StatusInfoBar.IsOpen = true;
        }

        private void ClearStatus() => StatusInfoBar.IsOpen = false;

        // Converts a value from a given unit to its SI equivalent
        private double ConvertToSI(double value, string unit, string category)
        {
            if (_units.TryGetValue(category, out var dict) &&
                dict.TryGetValue(unit, out var factors))
            {
                return value * factors.ToSI;
            }
            throw new ArgumentException($"Unknown unit '{unit}' for category '{category}'.");
        }

        // Converts a value from SI units to a specified target unit
        private double ConvertFromSI(double valueSI, string targetUnit, string category)
        {
            if (_units.TryGetValue(category, out var dict) &&
                dict.TryGetValue(targetUnit, out var factors))
            {
                return valueSI * factors.FromSI;
            }
            throw new ArgumentException($"Unknown unit '{targetUnit}' for category '{category}'.");
        }

        // Reads value from an input TextBox, validates, and converts to SI units
        // Returns null if the field is empty or disabled
        private double? GetValueInSI(string varKey)
        {
            if (!_inputControls.TryGetValue(varKey, out var controls))
                throw new InvalidOperationException($"Input controls for key '{varKey}' not found.");

            var (input, unitComboBox) = controls;
            if (!input.IsEnabled) return null;

            string text = input.Text.Trim();
            if (string.IsNullOrEmpty(text)) return null;

            var category = _varDetails[varKey].UnitCategory;
            // Use InvariantCulture to ensure consistent parsing
            if (double.TryParse(text, NumberStyles.Any,
                CultureInfo.InvariantCulture, out double value))
            {
                string unit = unitComboBox.SelectedItem as string
                    ?? throw new FormatException($"No unit selected for '{_varDetails[varKey].DisplayName}'.");
                return ConvertToSI(value, unit, category);
            }
            throw new FormatException($"Invalid numeric input for '{_varDetails[varKey].DisplayName}'.");
        }

        // Formats and displays the calculated result in the designated area
        private void DisplayResult(string variableKey, double valueSI)
        {
            if (!_inputControls.TryGetValue(variableKey, out var targetControls))
            {
                ShowStatus($"Internal Error: Could not find controls for '{variableKey}'.",
                    InfoBarSeverity.Error, "Internal Error");
                return;
            }

            string targetUnit = targetControls.Unit.SelectedItem as string
                ?? throw new ArgumentException($"Target unit missing for '{_varDetails[variableKey].DisplayName}'.");

            string category = _varDetails[variableKey].UnitCategory;
            double displayValue = ConvertFromSI(valueSI, targetUnit, category);

            string valueStr;
            // Handle near-zero values explicitly as "0"
            if (Math.Abs(displayValue) < Epsilon) valueStr = "0";
            // Use general format for readability
            else if (Math.Abs(displayValue) >= 1e-4 && Math.Abs(displayValue) < 1e7)
                valueStr = displayValue.ToString("G7", CultureInfo.InvariantCulture);
            // Use scientific notation for very large or very small values
            else valueStr = displayValue.ToString("E4", CultureInfo.InvariantCulture);

            ResultVariableTextBlock.Text = $"{_varDetails[variableKey].DisplayName}:";
            ResultValueTextBlock.Text = valueStr;
            ResultUnitTextBlock.Text = targetUnit;
            ResultBorder.Visibility = Visibility.Visible;

            // Show success message only if no warning/error is already being shown
            if (!StatusInfoBar.IsOpen || StatusInfoBar.Severity < InfoBarSeverity.Warning)
                ShowStatus("Calculation successful.", InfoBarSeverity.Success, "Success");
        }

        // Main calculation logic triggered by the Calculate button
        private void CalculateButton_Click(object sender, RoutedEventArgs e)
        {
            ClearStatus();
            ResultBorder.Visibility = Visibility.Collapsed;

            var selected = CalculateChoiceComboBox.SelectedItem as string;
            if (selected == null || GetVarKeyFromDisplay(selected) is not string targetKey)
            {
                ShowStatus("Please select a variable to calculate.", InfoBarSeverity.Error, "Selection Error");
                return;
            }

            // Collect known values provided by the user, converting them to SI units
            var knownValuesSI = new Dictionary<string, double>();
            try
            {
                foreach (var key in _varDetails.Keys)
                {
                    if (key == targetKey) continue;
                    var val = GetValueInSI(key);
                    if (val.HasValue) knownValuesSI.Add(key, val.Value);
                }
            }
            catch (FormatException ex)
            {
                ShowStatus($"Input Error: {ex.Message}", InfoBarSeverity.Error, "Input Error");
                return;
            }
            catch (ArgumentException ex)
            {
                ShowStatus($"Input Error: {ex.Message}", InfoBarSeverity.Error, "Input Error");
                return;
            }

            // Kinematics requires exactly 3 known values to solve for an unknown
            if (knownValuesSI.Count != 3)
            {
                ShowStatus($"Provide exactly 3 known values (found {knownValuesSI.Count}).",
                    InfoBarSeverity.Error, "Input Error");
                return;
            }

            try
            {
                // Extract SI values into nullable doubles for easier checking in formulas
                knownValuesSI.TryGetValue(VarDisplacement, out double dxVal);
                bool dxProvided = knownValuesSI.ContainsKey(VarDisplacement);
                double? dx = dxProvided ? dxVal : null;

                knownValuesSI.TryGetValue(VarInitialVelocity, out double v0Val);
                bool v0Provided = knownValuesSI.ContainsKey(VarInitialVelocity);
                double? v0 = v0Provided ? v0Val : null;

                knownValuesSI.TryGetValue(VarFinalVelocity, out double vVal);
                bool vProvided = knownValuesSI.ContainsKey(VarFinalVelocity);
                double? v = vProvided ? vVal : null;

                knownValuesSI.TryGetValue(VarAcceleration, out double aVal);
                bool aProvided = knownValuesSI.ContainsKey(VarAcceleration);
                double? a = aProvided ? aVal : null;

                knownValuesSI.TryGetValue(VarTime, out double tVal);
                bool tProvided = knownValuesSI.ContainsKey(VarTime);
                double? t = tProvided ? tVal : null;

                bool warningIssued = false; // Track if a non-fatal warning was shown

                // Check for physical inconsistencies in provided inputs
                if (a.HasValue && Math.Abs(a.Value) < Epsilon && v0.HasValue && v.HasValue &&
                    Math.Abs(v.Value - v0.Value) > Epsilon)
                {
                    ShowStatus("Provided acceleration is zero, but initial/final velocities differ.",
                        InfoBarSeverity.Warning, "Input Warning");
                    warningIssued = true;
                }

                if (a.HasValue && a.Value > Epsilon && v0.HasValue && v.HasValue &&
                    v.Value < v0.Value - Epsilon)
                {
                    ShowStatus("Provided acceleration is positive, but final velocity < initial velocity.",
                        InfoBarSeverity.Warning, "Input Warning");
                    warningIssued = true;
                }

                if (a.HasValue && a.Value < -Epsilon && v0.HasValue && v.HasValue &&
                    v.Value > v0.Value + Epsilon)
                {
                    ShowStatus("Provided acceleration is negative, but final velocity > initial velocity.",
                        InfoBarSeverity.Warning, "Input Warning");
                    warningIssued = true;
                }

                if (t.HasValue && Math.Abs(t.Value) < Epsilon)
                {
                    if (v0.HasValue && v.HasValue &&
                        Math.Abs(v.Value - v0.Value) > Epsilon)
                    {
                        ShowStatus("Provided time is zero, but initial/final velocities differ.",
                            InfoBarSeverity.Warning, "Input Warning");
                        warningIssued = true;
                    }
                    if (dx.HasValue && Math.Abs(dx.Value) > Epsilon)
                    {
                        ShowStatus("Provided time is zero, but displacement is non-zero.",
                            InfoBarSeverity.Warning, "Input Warning");
                        warningIssued = true;
                    }
                }

                if (v0.HasValue && a.HasValue && dx.HasValue &&
                    (v0.Value * v0.Value + 2 * a.Value * dx.Value) < -Epsilon)
                {
                    ShowStatus("Provided inputs imply an imaginary final velocity (v² < 0).",
                        InfoBarSeverity.Warning, "Input Warning");
                    warningIssued = true;
                }

                if (v.HasValue && a.HasValue && dx.HasValue &&
                    (v.Value * v.Value - 2 * a.Value * dx.Value) < -Epsilon)
                {
                    ShowStatus("Provided inputs imply an imaginary initial velocity (v₀² < 0).",
                        InfoBarSeverity.Warning, "Input Warning");
                    warningIssued = true;
                }

                // Specific check: if average velocity is zero, displacement must be zero to solve for time
                if (targetKey == VarTime && dx.HasValue && v0.HasValue && v.HasValue &&
                    Math.Abs(v0.Value + v.Value) < Epsilon && Math.Abs(dx.Value) > Epsilon)
                {
                    ShowStatus("Average velocity is zero, but displacement is non-zero. Cannot solve for time.",
                        InfoBarSeverity.Error, "Calculation Error");
                    return;
                }

                double? resultSI = null;
                string infoMessage = "";

                // Select the appropriate kinematic equation based on the target variable and available inputs
                switch (targetKey)
                {
                    case VarDisplacement: // Calculate Δx
                        // Equation: Δx = v₀t + ½at²
                        if (v0.HasValue && a.HasValue && t.HasValue)
                        {
                            resultSI = (v0.Value * t.Value) + (0.5 * a.Value * t.Value * t.Value);
                        }
                        // Equation: Δx = ½(v₀ + v)t
                        else if (v0.HasValue && v.HasValue && t.HasValue)
                        {
                            resultSI = 0.5 * (v0.Value + v.Value) * t.Value;
                        }
                        // Equation: Δx = vt - ½at²
                        else if (v.HasValue && a.HasValue && t.HasValue)
                        {
                            resultSI = (v.Value * t.Value) - (0.5 * a.Value * t.Value * t.Value);
                        }
                        // Equation: v² = v₀² + 2aΔx => Δx = (v² - v₀²) / 2a
                        else if (v0.HasValue && v.HasValue && a.HasValue)
                        {
                            // Avoid division by zero if acceleration is zero
                            if (Math.Abs(a.Value) < Epsilon)
                            {
                                if (Math.Abs(v.Value - v0.Value) < Epsilon)
                                {
                                    ShowStatus("Displacement cannot be determined (a=0, v=v₀). Provide time instead.",
                                        InfoBarSeverity.Warning, "Indeterminate");
                                    return;
                                }
                                else
                                {
                                    ShowStatus("Inconsistent state (a=0, v≠v₀). Check inputs.",
                                        InfoBarSeverity.Error, "Calculation Error");
                                    return;
                                }
                            }
                            else
                            {
                                resultSI = (v.Value * v.Value - v0.Value * v0.Value) / (2.0 * a.Value);
                            }
                        }
                        break;

                    case VarInitialVelocity: // Calculate v₀
                        // Equation: v = v₀ + at => v₀ = v - at
                        if (v.HasValue && a.HasValue && t.HasValue)
                        {
                            resultSI = v.Value - (a.Value * t.Value);
                        }
                        // Equation: Δx = v₀t + ½at² => v₀ = (Δx - ½at²) / t
                        else if (dx.HasValue && a.HasValue && t.HasValue)
                        {
                            if (Math.Abs(t.Value) < Epsilon)
                            {
                                ShowStatus("Cannot divide by zero time.", InfoBarSeverity.Error, "Calculation Error");
                                return;
                            }
                            resultSI = (dx.Value - 0.5 * a.Value * t.Value * t.Value) / t.Value;
                        }
                        // Equation: Δx = ½(v₀ + v)t => v₀ = (2Δx / t) - v
                        else if (dx.HasValue && v.HasValue && t.HasValue)
                        {
                            if (Math.Abs(t.Value) < Epsilon)
                            {
                                ShowStatus("Cannot divide by zero time.", InfoBarSeverity.Error, "Calculation Error");
                                return;
                            }
                            resultSI = (2.0 * dx.Value / t.Value) - v.Value;
                        }
                        // Equation: v² = v₀² + 2aΔx => v₀² = v² - 2aΔx
                        else if (v.HasValue && a.HasValue && dx.HasValue)
                        {
                            double v0_sq = v.Value * v.Value - 2.0 * a.Value * dx.Value;
                            if (v0_sq < -Epsilon)
                            {
                                ShowStatus("Resulting initial velocity is imaginary (v₀² < 0). Check inputs.",
                                    InfoBarSeverity.Error, "Calculation Error");
                                return;
                            }
                            resultSI = Math.Sqrt(Math.Max(0.0, v0_sq));
                            if (resultSI > Epsilon)
                            {
                                infoMessage = "Calculated positive root for v₀. Negative root might also be valid.";
                            }
                        }
                        break;

                    case VarFinalVelocity: // Calculate v
                        // Equation: v = v₀ + at
                        if (v0.HasValue && a.HasValue && t.HasValue)
                        {
                            resultSI = v0.Value + (a.Value * t.Value);
                        }
                        // Equation: Δx = ½(v₀ + v)t => v = (2Δx / t) - v₀
                        else if (dx.HasValue && v0.HasValue && t.HasValue)
                        {
                            if (Math.Abs(t.Value) < Epsilon)
                            {
                                ShowStatus("Cannot divide by zero time.", InfoBarSeverity.Error, "Calculation Error");
                                return;
                            }
                            resultSI = (2.0 * dx.Value / t.Value) - v0.Value;
                        }
                        // Equation: Δx = vt - ½at² => v = (Δx / t) + ½at
                        else if (dx.HasValue && a.HasValue && t.HasValue)
                        {
                            if (Math.Abs(t.Value) < Epsilon)
                            {
                                ShowStatus("Cannot divide by zero time.", InfoBarSeverity.Error, "Calculation Error");
                                return;
                            }
                            resultSI = (dx.Value / t.Value) + (0.5 * a.Value * t.Value);
                        }
                        // Equation: v² = v₀² + 2aΔx
                        else if (v0.HasValue && a.HasValue && dx.HasValue)
                        {
                            double v_sq = v0.Value * v0.Value + 2.0 * a.Value * dx.Value;
                            if (v_sq < -Epsilon)
                            {
                                ShowStatus("Resulting final velocity is imaginary (v² < 0). Check inputs.",
                                    InfoBarSeverity.Error, "Calculation Error");
                                return;
                            }
                            resultSI = Math.Sqrt(Math.Max(0.0, v_sq));
                            if (resultSI > Epsilon)
                            {
                                infoMessage = "Calculated positive root for v. Negative root might also be valid.";
                            }
                        }
                        break;

                    case VarAcceleration: // Calculate a
                        // Equation: v = v₀ + at => a = (v - v₀) / t
                        if (v0.HasValue && v.HasValue && t.HasValue)
                        {
                            if (Math.Abs(t.Value) < Epsilon)
                            {
                                if (Math.Abs(v.Value - v0.Value) < Epsilon)
                                {
                                    ShowStatus("Acceleration cannot be determined (t=0, v=v₀).",
                                        InfoBarSeverity.Warning, "Indeterminate");
                                    return;
                                }
                                else
                                {
                                    ShowStatus("Infinite acceleration implied (t=0, v≠v₀).",
                                        InfoBarSeverity.Error, "Calculation Error");
                                    return;
                                }
                            }
                            resultSI = (v.Value - v0.Value) / t.Value;
                        }
                        // Equation: Δx = v₀t + ½at² => a = 2(Δx - v₀t) / t²
                        else if (dx.HasValue && v0.HasValue && t.HasValue)
                        {
                            if (Math.Abs(t.Value) < Epsilon || Math.Abs(t.Value * t.Value) < Epsilon * Epsilon)
                            {
                                ShowStatus("Cannot divide by zero time squared.", InfoBarSeverity.Error, "Calculation Error");
                                return;
                            }
                            resultSI = 2.0 * (dx.Value - v0.Value * t.Value) / (t.Value * t.Value);
                        }
                        // Equation: Δx = vt - ½at² => a = 2(vt - Δx) / t²
                        else if (dx.HasValue && v.HasValue && t.HasValue)
                        {
                            if (Math.Abs(t.Value) < Epsilon || Math.Abs(t.Value * t.Value) < Epsilon * Epsilon)
                            {
                                ShowStatus("Cannot divide by zero time squared.", InfoBarSeverity.Error, "Calculation Error");
                                return;
                            }
                            resultSI = 2.0 * (v.Value * t.Value - dx.Value) / (t.Value * t.Value);
                        }
                        // Equation: v² = v₀² + 2aΔx => a = (v² - v₀²) / 2Δx
                        else if (v0.HasValue && v.HasValue && dx.HasValue)
                        {
                            if (Math.Abs(dx.Value) < Epsilon)
                            {
                                if (Math.Abs(v.Value * v.Value - v0.Value * v0.Value) < Epsilon)
                                {
                                    ShowStatus("Acceleration cannot be determined (Δx=0, v²=v₀²).",
                                        InfoBarSeverity.Warning, "Indeterminate");
                                    return;
                                }
                                else
                                {
                                    ShowStatus("Inconsistent state (Δx=0, v²≠v₀²).",
                                        InfoBarSeverity.Error, "Calculation Error");
                                    return;
                                }
                            }
                            resultSI = (v.Value * v.Value - v0.Value * v0.Value) / (2.0 * dx.Value);
                        }
                        break;

                    case VarTime: // Calculate t
                        // Equation: v = v₀ + at => t = (v - v₀) / a
                        if (v0.HasValue && v.HasValue && a.HasValue)
                        {
                            if (Math.Abs(a.Value) < Epsilon)
                            {
                                if (Math.Abs(v.Value - v0.Value) < Epsilon)
                                {
                                    ShowStatus("Time cannot be determined (a=0, v=v₀).",
                                        InfoBarSeverity.Warning, "Indeterminate");
                                    return;
                                }
                                else
                                {
                                    ShowStatus("Impossible state (a=0, v≠v₀). Check inputs.",
                                        InfoBarSeverity.Error, "Calculation Error");
                                    return;
                                }
                            }
                            double calc = (v.Value - v0.Value) / a.Value;
                            if (calc < -Epsilon)
                            {
                                ShowStatus($"Resulting time is negative ({calc.ToString("G4", CultureInfo.InvariantCulture)}).",
                                    InfoBarSeverity.Error, "Calculation Error");
                                return;
                            }
                            resultSI = Math.Max(0.0, calc);
                        }
                        // Equation: Δx = ½(v₀ + v)t => t = 2Δx / (v₀ + v)
                        else if (dx.HasValue && v0.HasValue && v.HasValue)
                        {
                            double sum = v0.Value + v.Value;
                            if (Math.Abs(sum) < Epsilon)
                            {
                                if (Math.Abs(dx.Value) < Epsilon)
                                {
                                    ShowStatus("Time cannot be determined (Δx=0, avg v=0).",
                                        InfoBarSeverity.Warning, "Indeterminate");
                                    return;
                                }
                                else
                                {
                                    ShowStatus("Impossible state (Δx≠0, avg v=0).",
                                        InfoBarSeverity.Error, "Calculation Error");
                                    return;
                                }
                            }
                            double calc = 2.0 * dx.Value / sum;
                            if (calc < -Epsilon)
                            {
                                ShowStatus($"Resulting time is negative ({calc.ToString("G4", CultureInfo.InvariantCulture)}).",
                                    InfoBarSeverity.Error, "Calculation Error");
                                return;
                            }
                            resultSI = Math.Max(0.0, calc);
                        }
                        // Requires solving quadratic equation for t:
                        // Using Δx = v₀t + ½at² OR Δx = vt - ½at²
                        else if (dx.HasValue && a.HasValue && (v0.HasValue || v.HasValue))
                        {
                            double quadA, quadB, quadC;
                            if (v0.HasValue)
                            {
                                quadA = 0.5 * a.Value;
                                quadB = v0.Value;
                                quadC = -dx.Value;
                            }
                            else
                            {
                                quadA = 0.5 * a.Value;
                                quadB = -v!.Value;
                                quadC = dx.Value;
                            }

                            // Linear case (a=0)
                            if (Math.Abs(quadA) < Epsilon)
                            {
                                if (Math.Abs(quadB) < Epsilon)
                                {
                                    if (Math.Abs(quadC) < Epsilon)
                                    {
                                        ShowStatus("Time cannot be determined (a=0, v=0, Δx=0).",
                                            InfoBarSeverity.Warning, "Indeterminate");
                                        return;
                                    }
                                    else
                                    {
                                        ShowStatus("Impossible state (a=0, v=0, Δx≠0).",
                                            InfoBarSeverity.Error, "Calculation Error");
                                        return;
                                    }
                                }
                                double calc = -quadC / quadB;
                                if (calc < -Epsilon)
                                {
                                    ShowStatus($"Resulting time is negative ({calc.ToString("G4", CultureInfo.InvariantCulture)}).",
                                        InfoBarSeverity.Error, "Calculation Error");
                                    return;
                                }
                                resultSI = Math.Max(0.0, calc);
                            }
                            else
                            {
                                double disc = quadB * quadB - 4.0 * quadA * quadC;
                                if (disc < -Epsilon)
                                {
                                    ShowStatus("No real solution for time (discriminant < 0).",
                                        InfoBarSeverity.Error, "Calculation Error");
                                    return;
                                }
                                disc = Math.Max(0.0, disc);
                                double sqrtDisc = Math.Sqrt(disc);
                                double denom = 2.0 * quadA;

                                double t1 = (-quadB + sqrtDisc) / denom;
                                double t2 = (-quadB - sqrtDisc) / denom;

                                var valid = new List<double>();
                                if (t1 >= -Epsilon) valid.Add(Math.Max(0.0, t1));
                                if (Math.Abs(t1 - t2) > Epsilon && t2 >= -Epsilon)
                                    valid.Add(Math.Max(0.0, t2));

                                if (valid.Count == 0)
                                {
                                    ShowStatus("Both calculated time roots are negative or invalid. Check inputs.",
                                        InfoBarSeverity.Error, "Calculation Error");
                                    return;
                                }

                                resultSI = valid.Min();
                                if (valid.Count == 2)
                                {
                                    infoMessage = $"Two possible positive times found ({valid.Min().ToString("G3", CultureInfo.InvariantCulture)}s, {valid.Max().ToString("G3", CultureInfo.InvariantCulture)}s). Using the smaller time.";
                                }
                            }
                        }
                        break;
                }

                // If a result was successfully calculated
                if (resultSI.HasValue)
                {
                    // Display result only if no calculation error occurred. Allows warnings to persist
                    if (!StatusInfoBar.IsOpen || StatusInfoBar.Severity < InfoBarSeverity.Error)
                    {
                        DisplayResult(targetKey, resultSI.Value);
                        // If there's an informational message (like multiple roots), show it
                        if (!string.IsNullOrEmpty(infoMessage))
                        {
                            ShowStatus(infoMessage, InfoBarSeverity.Informational, "Info");
                        }
                        // If no warning/info was shown, ensure the success message is displayed
                        else if (!warningIssued && (!StatusInfoBar.IsOpen || StatusInfoBar.Severity < InfoBarSeverity.Warning))
                        {
                            ShowStatus("Calculation successful.", InfoBarSeverity.Success, "Success");
                        }
                    }
                }
                else
                {
                    // If no applicable formula was found for the given inputs
                    if (!StatusInfoBar.IsOpen)
                    {
                        ShowStatus("Could not calculate result. Ensure the provided inputs allow calculation for the selected variable.",
                            InfoBarSeverity.Warning, "Calculation Warning");
                    }
                }
            }
            catch (DivideByZeroException)
            {
                ShowStatus("Division by zero occurred. Check inputs (e.g., time=0, Δx=0).", InfoBarSeverity.Error, "Calculation Error");
            }
            catch (OverflowException)
            {
                ShowStatus("Numerical overflow. Inputs likely result in excessively large numbers.", InfoBarSeverity.Error, "Calculation Error");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Unexpected calculation error: {ex}");
                ShowStatus($"An unexpected issue occurred ({ex.GetType().Name}). Check inputs.", InfoBarSeverity.Error, "Calculation Error");
            }
        }

        private async void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new AboutDialog();
            // IMPORTANT: Set XamlRoot before showing a ContentDialog in WinUI 3 Desktop
            dialog.XamlRoot = this.Content.XamlRoot;
            await dialog.ShowAsync();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close(); 
        }
    }
}