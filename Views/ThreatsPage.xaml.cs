using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AfterlifeWinUI.Services;
using AfterlifeWinUI.Animations;
using System.Collections.Specialized;

namespace AfterlifeWinUI.Views;

public sealed partial class ThreatsPage : Page
{
    public ThreatsPage()
    {
        InitializeComponent();
        
        // Bind to threat history service
        ThreatsList.ItemsSource = ThreatHistoryService.Instance.ActiveThreats;
        
        // Subscribe to changes
        ThreatHistoryService.Instance.ActiveThreats.CollectionChanged += OnThreatsChanged;
        ThreatHistoryService.Instance.ThreatsChanged += OnStatsChanged;
        
        // Subscribe to theme changes
        AppSettingsService.Instance.ThemeChanged += OnThemeChanged;
        
        // Update UI
        UpdateUI();
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
        var statLabelBrush = AppSettingsService.GetStatLabelBrush();
        
        // Border thickness - thinner for light mode
        var panelBorderThickness = isLightMode ? new Thickness(1.0) : new Thickness(1.5);
        
        // Apply glass panel backgrounds
        AnimStats.Background = glassBrush;
        AnimStats.BorderBrush = borderBrush;
        AnimStats.BorderThickness = panelBorderThickness;
        
        AnimContent.Background = glassBrush;
        AnimContent.BorderBrush = borderBrush;
        AnimContent.BorderThickness = panelBorderThickness;
        
        // Apply text colors
        PageSubtitle.Foreground = secondaryTextBrush;
        EmptyStateSubtitle.Foreground = secondaryTextBrush;
        
        // Stat labels
        TotalLabel.Foreground = statLabelBrush;
        ActiveLabel.Foreground = statLabelBrush;
        QuarantinedLabel.Foreground = statLabelBrush;
        DeletedLabel.Foreground = statLabelBrush;
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyTheme();
        PlayEntranceAnimations();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        AppSettingsService.Instance.ThemeChanged -= OnThemeChanged;
    }

    private void PlayEntranceAnimations()
    {
        var animatedElements = new List<UIElement>
        {
            AnimHeader,
            AnimStats,
            AnimContent
        };

        PageAnimations.AnimateEntranceStaggered(
            animatedElements,
            staggerDelay: 55,
            duration: 380,
            slideDistance: 22f
        );
    }

    private void OnThreatsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateUI);
    }

    private void OnStatsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateStats);
    }

    private void UpdateUI()
    {
        var hasThreats = ThreatHistoryService.Instance.ActiveThreats.Count > 0;
        
        EmptyState.Visibility = hasThreats ? Visibility.Collapsed : Visibility.Visible;
        ThreatsList.Visibility = hasThreats ? Visibility.Visible : Visibility.Collapsed;
        
        UpdateStats();
    }

    private void UpdateStats()
    {
        var service = ThreatHistoryService.Instance;
        
        TotalCount.Text = service.TotalCount.ToString("N0");
        ActiveCount.Text = service.ActiveCount.ToString("N0");
        QuarantinedCount.Text = service.QuarantinedCount.ToString("N0");
        DeletedCount.Text = service.DeletedCount.ToString("N0");
    }

    private void Quarantine_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string threatId)
        {
            ThreatHistoryService.Instance.QuarantineThreat(threatId);
            ActivityLogService.Instance.Success("Threat quarantined successfully");
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string threatId)
        {
            ThreatHistoryService.Instance.DeleteThreat(threatId);
            ActivityLogService.Instance.Success("Threat deleted successfully");
        }
    }

    private void Ignore_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string threatId)
        {
            ThreatHistoryService.Instance.IgnoreThreat(threatId);
            ActivityLogService.Instance.Warning("Threat ignored - file was not removed");
        }
    }

    private void QuarantineAll_Click(object sender, RoutedEventArgs e)
    {
        int count = ThreatHistoryService.Instance.QuarantineAll();
        if (count > 0)
        {
            ActivityLogService.Instance.Success($"Quarantined {count} threats");
        }
    }

    private void ClearResolved_Click(object sender, RoutedEventArgs e)
    {
        ThreatHistoryService.Instance.ClearResolved();
        ActivityLogService.Instance.Info("Cleared resolved threats from history");
    }
}
