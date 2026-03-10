using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using AfterlifeWinUI.ViewModels;
using AfterlifeWinUI.Views;
using AfterlifeWinUI.Services;
using AfterlifeWinUI.Animations;
using Windows.Graphics;
using Windows.UI;
using WinRT.Interop;
using Serilog;
using Microsoft.UI.Dispatching;
using System.Runtime.InteropServices;
using System.IO;
using System.Drawing;

namespace AfterlifeWinUI;

/// <summary>
/// Main application window with frosted glass UI and autonomous neon orbs
/// </summary>
public sealed partial class MainWindow : Window
{
    // Win32 imports for setting window icon
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    
    [DllImport("user32.dll")]
    private static extern int DestroyIcon(IntPtr hIcon);
    
    // Win32 import for rounded corners (Windows 11)
    [DllImport("dwmapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void DwmSetWindowAttribute(IntPtr hwnd, DWMWINDOWATTRIBUTE attribute, ref int pvAttribute, uint cbAttribute);

    private enum DWMWINDOWATTRIBUTE
    {
        DWMWA_WINDOW_CORNER_PREFERENCE = 33
    }

    private enum DWM_WINDOW_CORNER_PREFERENCE
    {
        DWMWCP_DEFAULT = 0,
        DWMWCP_DONOTROUND = 1,
        DWMWCP_ROUND = 2,
        DWMWCP_ROUNDSMALL = 3
    }
    
    private const uint WM_SETICON = 0x0080;
    private const int ICON_SMALL = 0;
    private const int ICON_BIG = 1;
    
    // Store icon handles to prevent garbage collection
    private IntPtr _smallIconHandle = IntPtr.Zero;
    private IntPtr _bigIconHandle = IntPtr.Zero;

    public MainViewModel ViewModel { get; }
    private AppWindow? _appWindow;
    private Button? _activeNavButton;
    private IntPtr _hwnd;
    
    // Autonomous orb animation
    private DispatcherQueueTimer? _orbAnimationTimer;
    private double _animationTime = 0;
    private double _windowWidth = 1200;
    private double _windowHeight = 800;
    
    // Window settings save debounce timer
    private DispatcherQueueTimer? _saveSettingsTimer;
    private bool _pendingSave = false;
    
    // Random phases for organic movement - one for each orb
    private readonly Random _random = new();
    private double _orangePhase;
    private double _cyan1Phase;
    private double _cyan2Phase;
    private double _purple1Phase;
    private double _purple2Phase;
    private double _magentaPhase;
    private double _greenPhase;
    private double _bluePhase;
    
    // Current theme for styling
    private AppTheme _currentTheme = AppTheme.Dark;

    public MainWindow()
    {
        InitializeComponent();

        ViewModel = new MainViewModel();
        ViewModel.Initialize();

        // Initialize random phases for each orb for organic movement
        _orangePhase = _random.NextDouble() * Math.PI * 2;
        _cyan1Phase = _random.NextDouble() * Math.PI * 2;
        _cyan2Phase = _random.NextDouble() * Math.PI * 2;
        _purple1Phase = _random.NextDouble() * Math.PI * 2;
        _purple2Phase = _random.NextDouble() * Math.PI * 2;
        _magentaPhase = _random.NextDouble() * Math.PI * 2;
        _greenPhase = _random.NextDouble() * Math.PI * 2;
        _bluePhase = _random.NextDouble() * Math.PI * 2;

        // Setup window customization
        SetupWindow();

        // Setup debounce timer for window settings save (500ms delay)
        _saveSettingsTimer = DispatcherQueue.CreateTimer();
        _saveSettingsTimer.Interval = TimeSpan.FromMilliseconds(500);
        _saveSettingsTimer.IsRepeating = false;
        _saveSettingsTimer.Tick += OnSaveSettingsTimerTick;

        // Apply saved theme
        _currentTheme = AppSettingsService.Instance.GetEffectiveTheme();
        ApplyTheme(_currentTheme);
        
        // Subscribe to theme changes for immediate switching
        AppSettingsService.Instance.ThemeChanged += OnThemeChanged;

        // Start autonomous orb animation
        StartOrbAnimation();

        // Navigate to dashboard by default
        ContentFrame.Navigate(typeof(DashboardPage));
        _activeNavButton = NavDashboard;
        UpdateNavButtonStyles();

        // Handle window closing
        Closed += OnWindowClosed;
        
        // Handle window size changed
        SizeChanged += OnWindowSizeChanged;
        
        Log.Information("MainWindow initialized");
    }

    private void OnThemeChanged(object? sender, AppTheme theme)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            _currentTheme = AppSettingsService.Instance.GetEffectiveTheme();
            ApplyTheme(_currentTheme);
            UpdateNavButtonStyles();
            Log.Information("Theme applied immediately: {Theme}", _currentTheme);
        });
    }

    private void ApplyTheme(AppTheme effectiveTheme)
    {
        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = effectiveTheme == AppTheme.Light 
                ? ElementTheme.Light 
                : ElementTheme.Dark;
        }
        
        UpdateThemeColors(effectiveTheme);
    }

    private void UpdateThemeColors(AppTheme theme)
    {
        if (theme == AppTheme.Light)
        {
            // Light mode - Windows 11 inspired light background with subtle warmth
            RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 243, 243, 243));
            
            // Sidebar with frosted white glass - synced transparency with panels
            SidebarBorder.Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Windows.UI.Color.FromArgb(100, 255, 255, 255), Offset = 0 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(85, 250, 250, 252), Offset = 0.3 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(90, 248, 248, 250), Offset = 0.7 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(95, 252, 252, 255), Offset = 1 }
                }
            };
            
            // Light mode border - subtle shadow effect, thinner
            SidebarBorder.BorderBrush = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Windows.UI.Color.FromArgb(35, 0, 0, 0), Offset = 0 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(20, 0, 0, 0), Offset = 0.5 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(30, 0, 0, 0), Offset = 1 }
                }
            };
            
            // Sidebar text - DARK for white/light sidebar for visibility
            BrandAfter.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 30, 35));
            MenuLabel.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 100, 110));
            SigLabel.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 80, 80, 90));
            YaraLabel.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 80, 80, 90));
            
            // Make orbs visible in light mode
            SetOrbsForLightMode();
        }
        else
        {
            // Dark mode colors with liquid glass effect
            RootGrid.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 8, 8, 12));
            
            // Sidebar semi-transparent dark glass - synced with panel transparency
            SidebarBorder.Background = new LinearGradientBrush
            {
                StartPoint = new Windows.Foundation.Point(0, 0),
                EndPoint = new Windows.Foundation.Point(1, 1),
                GradientStops =
                {
                    new GradientStop { Color = Windows.UI.Color.FromArgb(35, 30, 30, 50), Offset = 0 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(20, 255, 255, 255), Offset = 0.15 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(12, 255, 255, 255), Offset = 0.5 },
                    new GradientStop { Color = Windows.UI.Color.FromArgb(30, 20, 20, 40), Offset = 1 }
                }
            };
            
            // Dark mode border - subtle white border only
            SidebarBorder.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(40, 255, 255, 255));
            
            // Sidebar text - light for dark background - brighter colors
            BrandAfter.Foreground = new SolidColorBrush(Colors.White);
            MenuLabel.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 160, 160, 175));
            SigLabel.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 160, 160, 175));
            YaraLabel.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 160, 160, 175));
            
            // Full neon orbs for dark mode
            SetOrbsForDarkMode();
        }
    }

    private void SetOrbsForLightMode()
    {
        // Keep orbs visible in light mode - slightly reduced but still visible
        OrbOrange.Opacity = 0.5;
        OrbCyan1.Opacity = 0.5;
        OrbCyan2.Opacity = 0.4;
        OrbPurple1.Opacity = 0.35;
        OrbPurple2.Opacity = 0.3;
        OrbMagenta.Opacity = 0.25;
        OrbGreen.Opacity = 0.3;
        OrbBlue.Opacity = 0.35;
    }

    private void SetOrbsForDarkMode()
    {
        // Full vibrant orbs for dark mode
        OrbOrange.Opacity = 1.0;
        OrbCyan1.Opacity = 1.0;
        OrbCyan2.Opacity = 1.0;
        OrbPurple1.Opacity = 1.0;
        OrbPurple2.Opacity = 1.0;
        OrbMagenta.Opacity = 1.0;
        OrbGreen.Opacity = 1.0;
        OrbBlue.Opacity = 1.0;
    }

    private static Windows.UI.Color ParseColor(string hex)
    {
        hex = hex.TrimStart('#');
        byte a = Convert.ToByte(hex.Substring(0, 2), 16);
        byte r = Convert.ToByte(hex.Substring(2, 2), 16);
        byte g = Convert.ToByte(hex.Substring(4, 2), 16);
        byte b = Convert.ToByte(hex.Substring(6, 2), 16);
        return Windows.UI.Color.FromArgb(a, r, g, b);
    }

    private void SetupWindow()
    {
        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow != null)
        {
            // Apply rounded corners (Windows 11)
            ApplyRoundedCorners();
            
            // Set window icon from Assets folder using Win32 (supports PNG)
            SetWindowIconFromPng();
            
            // Extend content into title bar but keep native caption buttons visible
            _appWindow.TitleBar.ExtendsContentIntoTitleBar = true;
            
            // IMPORTANT: Set the drag region element so native caption buttons work correctly
            // This tells the system which XAML element is the draggable title bar area
            SetTitleBar(TitleBarDragRegion);
            
            // Style the native buttons to be semi-transparent
            _appWindow.TitleBar.ButtonBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            _appWindow.TitleBar.ButtonInactiveBackgroundColor = Windows.UI.Color.FromArgb(0, 0, 0, 0);
            _appWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(30, 255, 255, 255);
            _appWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(50, 255, 255, 255);
            _appWindow.TitleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            _appWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 128, 128, 128);
            _appWindow.TitleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
            _appWindow.TitleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 200, 200, 200);

            // Load and apply saved window settings
            var settings = WindowSettingsService.Load();
            WindowSettingsService.Apply(_appWindow, settings);

            _appWindow.Title = "AFTERLiFE - Protection Dashboard";
            _appWindow.Changed += OnAppWindowChanged;
            
            Log.Debug("Window configured with native title bar buttons and rounded corners");
        }
    }

    /// <summary>
    /// Apply rounded corners to the window using Windows 11 DWM API
    /// </summary>
    private void ApplyRoundedCorners()
    {
        try
        {
            // DWMWCP_ROUND = 2 for standard rounded corners
            // DWMWCP_ROUNDSMALL = 3 for smaller rounded corners
            int preference = (int)DWM_WINDOW_CORNER_PREFERENCE.DWMWCP_ROUND;
            DwmSetWindowAttribute(
                _hwnd,
                DWMWINDOWATTRIBUTE.DWMWA_WINDOW_CORNER_PREFERENCE,
                ref preference,
                sizeof(int));
            
            Log.Debug("Applied rounded corners to window");
        }
        catch (Exception ex)
        {
            // This may fail on Windows 10 or older - that's OK
            Log.Debug("Could not apply rounded corners (may not be supported): {Message}", ex.Message);
        }
    }
    
    /// <summary>
    /// Set the window icon from a PNG file using Win32 API
    /// </summary>
    private void SetWindowIconFromPng()
    {
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "icon.png");
            if (!File.Exists(iconPath))
            {
                Log.Warning("Window icon not found at: {IconPath}", iconPath);
                return;
            }

            using var bitmap = new Bitmap(iconPath);
            
            // Create small icon (16x16 for taskbar, title bar)
            using var smallBitmap = new Bitmap(bitmap, new System.Drawing.Size(16, 16));
            _smallIconHandle = smallBitmap.GetHicon();
            
            // Create big icon (32x32 for Alt+Tab, etc.)
            using var bigBitmap = new Bitmap(bitmap, new System.Drawing.Size(32, 32));
            _bigIconHandle = bigBitmap.GetHicon();
            
            // Set both icons using Win32
            SendMessage(_hwnd, WM_SETICON, (IntPtr)ICON_SMALL, _smallIconHandle);
            SendMessage(_hwnd, WM_SETICON, (IntPtr)ICON_BIG, _bigIconHandle);
            
            Log.Debug("Window icon set from PNG: {IconPath}", iconPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set window icon from PNG");
        }
    }

    private void StartOrbAnimation()
    {
        _orbAnimationTimer = DispatcherQueue.CreateTimer();
        _orbAnimationTimer.Interval = TimeSpan.FromMilliseconds(16);
        _orbAnimationTimer.Tick += OnOrbAnimationTick;
        _orbAnimationTimer.Start();
    }

    private void OnOrbAnimationTick(DispatcherQueueTimer sender, object args)
    {
        _animationTime += 0.016;
        
        // Orange Orb - Top left, slow organic movement like Liquid Glass Shader reference
        double orangeBaseX = -100;
        double orangeBaseY = -50;
        double orangeX = orangeBaseX + 
            Math.Sin(_animationTime * 0.3 + _orangePhase) * 120 +
            Math.Sin(_animationTime * 0.12) * 40;
        double orangeY = orangeBaseY + 
            Math.Cos(_animationTime * 0.25 + _orangePhase) * 100 +
            Math.Cos(_animationTime * 0.1) * 30;
        Canvas.SetLeft(OrbOrange, orangeX);
        Canvas.SetTop(OrbOrange, orangeY);
        
        // Cyan Orb 1 - Large, slow flowing movement across full window
        double cyan1X = (_windowWidth * 0.3) + 
            Math.Sin(_animationTime * 0.25 + _cyan1Phase) * (_windowWidth * 0.35) +
            Math.Cos(_animationTime * 0.15) * 80;
        double cyan1Y = (_windowHeight * 0.2) + 
            Math.Cos(_animationTime * 0.2 + _cyan1Phase) * (_windowHeight * 0.3) +
            Math.Sin(_animationTime * 0.12) * 60;
        Canvas.SetLeft(OrbCyan1, cyan1X);
        Canvas.SetTop(OrbCyan1, cyan1Y);
        
        // Cyan Orb 2 - Medium, different rhythm
        double cyan2X = (_windowWidth * 0.1) + 
            Math.Sin(_animationTime * 0.35 + _cyan2Phase) * (_windowWidth * 0.4) +
            Math.Sin(_animationTime * 0.18) * 60;
        double cyan2Y = (_windowHeight * 0.4) + 
            Math.Cos(_animationTime * 0.28 + _cyan2Phase) * (_windowHeight * 0.25) +
            Math.Cos(_animationTime * 0.14) * 50;
        Canvas.SetLeft(OrbCyan2, cyan2X);
        Canvas.SetTop(OrbCyan2, cyan2Y);
        
        // Purple Orb 1 - Large, sweeping motion
        double purple1X = (_windowWidth * 0.5) + 
            Math.Cos(_animationTime * 0.22 + _purple1Phase) * (_windowWidth * 0.35) +
            Math.Sin(_animationTime * 0.13) * 70;
        double purple1Y = (_windowHeight * 0.35) + 
            Math.Sin(_animationTime * 0.18 + _purple1Phase) * (_windowHeight * 0.35) +
            Math.Cos(_animationTime * 0.1) * 55;
        Canvas.SetLeft(OrbPurple1, purple1X);
        Canvas.SetTop(OrbPurple1, purple1Y);
        
        // Purple Orb 2 - Medium, orbital pattern
        double purple2Radius = 120 + Math.Sin(_animationTime * 0.25) * 40;
        double purple2X = (_windowWidth * 0.15) + 
            Math.Cos(_animationTime * 0.3 + _purple2Phase) * purple2Radius +
            Math.Sin(_animationTime * 0.2) * (_windowWidth * 0.2);
        double purple2Y = (_windowHeight * 0.6) + 
            Math.Sin(_animationTime * 0.25 + _purple2Phase) * (purple2Radius * 0.8) +
            Math.Cos(_animationTime * 0.16) * 80;
        Canvas.SetLeft(OrbPurple2, purple2X);
        Canvas.SetTop(OrbPurple2, purple2Y);
        
        // Magenta Orb - Accent, flowing diagonal
        double magentaX = (_windowWidth * 0.4) + 
            Math.Sin(_animationTime * 0.28 + _magentaPhase) * (_windowWidth * 0.3) +
            Math.Cos(_animationTime * 0.17) * 65;
        double magentaY = (_windowHeight * 0.25) + 
            Math.Cos(_animationTime * 0.23 + _magentaPhase) * (_windowHeight * 0.3) +
            Math.Sin(_animationTime * 0.11) * 50;
        Canvas.SetLeft(OrbMagenta, magentaX);
        Canvas.SetTop(OrbMagenta, magentaY);
        
        // Green Orb - Accent, wave motion
        double greenX = (_windowWidth * 0.25) + 
            Math.Sin(_animationTime * 0.2 + _greenPhase) * (_windowWidth * 0.35) +
            Math.Cos(_animationTime * 0.32) * 55;
        double greenY = (_windowHeight * 0.55) + 
            Math.Sin(_animationTime * 0.26 + _greenPhase * 0.7) * (_windowHeight * 0.25) +
            Math.Cos(_animationTime * 0.15) * 40;
        Canvas.SetLeft(OrbGreen, greenX);
        Canvas.SetTop(OrbGreen, greenY);
        
        // Blue Orb - Deep accent, slow drift
        double blueX = (_windowWidth * 0.05) + 
            Math.Sin(_animationTime * 0.18 + _bluePhase) * (_windowWidth * 0.3) +
            Math.Cos(_animationTime * 0.22) * 70;
        double blueY = (_windowHeight * 0.15) + 
            Math.Cos(_animationTime * 0.15 + _bluePhase) * (_windowHeight * 0.35) +
            Math.Sin(_animationTime * 0.19) * 60;
        Canvas.SetLeft(OrbBlue, blueX);
        Canvas.SetTop(OrbBlue, blueY);
    }

    private void OnWindowSizeChanged(object sender, WindowSizeChangedEventArgs e)
    {
        _windowWidth = e.Size.Width;
        _windowHeight = e.Size.Height;
        
        // Schedule a debounced save
        ScheduleWindowSettingsSave();
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (args.DidPositionChange || args.DidSizeChange || args.DidPresenterChange)
        {
            var presenter = sender.Presenter as OverlappedPresenter;
            if (presenter?.State != OverlappedPresenterState.Minimized)
            {
                // Schedule a debounced save instead of saving immediately
                ScheduleWindowSettingsSave();
            }
        }
    }

    /// <summary>
    /// Schedule a debounced save of window settings
    /// </summary>
    private void ScheduleWindowSettingsSave()
    {
        _pendingSave = true;
        
        // Reset the timer - this debounces rapid changes
        _saveSettingsTimer?.Stop();
        _saveSettingsTimer?.Start();
    }

    /// <summary>
    /// Timer callback to actually save window settings
    /// </summary>
    private void OnSaveSettingsTimerTick(DispatcherQueueTimer sender, object args)
    {
        sender.Stop();
        
        if (_pendingSave && _appWindow != null)
        {
            _pendingSave = false;
            WindowSettingsService.Save(_appWindow);
            Log.Debug("Window settings saved (debounced)");
        }
    }

    private void OnWindowClosed(object sender, WindowEventArgs e)
    {
        AppSettingsService.Instance.ThemeChanged -= OnThemeChanged;
        
        // Stop timers
        _orbAnimationTimer?.Stop();
        _saveSettingsTimer?.Stop();
        
        // Cleanup icon handles
        if (_smallIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_smallIconHandle);
            _smallIconHandle = IntPtr.Zero;
        }
        if (_bigIconHandle != IntPtr.Zero)
        {
            DestroyIcon(_bigIconHandle);
            _bigIconHandle = IntPtr.Zero;
        }
        
        // Always save window settings on close (immediate, not debounced)
        if (_appWindow != null)
        {
            _appWindow.Changed -= OnAppWindowChanged;
            WindowSettingsService.Save(_appWindow);
            Log.Debug("Window settings saved on close");
        }
        
        ViewModel.Shutdown();
        App.Shutdown();
        
        Log.Information("MainWindow closed");
    }

    #region Navigation

    private void NavDashboard_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(typeof(DashboardPage), sender as Button);
    }

    private void NavThreats_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(typeof(ThreatsPage), sender as Button);
    }

    private void NavActivity_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(typeof(ActivityPage), sender as Button);
    }

    private void NavSystemStatus_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(typeof(SystemStatusPage), sender as Button);
    }

    private void NavSettings_Click(object sender, RoutedEventArgs e)
    {
        NavigateTo(typeof(SettingsPage), sender as Button);
    }

    private async void NavigateTo(Type pageType, Button? navButton)
    {
        if (ContentFrame.Content?.GetType() == pageType) return;

        Log.Debug("Navigating to {PageType}", pageType.Name);

        // Animate nav button press feedback
        if (navButton != null)
        {
            PageAnimations.AnimateBounce(navButton);
        }

        if (ContentFrame.Content is UIElement currentPage)
        {
            PageAnimations.AnimatePageExit(currentPage, 120);
            await Task.Delay(80); // Reduced delay for snappier transitions
        }

        ContentFrame.Navigate(pageType, null, new SuppressNavigationTransitionInfo());

        _activeNavButton = navButton;
        UpdateNavButtonStyles();
    }

    private void UpdateNavButtonStyles()
    {
        var navButtons = new[] { NavDashboard, NavThreats, NavActivity, NavSystemStatus, NavSettings };
        foreach (var btn in navButtons)
        {
            btn.Tag = btn == _activeNavButton ? "Active" : null;
            UpdateNavButtonVisual(btn, btn == _activeNavButton);
        }
    }

    private void UpdateNavButtonVisual(Button button, bool isActive)
    {
        // Determine text colors based on theme
        bool isLightMode = _currentTheme == AppTheme.Light;
        
        if (isActive)
        {
            button.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(35, 0, 243, 255));
            button.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 200, 220));
            button.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 200, 220));
            button.BorderThickness = new Thickness(2, 0, 0, 0);
        }
        else
        {
            button.Background = new SolidColorBrush(Colors.Transparent);
            // Dark text for light sidebar, light text for dark sidebar
            if (isLightMode)
            {
                button.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 80, 80, 90));
            }
            else
            {
                // Brighter text for dark mode sidebar
                button.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 200, 200, 210));
            }
            button.BorderBrush = new SolidColorBrush(Colors.Transparent);
            button.BorderThickness = new Thickness(0);
        }
    }

    #endregion

    #region Window Controls

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        if (_appWindow != null)
        {
            var presenter = _appWindow.Presenter as OverlappedPresenter;
            presenter?.Minimize();
        }
    }

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        if (_appWindow != null)
        {
            var presenter = _appWindow.Presenter as OverlappedPresenter;
            if (presenter != null)
            {
                if (presenter.State == OverlappedPresenterState.Maximized)
                {
                    presenter.Restore();
                }
                else
                {
                    presenter.Maximize();
                }
            }
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion
}

/// <summary>
/// Converter for sidebar toggle chevron direction
/// </summary>
public class BoolToChevronGlyphConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isExpanded)
        {
            return isExpanded ? "\uE76B" : "\uE76C";
        }
        return "\uE76B";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

/// <summary>
/// Inverse boolean to visibility converter
/// </summary>
public class InverseBoolToVisibilityConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
