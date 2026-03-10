using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using AfterlifeWinUI.Animations;
using AfterlifeWinUI.Services;

namespace AfterlifeWinUI.Views;

public sealed partial class SettingsPage : Page
{
    private static readonly SolidColorBrush CyanBrush = new(Windows.UI.Color.FromArgb(255, 0, 243, 255));
    private static readonly SolidColorBrush TransparentBrush = new(Microsoft.UI.Colors.Transparent);
    
    private bool _isLoadingSettings = false; // Guard against toggle events during load

    public SettingsPage()
    {
        InitializeComponent();
        
        // Subscribe to theme changes
        AppSettingsService.Instance.ThemeChanged += OnThemeChanged;
        
        // Setup hover animations for theme buttons
        SetupThemeButtonAnimations();
    }

    private void SetupThemeButtonAnimations()
    {
        // Theme System button
        ThemeSystem.PointerEntered += (s, e) => PageAnimations.AnimateHoverEnter(ThemeSystem, 1.03f);
        ThemeSystem.PointerExited += (s, e) => PageAnimations.AnimateHoverExit(ThemeSystem);
        
        // Theme Dark button
        ThemeDark.PointerEntered += (s, e) => PageAnimations.AnimateHoverEnter(ThemeDark, 1.03f);
        ThemeDark.PointerExited += (s, e) => PageAnimations.AnimateHoverExit(ThemeDark);
        
        // Theme Light button
        ThemeLight.PointerEntered += (s, e) => PageAnimations.AnimateHoverEnter(ThemeLight, 1.03f);
        ThemeLight.PointerExited += (s, e) => PageAnimations.AnimateHoverExit(ThemeLight);
    }

    private void OnThemeChanged(object? sender, AppTheme theme)
    {
        DispatcherQueue.TryEnqueue(ApplyTheme);
    }

    private void ApplyTheme()
    {
        var effectiveTheme = AppSettingsService.Instance.GetEffectiveTheme();
        bool isLightMode = effectiveTheme == AppTheme.Light;
        
        var glassBrush = AppSettingsService.GetGlassPanelBrush();
        var borderBrush = AppSettingsService.GetGlassBorderBrush();
        var secondaryTextBrush = AppSettingsService.GetSecondaryTextBrush();
        var sectionTitleBrush = AppSettingsService.GetSectionTitleBrush();
        var primaryTextBrush = AppSettingsService.GetPrimaryTextBrush();
        
        // Border thickness - thinner for light mode
        var panelBorderThickness = isLightMode ? new Thickness(1.0) : new Thickness(1.5);
        
        // Apply glass panel backgrounds
        AnimSection0.Background = glassBrush;
        AnimSection0.BorderBrush = borderBrush;
        AnimSection0.BorderThickness = panelBorderThickness;
        
        AnimSection1.Background = glassBrush;
        AnimSection1.BorderBrush = borderBrush;
        AnimSection1.BorderThickness = panelBorderThickness;
        
        AnimSection2.Background = glassBrush;
        AnimSection2.BorderBrush = borderBrush;
        AnimSection2.BorderThickness = panelBorderThickness;
        
        AnimSection3.Background = glassBrush;
        AnimSection3.BorderBrush = borderBrush;
        AnimSection3.BorderThickness = panelBorderThickness;
        
        // Apply text colors
        PageSubtitle.Foreground = secondaryTextBrush;
        ThemeLabel.Foreground = secondaryTextBrush;
        ScanIntervalLabel.Foreground = secondaryTextBrush;
        
        // Section titles
        AppearanceTitle.Foreground = sectionTitleBrush;
        RealTimeTitle.Foreground = sectionTitleBrush;
        DetectionTitle.Foreground = sectionTitleBrush;
        ScheduledTitle.Foreground = sectionTitleBrush;
        
        // Theme button text
        SystemThemeText.Foreground = primaryTextBrush;
        DarkThemeText.Foreground = primaryTextBrush;
        LightThemeText.Foreground = primaryTextBrush;
        
        // Theme button backgrounds for light mode
        if (isLightMode)
        {
            ThemeSystem.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 0, 0, 0));
            ThemeDark.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 0, 0, 0));
            ThemeLight.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(30, 0, 0, 0));
        }
        else
        {
            ThemeSystem.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(21, 255, 255, 255));
            ThemeDark.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(21, 255, 255, 255));
            ThemeLight.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(21, 255, 255, 255));
        }
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // Load current settings
        LoadSettings();
        
        // Apply theme
        ApplyTheme();

        // Play entrance animations
        PlayEntranceAnimations();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        AppSettingsService.Instance.ThemeChanged -= OnThemeChanged;
    }

    private void LoadSettings()
    {
        _isLoadingSettings = true;
        try
        {
            var settings = AppSettingsService.Instance;

            // Update theme selector
            UpdateThemeSelection(settings.Theme);

            // Load all toggle values from settings
            ToggleRealTime.IsOn = settings.RealTimeProtection;
            ToggleScanOnCreation.IsOn = settings.ScanOnCreation;
            ToggleScanOnModification.IsOn = settings.ScanOnModification;
            
            // Detection engine toggles
            ToggleSignature.IsOn = settings.SignatureDetection;
            ToggleYara.IsOn = settings.YaraDetection;
            ToggleAI.IsOn = settings.AiHeuristic;
            
            // Scheduled scan toggle
            ToggleScheduled.IsOn = settings.ScheduledScansEnabled;
        }
        finally
        {
            _isLoadingSettings = false;
        }
    }

    private void UpdateThemeSelection(AppTheme theme)
    {
        // Reset all borders
        ThemeSystem.BorderBrush = TransparentBrush;
        ThemeDark.BorderBrush = TransparentBrush;
        ThemeLight.BorderBrush = TransparentBrush;

        // Highlight selected theme
        switch (theme)
        {
            case AppTheme.System:
                ThemeSystem.BorderBrush = CyanBrush;
                break;
            case AppTheme.Dark:
                ThemeDark.BorderBrush = CyanBrush;
                break;
            case AppTheme.Light:
                ThemeLight.BorderBrush = CyanBrush;
                break;
        }
    }

    private void ThemeSystem_Click(object sender, PointerRoutedEventArgs e)
    {
        PageAnimations.AnimateBounce(ThemeSystem);
        AppSettingsService.Instance.Theme = AppTheme.System;
        UpdateThemeSelection(AppTheme.System);
        ActivityLogService.Instance.Info("Theme changed to System - applied immediately");
    }

    private void ThemeDark_Click(object sender, PointerRoutedEventArgs e)
    {
        PageAnimations.AnimateBounce(ThemeDark);
        AppSettingsService.Instance.Theme = AppTheme.Dark;
        UpdateThemeSelection(AppTheme.Dark);
        ActivityLogService.Instance.Info("Theme changed to Dark - applied immediately");
    }

    private void ThemeLight_Click(object sender, PointerRoutedEventArgs e)
    {
        PageAnimations.AnimateBounce(ThemeLight);
        AppSettingsService.Instance.Theme = AppTheme.Light;
        UpdateThemeSelection(AppTheme.Light);
        ActivityLogService.Instance.Info("Theme changed to Light - applied immediately");
    }

    private void ToggleRealTime_Toggled(object sender, RoutedEventArgs e)
    {
        // Don't save during initial load
        if (_isLoadingSettings) return;
        
        bool newValue = ToggleRealTime.IsOn;
        
        // Setting the property auto-saves to disk
        AppSettingsService.Instance.RealTimeProtection = newValue;

        if (newValue)
        {
            ActivityLogService.Instance.Success("Real-time protection enabled");
        }
        else
        {
            ActivityLogService.Instance.Warning("Real-time protection disabled");
        }
    }

    private void ToggleScanOnCreation_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        
        AppSettingsService.Instance.ScanOnCreation = ToggleScanOnCreation.IsOn;

        if (ToggleScanOnCreation.IsOn)
            ActivityLogService.Instance.Info("Scan on file creation enabled");
        else
            ActivityLogService.Instance.Warning("Scan on file creation disabled");
    }

    private void ToggleScanOnModification_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        
        AppSettingsService.Instance.ScanOnModification = ToggleScanOnModification.IsOn;

        if (ToggleScanOnModification.IsOn)
            ActivityLogService.Instance.Info("Scan on file modification enabled");
        else
            ActivityLogService.Instance.Warning("Scan on file modification disabled");
    }

    private void ToggleSignature_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        
        AppSettingsService.Instance.SignatureDetection = ToggleSignature.IsOn;

        if (ToggleSignature.IsOn)
            ActivityLogService.Instance.Info("Signature detection enabled");
        else
            ActivityLogService.Instance.Warning("Signature detection disabled");
    }

    private void ToggleYara_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        
        AppSettingsService.Instance.YaraDetection = ToggleYara.IsOn;

        if (ToggleYara.IsOn)
            ActivityLogService.Instance.Info("YARA detection enabled");
        else
            ActivityLogService.Instance.Warning("YARA detection disabled");
    }

    private void ToggleAI_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        
        AppSettingsService.Instance.AiHeuristic = ToggleAI.IsOn;

        if (ToggleAI.IsOn)
            ActivityLogService.Instance.Info("AI heuristic enabled");
        else
            ActivityLogService.Instance.Warning("AI heuristic disabled");
    }

    private void ToggleScheduled_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isLoadingSettings) return;
        
        AppSettingsService.Instance.ScheduledScansEnabled = ToggleScheduled.IsOn;

        if (ToggleScheduled.IsOn)
            ActivityLogService.Instance.Info("Scheduled scans enabled");
        else
            ActivityLogService.Instance.Warning("Scheduled scans disabled");
    }

    private void PlayEntranceAnimations()
    {
        var animatedElements = new List<UIElement>
        {
            AnimHeader,
            AnimSection0,
            AnimSection1,
            AnimSection2,
            AnimSection3
        };

        // Use smoother animation parameters
        PageAnimations.AnimateEntranceStaggered(
            animatedElements,
            staggerDelay: 60,    // Slightly increased for smoother cascade
            duration: 400,       // Longer duration for more fluid feel
            slideDistance: 24f   // Reduced distance for subtler motion
        );
    }
}
