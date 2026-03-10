using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using AfterlifeWinUI.Services;
using AfterlifeWinUI.Animations;
using System.Collections.Specialized;

namespace AfterlifeWinUI.Views;

public sealed partial class ActivityPage : Page
{
    public ActivityPage()
    {
        InitializeComponent();
        
        // Bind to shared activity log service
        ActivityList.ItemsSource = ActivityLogService.Instance.Entries;
        
        // Subscribe to collection changes
        ActivityLogService.Instance.Entries.CollectionChanged += OnEntriesChanged;
        ActivityLogService.Instance.StatsChanged += OnStatsChanged;
        
        // Subscribe to theme changes
        AppSettingsService.Instance.ThemeChanged += OnThemeChanged;
        
        // Update UI state
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
        var tertiaryTextBrush = AppSettingsService.GetTertiaryTextBrush();
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
        
        // Stat labels
        TotalEventsLabel.Foreground = statLabelBrush;
        ThreatsLabel.Foreground = statLabelBrush;
        ScansLabel.Foreground = statLabelBrush;
        WarningsLabel.Foreground = statLabelBrush;
        
        // Empty state
        EmptyStateTitle.Foreground = secondaryTextBrush;
        EmptyStateSubtitle.Foreground = tertiaryTextBrush;
        
        // Force ListView to refresh its theme
        if (ActivityList.ItemsSource != null)
        {
            var items = ActivityList.ItemsSource;
            ActivityList.ItemsSource = null;
            ActivityList.ItemsSource = items;
        }
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

    private void OnEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateUI);
    }

    private void OnStatsChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateStats);
    }

    private void UpdateUI()
    {
        var hasEntries = ActivityLogService.Instance.Entries.Count > 0;
        
        EmptyState.Visibility = hasEntries ? Visibility.Collapsed : Visibility.Visible;
        ActivityList.Visibility = hasEntries ? Visibility.Visible : Visibility.Collapsed;
        
        UpdateStats();
    }

    private void UpdateStats()
    {
        TotalEntries.Text = ActivityLogService.Instance.TotalEvents.ToString("N0");
        ThreatCount.Text = ActivityLogService.Instance.ThreatCount.ToString("N0");
        ScanCount.Text = ActivityLogService.Instance.ScanCount.ToString("N0");
        WarningCount.Text = ActivityLogService.Instance.WarningCount.ToString("N0");
    }

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        ActivityLogService.Instance.Clear();
        UpdateUI();
    }
}
