using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AfterlifeWinUI.Animations;
using AfterlifeWinUI.Services;

namespace AfterlifeWinUI.Views;

public sealed partial class SystemStatusPage : Page
{
    public SystemStatusPage()
    {
        InitializeComponent();
        
        // Subscribe to theme changes
        AppSettingsService.Instance.ThemeChanged += OnThemeChanged;
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
        
        // Border thickness - thinner for light mode
        var panelBorderThickness = isLightMode ? new Thickness(1.0) : new Thickness(1.5);
        
        // Apply glass panel backgrounds
        AnimService1.Background = glassBrush;
        AnimService1.BorderBrush = borderBrush;
        AnimService1.BorderThickness = panelBorderThickness;
        
        AnimService2.Background = glassBrush;
        AnimService2.BorderBrush = borderBrush;
        AnimService2.BorderThickness = panelBorderThickness;
        
        AnimEngine1.Background = glassBrush;
        AnimEngine1.BorderBrush = borderBrush;
        AnimEngine1.BorderThickness = panelBorderThickness;
        
        AnimEngine2.Background = glassBrush;
        AnimEngine2.BorderBrush = borderBrush;
        AnimEngine2.BorderThickness = panelBorderThickness;
        
        AnimEngine3.Background = glassBrush;
        AnimEngine3.BorderBrush = borderBrush;
        AnimEngine3.BorderThickness = panelBorderThickness;
        
        // Apply text colors
        PageSubtitle.Foreground = secondaryTextBrush;
        
        // Service titles
        CoreServiceTitle.Foreground = sectionTitleBrush;
        AIEngineTitle.Foreground = sectionTitleBrush;
        
        // Engine labels
        SignatureEngineLabel.Foreground = secondaryTextBrush;
        SignaturesLabel.Foreground = secondaryTextBrush;
        YaraEngineLabel.Foreground = secondaryTextBrush;
        RulesLabel.Foreground = secondaryTextBrush;
        AIHeuristicLabel.Foreground = secondaryTextBrush;
        ModelLabel.Foreground = secondaryTextBrush;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateStats();
        ApplyTheme();
        PlayEntranceAnimations();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        AppSettingsService.Instance.ThemeChanged -= OnThemeChanged;
    }

    private void UpdateStats()
    {
        var stats = MalwareScanner.Instance.GetStats();
        SignatureCount.Text = stats.SignatureCount.ToString("N0");
        YaraCount.Text = stats.YaraRuleCount.ToString("N0");
        AIModelCount.Text = stats.AIModelCount.ToString("N0");
    }

    private void PlayEntranceAnimations()
    {
        var animatedElements = new List<UIElement>
        {
            AnimHeader,
            AnimService1,
            AnimService2,
            AnimEngine1,
            AnimEngine2,
            AnimEngine3
        };

        PageAnimations.AnimateEntranceStaggered(
            animatedElements,
            staggerDelay: 50,
            duration: 380,
            slideDistance: 22f
        );
    }
}
