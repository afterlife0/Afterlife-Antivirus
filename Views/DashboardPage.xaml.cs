using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using AfterlifeWinUI.ViewModels;
using AfterlifeWinUI.Services;
using AfterlifeWinUI.Animations;
using Serilog;

namespace AfterlifeWinUI.Views;

/// <summary>
/// Dashboard page showing scan controls, stats, and activity feed
/// </summary>
public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        InitializeComponent();
        ViewModel = new DashboardViewModel();
        
        // Initialize UI from ViewModel
        ScanPathBox.Text = ViewModel.ScanPath;
        MonitoredPathsList.ItemsSource = ViewModel.MonitoredPaths;
        ActivityLogList.ItemsSource = ViewModel.ActivityLog;
        
        // Subscribe to property changes for stats updates
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        
        // Subscribe to monitored paths collection changes
        ViewModel.MonitoredPaths.CollectionChanged += MonitoredPaths_CollectionChanged;
        
        // Subscribe to theme changes
        AppSettingsService.Instance.ThemeChanged += OnThemeChanged;
        
        // Update stats and empty state
        UpdateStats();
        UpdateEmptyState();
    }

    private void OnThemeChanged(object? sender, AppTheme theme)
    {
        DispatcherQueue.TryEnqueue(ApplyTheme);
    }

    private void ApplyTheme()
    {
        var effectiveTheme = AppSettingsService.Instance.GetEffectiveTheme();
        bool isLightMode = effectiveTheme == AppTheme.Light;
        
        // Apply glass panel backgrounds based on current theme
        var glassBrush = AppSettingsService.GetGlassPanelBrush();
        var borderBrush = AppSettingsService.GetGlassBorderBrush();
        
        // Border thickness - thinner for light mode
        var panelBorderThickness = isLightMode ? new Thickness(1.0) : new Thickness(1.5);
        
        AnimQuickScan.Background = glassBrush;
        AnimQuickScan.BorderBrush = borderBrush;
        AnimQuickScan.BorderThickness = panelBorderThickness;
        
        AnimStatCard1.Background = glassBrush;
        AnimStatCard1.BorderBrush = borderBrush;
        AnimStatCard1.BorderThickness = panelBorderThickness;
        AnimStatCard2.Background = glassBrush;
        AnimStatCard2.BorderBrush = borderBrush;
        AnimStatCard2.BorderThickness = panelBorderThickness;
        AnimStatCard3.Background = glassBrush;
        AnimStatCard3.BorderBrush = borderBrush;
        AnimStatCard3.BorderThickness = panelBorderThickness;
        AnimStatCard4.Background = glassBrush;
        AnimStatCard4.BorderBrush = borderBrush;
        AnimStatCard4.BorderThickness = panelBorderThickness;
        AnimStatCard5.Background = glassBrush;
        AnimStatCard5.BorderBrush = borderBrush;
        AnimStatCard5.BorderThickness = panelBorderThickness;
        
        AnimMonitored.Background = glassBrush;
        AnimMonitored.BorderBrush = borderBrush;
        AnimMonitored.BorderThickness = panelBorderThickness;
        
        AnimActivity.Background = glassBrush;
        AnimActivity.BorderBrush = borderBrush;
        AnimActivity.BorderThickness = panelBorderThickness;
        
        // Apply text colors based on theme
        var secondaryTextBrush = AppSettingsService.GetSecondaryTextBrush();
        var tertiaryTextBrush = AppSettingsService.GetTertiaryTextBrush();
        var sectionTitleBrush = AppSettingsService.GetSectionTitleBrush();
        var statLabelBrush = AppSettingsService.GetStatLabelBrush();
        
        // Page header
        PageSubtitle.Foreground = secondaryTextBrush;
        
        // Section titles
        QuickScanTitle.Foreground = sectionTitleBrush;
        MonitoredTitle.Foreground = sectionTitleBrush;
        ActivityTitle.Foreground = sectionTitleBrush;
        
        // Stat labels
        FilesScannedLabel.Foreground = statLabelBrush;
        ThreatsBlockedLabel.Foreground = statLabelBrush;
        SignaturesLabel.Foreground = statLabelBrush;
        YaraRulesLabel.Foreground = statLabelBrush;
        AIModelsLabel.Foreground = statLabelBrush;
        
        // Hints and tertiary text
        TipText.Foreground = tertiaryTextBrush;
        MonitoredHint.Foreground = tertiaryTextBrush;
        EmptyMonitoredText.Foreground = tertiaryTextBrush;
        
        // Force Activity ListView to refresh its theme
        if (ActivityLogList.ItemsSource != null)
        {
            var items = ActivityLogList.ItemsSource;
            ActivityLogList.ItemsSource = null;
            ActivityLogList.ItemsSource = items;
        }
    }

    private void MonitoredPaths_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(UpdateEmptyState);
    }

    private void UpdateEmptyState()
    {
        EmptyMonitoredText.Visibility = ViewModel.MonitoredPaths.Count == 0 
            ? Visibility.Visible 
            : Visibility.Collapsed;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(ViewModel.FilesScanned):
                case nameof(ViewModel.ThreatsDetected):
                case nameof(ViewModel.SignatureCount):
                case nameof(ViewModel.YaraRuleCount):
                case nameof(ViewModel.AIModelCount):
                    UpdateStats();
                    break;
                case nameof(ViewModel.ScanProgress):
                    ScanProgress.Value = ViewModel.ScanProgress;
                    ScanProgressText.Text = $"{ViewModel.ScanProgress}%";
                    break;
                case nameof(ViewModel.ScanStatus):
                    ScanStatusText.Text = ViewModel.ScanStatus;
                    break;
                case nameof(ViewModel.CurrentScanFile):
                    CurrentFileText.Text = ViewModel.CurrentScanFile;
                    break;
                case nameof(ViewModel.IsScanning):
                    if (ViewModel.IsScanning)
                    {
                        ProgressPanel.Visibility = Visibility.Visible;
                        ScanButton.Content = "Stop Scan";
                    }
                    else
                    {
                        ProgressPanel.Visibility = Visibility.Collapsed;
                        ScanButton.Content = "Start Scan";
                        UpdateStats();
                    }
                    break;
                case nameof(ViewModel.ScanPath):
                    ScanPathBox.Text = ViewModel.ScanPath;
                    break;
            }
        });
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        ViewModel.OnLoaded();
        UpdateStats();
        UpdateEmptyState();
        
        // Apply theme-specific glass panel styling
        ApplyTheme();
        
        // Play entrance animations
        PlayEntranceAnimations();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        AppSettingsService.Instance.ThemeChanged -= OnThemeChanged;
        ViewModel.OnUnloaded();
    }

    private void PlayEntranceAnimations()
    {
        // Collect all animated elements in order
        var animatedElements = new List<UIElement>
        {
            AnimHeader,
            AnimQuickScan,
            AnimStatCard1,
            AnimStatCard2,
            AnimStatCard3,
            AnimStatCard4,
            AnimStatCard5,
            AnimMonitored,
            AnimActivity
        };

        // Play staggered entrance animation with smoother parameters
        PageAnimations.AnimateEntranceStaggered(
            animatedElements,
            staggerDelay: 50,   // Tighter stagger for fluid cascade
            duration: 380,      // Slightly longer for smoothness
            slideDistance: 22f  // Reduced for subtler motion
        );
    }

    private void UpdateStats()
    {
        FilesScannedText.Text = ViewModel.FilesScanned.ToString("N0");
        ThreatsBlockedText.Text = ViewModel.ThreatsDetected.ToString("N0");
        SuspiciousBlockedText.Text = ViewModel.SuspiciousDetected.ToString("N0");
        SignaturesText.Text = ViewModel.SignatureCount.ToString("N0");
        YaraRulesText.Text = ViewModel.YaraRuleCount.ToString("N0");
        AIModelsText.Text = ViewModel.AIModelCount.ToString("N0");
    }

    #region Drag and Drop

    private void Page_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Drop to scan";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsGlyphVisible = true;
            
            // Show drop overlay
            DropOverlay.Visibility = Visibility.Visible;
        }
        else
        {
            e.AcceptedOperation = DataPackageOperation.None;
        }
    }

    private void Page_DragLeave(object sender, DragEventArgs e)
    {
        // Hide drop overlay
        DropOverlay.Visibility = Visibility.Collapsed;
    }

    private async void Page_Drop(object sender, DragEventArgs e)
    {
        // Hide drop overlay
        DropOverlay.Visibility = Visibility.Collapsed;

        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            try
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var paths = items.Select(item => item.Path).ToList();
                
                Log.Information("[DashboardPage] Dropped {Count} items for scanning", paths.Count);
                ActivityLogService.Instance.Scan($"Dropped {paths.Count} item(s) for scanning");
                
                if (paths.Count > 0)
                {
                    await ViewModel.ScanFilesCommand.ExecuteAsync(paths);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[DashboardPage] Error processing dropped items");
                ActivityLogService.Instance.Error($"Failed to process dropped items: {ex.Message}");
            }
        }
    }

    #endregion

    #region Browse Buttons

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.BrowseForFolderCommand.ExecuteAsync(null);
        ScanPathBox.Text = ViewModel.ScanPath;
    }

    private async void BrowseFileButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.BrowseForFileCommand.ExecuteAsync(null);
        ScanPathBox.Text = ViewModel.ScanPath;
    }

    #endregion

    #region Scan Button

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsScanning)
        {
            // Stop the scan
            ViewModel.StopScanCommand.Execute(null);
        }
        else
        {
            // Validate path
            var scanPath = ScanPathBox.Text?.Trim();
            if (string.IsNullOrEmpty(scanPath))
            {
                ActivityLogService.Instance.Warning("Please enter a file or folder path to scan");
                return;
            }

            if (!System.IO.File.Exists(scanPath) && !System.IO.Directory.Exists(scanPath))
            {
                ActivityLogService.Instance.Error($"Path not found: {scanPath}");
                return;
            }

            // Start the scan
            ViewModel.ScanPath = scanPath;
            await ViewModel.QuickScanCommand.ExecuteAsync(null);
        }
    }

    #endregion

    #region Monitored Folders

    private async void AddFolderButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.AddMonitoredFolderCommand.ExecuteAsync(null);
        UpdateEmptyState();
    }

    private void RemovePath_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string path)
        {
            ViewModel.RemoveMonitoredFolderCommand.Execute(path);
            UpdateEmptyState();
        }
    }

    #endregion

    #region Activity Log

    private void ClearLog_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearActivityLogCommand.Execute(null);
    }

    #endregion

    /// <summary>
    /// Formats large numbers with comma separators
    /// </summary>
    public string FormatNumber(uint number)
    {
        return number.ToString("N0");
    }
}
