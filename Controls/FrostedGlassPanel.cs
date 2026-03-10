using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.Graphics.Canvas.UI.Xaml;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Numerics;
using Windows.UI;

namespace AfterlifeWinUI.Controls;

/// <summary>
/// A custom control that renders a frosted glass effect using Win2D GPU acceleration
/// </summary>
public sealed class FrostedGlassPanel : ContentControl
{
    private CanvasControl? _canvasControl;

    #region Dependency Properties

    public static readonly DependencyProperty BlurAmountProperty =
        DependencyProperty.Register(nameof(BlurAmount), typeof(double), typeof(FrostedGlassPanel),
            new PropertyMetadata(20.0, OnEffectPropertyChanged));

    public static readonly DependencyProperty TintColorProperty =
        DependencyProperty.Register(nameof(TintColor), typeof(Color), typeof(FrostedGlassPanel),
            new PropertyMetadata(Color.FromArgb(40, 255, 255, 255), OnEffectPropertyChanged));

    public static readonly DependencyProperty TintOpacityProperty =
        DependencyProperty.Register(nameof(TintOpacity), typeof(double), typeof(FrostedGlassPanel),
            new PropertyMetadata(0.15, OnEffectPropertyChanged));

    public static readonly DependencyProperty BorderGlowColorProperty =
        DependencyProperty.Register(nameof(BorderGlowColor), typeof(Color), typeof(FrostedGlassPanel),
            new PropertyMetadata(Color.FromArgb(80, 255, 255, 255), OnEffectPropertyChanged));

    public static readonly DependencyProperty CornerRadiusValueProperty =
        DependencyProperty.Register(nameof(CornerRadiusValue), typeof(double), typeof(FrostedGlassPanel),
            new PropertyMetadata(16.0, OnEffectPropertyChanged));

    public static readonly DependencyProperty AccentColorProperty =
        DependencyProperty.Register(nameof(AccentColor), typeof(Color), typeof(FrostedGlassPanel),
            new PropertyMetadata(Colors.Transparent, OnEffectPropertyChanged));

    public static readonly DependencyProperty AccentBorderThicknessProperty =
        DependencyProperty.Register(nameof(AccentBorderThickness), typeof(double), typeof(FrostedGlassPanel),
            new PropertyMetadata(0.0, OnEffectPropertyChanged));

    public static readonly DependencyProperty WhiteTintIntensityProperty =
        DependencyProperty.Register(nameof(WhiteTintIntensity), typeof(double), typeof(FrostedGlassPanel),
            new PropertyMetadata(0.08, OnEffectPropertyChanged));

    public double BlurAmount
    {
        get => (double)GetValue(BlurAmountProperty);
        set => SetValue(BlurAmountProperty, value);
    }

    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    public double TintOpacity
    {
        get => (double)GetValue(TintOpacityProperty);
        set => SetValue(TintOpacityProperty, value);
    }

    public Color BorderGlowColor
    {
        get => (Color)GetValue(BorderGlowColorProperty);
        set => SetValue(BorderGlowColorProperty, value);
    }

    public double CornerRadiusValue
    {
        get => (double)GetValue(CornerRadiusValueProperty);
        set => SetValue(CornerRadiusValueProperty, value);
    }

    public Color AccentColor
    {
        get => (Color)GetValue(AccentColorProperty);
        set => SetValue(AccentColorProperty, value);
    }

    public double AccentBorderThickness
    {
        get => (double)GetValue(AccentBorderThicknessProperty);
        set => SetValue(AccentBorderThicknessProperty, value);
    }

    public double WhiteTintIntensity
    {
        get => (double)GetValue(WhiteTintIntensityProperty);
        set => SetValue(WhiteTintIntensityProperty, value);
    }

    #endregion

    public FrostedGlassPanel()
    {
        DefaultStyleKey = typeof(FrostedGlassPanel);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    protected override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        SetupCanvas();
    }

    private void SetupCanvas()
    {
        // Create canvas for Win2D rendering
        _canvasControl = new CanvasControl();
        _canvasControl.Draw += OnCanvasDraw;

        // Create the visual tree
        var grid = new Grid();
        
        // Background canvas for glass effect
        grid.Children.Add(_canvasControl);

        // Content presenter for child content
        var border = new Border
        {
            CornerRadius = new CornerRadius(CornerRadiusValue),
            Padding = Padding
        };

        var contentPresenter = new ContentPresenter
        {
            Content = Content,
            ContentTemplate = ContentTemplate,
            HorizontalAlignment = HorizontalContentAlignment,
            VerticalAlignment = VerticalContentAlignment
        };

        border.Child = contentPresenter;
        grid.Children.Add(border);

        // Set as visual content
        base.Content = grid;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _canvasControl?.Invalidate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        _canvasControl?.Invalidate();
    }

    private static void OnEffectPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrostedGlassPanel panel)
        {
            panel._canvasControl?.Invalidate();
        }
    }

    private void OnCanvasDraw(CanvasControl sender, CanvasDrawEventArgs args)
    {
        var session = args.DrawingSession;
        var size = new Vector2((float)sender.ActualWidth, (float)sender.ActualHeight);
        
        if (size.X <= 0 || size.Y <= 0) return;

        var cornerRadius = (float)CornerRadiusValue;

        // Draw the glass background with gaussian blur effect simulation
        DrawGlassBackground(session, size, cornerRadius);

        // Draw subtle white tint overlay
        DrawWhiteTintOverlay(session, size, cornerRadius);

        // Draw accent border if specified
        if (AccentBorderThickness > 0 && AccentColor.A > 0)
        {
            DrawAccentBorder(session, size, cornerRadius);
        }

        // Draw subtle border
        DrawGlassBorder(session, size, cornerRadius);
    }

    private void DrawGlassBackground(CanvasDrawingSession session, Vector2 size, float cornerRadius)
    {
        // Create gradient for the glass fill - simulating frosted glass with subtle color variation
        var device = CanvasDevice.GetSharedDevice();
        
        // Base glass color with subtle gradient
        var gradientStops = new Microsoft.Graphics.Canvas.Brushes.CanvasGradientStop[]
        {
            new() { Position = 0.0f, Color = Color.FromArgb((byte)(TintOpacity * 255 * 0.9), TintColor.R, TintColor.G, TintColor.B) },
            new() { Position = 0.2f, Color = Color.FromArgb((byte)(TintOpacity * 255 * 0.7), TintColor.R, TintColor.G, TintColor.B) },
            new() { Position = 0.5f, Color = Color.FromArgb((byte)(TintOpacity * 255 * 0.5), TintColor.R, TintColor.G, TintColor.B) },
            new() { Position = 0.8f, Color = Color.FromArgb((byte)(TintOpacity * 255 * 0.6), TintColor.R, TintColor.G, TintColor.B) },
            new() { Position = 1.0f, Color = Color.FromArgb((byte)(TintOpacity * 255 * 0.8), TintColor.R, TintColor.G, TintColor.B) }
        };

        using var gradientBrush = new Microsoft.Graphics.Canvas.Brushes.CanvasLinearGradientBrush(device, gradientStops)
        {
            StartPoint = new Vector2(0, 0),
            EndPoint = new Vector2(size.X * 0.3f, size.Y)
        };

        session.FillRoundedRectangle(0, 0, size.X, size.Y, cornerRadius, cornerRadius, gradientBrush);

        // Add a second layer for depth - simulating gaussian blur depth
        var blurGradientStops = new Microsoft.Graphics.Canvas.Brushes.CanvasGradientStop[]
        {
            new() { Position = 0.0f, Color = Color.FromArgb(15, 255, 255, 255) },
            new() { Position = 0.5f, Color = Color.FromArgb(5, 255, 255, 255) },
            new() { Position = 1.0f, Color = Color.FromArgb(10, 255, 255, 255) }
        };

        using var blurBrush = new Microsoft.Graphics.Canvas.Brushes.CanvasLinearGradientBrush(device, blurGradientStops)
        {
            StartPoint = new Vector2(0, 0),
            EndPoint = new Vector2(0, size.Y)
        };

        session.FillRoundedRectangle(0, 0, size.X, size.Y, cornerRadius, cornerRadius, blurBrush);
    }

    private void DrawWhiteTintOverlay(CanvasDrawingSession session, Vector2 size, float cornerRadius)
    {
        var device = CanvasDevice.GetSharedDevice();
        
        // Subtle white tint that gives the frosted glass look
        var whiteTintAlpha = (byte)(WhiteTintIntensity * 255);
        
        // Create a radial-like gradient for natural light diffusion effect
        var whiteTintStops = new Microsoft.Graphics.Canvas.Brushes.CanvasGradientStop[]
        {
            new() { Position = 0.0f, Color = Color.FromArgb((byte)(whiteTintAlpha * 1.5), 255, 255, 255) },
            new() { Position = 0.3f, Color = Color.FromArgb(whiteTintAlpha, 255, 255, 255) },
            new() { Position = 0.7f, Color = Color.FromArgb((byte)(whiteTintAlpha * 0.6), 255, 255, 255) },
            new() { Position = 1.0f, Color = Color.FromArgb((byte)(whiteTintAlpha * 0.4), 255, 255, 255) }
        };

        using var whiteTintBrush = new Microsoft.Graphics.Canvas.Brushes.CanvasLinearGradientBrush(device, whiteTintStops)
        {
            StartPoint = new Vector2(0, 0),
            EndPoint = new Vector2(size.X, size.Y)
        };

        session.FillRoundedRectangle(0, 0, size.X, size.Y, cornerRadius, cornerRadius, whiteTintBrush);

        // Add top highlight for depth effect (light reflection)
        var highlightStops = new Microsoft.Graphics.Canvas.Brushes.CanvasGradientStop[]
        {
            new() { Position = 0.0f, Color = Color.FromArgb(40, 255, 255, 255) },
            new() { Position = 0.15f, Color = Color.FromArgb(15, 255, 255, 255) },
            new() { Position = 0.4f, Color = Color.FromArgb(0, 255, 255, 255) }
        };

        using var highlightBrush = new Microsoft.Graphics.Canvas.Brushes.CanvasLinearGradientBrush(device, highlightStops)
        {
            StartPoint = new Vector2(size.X / 2, 0),
            EndPoint = new Vector2(size.X / 2, size.Y * 0.5f)
        };

        session.FillRoundedRectangle(0, 0, size.X, size.Y * 0.5f, cornerRadius, cornerRadius, highlightBrush);
    }

    private void DrawAccentBorder(CanvasDrawingSession session, Vector2 size, float cornerRadius)
    {
        // Draw colored accent at top border
        using var accentBrush = new Microsoft.Graphics.Canvas.Brushes.CanvasSolidColorBrush(
            CanvasDevice.GetSharedDevice(), AccentColor);

        // Top accent line
        session.FillRoundedRectangle(
            0, 0, size.X, (float)AccentBorderThickness + cornerRadius,
            cornerRadius, cornerRadius, accentBrush);

        // Mask bottom of accent
        session.FillRectangle(0, (float)AccentBorderThickness, size.X, cornerRadius, accentBrush);
    }

    private void DrawGlassBorder(CanvasDrawingSession session, Vector2 size, float cornerRadius)
    {
        var device = CanvasDevice.GetSharedDevice();
        
        // Subtle border with gradient for depth
        var borderStops = new Microsoft.Graphics.Canvas.Brushes.CanvasGradientStop[]
        {
            new() { Position = 0.0f, Color = Color.FromArgb(100, 255, 255, 255) },
            new() { Position = 0.5f, Color = Color.FromArgb(60, 255, 255, 255) },
            new() { Position = 1.0f, Color = Color.FromArgb(40, 255, 255, 255) }
        };

        using var borderGradient = new Microsoft.Graphics.Canvas.Brushes.CanvasLinearGradientBrush(device, borderStops)
        {
            StartPoint = new Vector2(0, 0),
            EndPoint = new Vector2(0, size.Y)
        };

        session.DrawRoundedRectangle(0.5f, 0.5f, size.X - 1f, size.Y - 1f, cornerRadius, cornerRadius, borderGradient, 1f);

        // Inner subtle highlight at top
        var highlightColor = Color.FromArgb(50, 255, 255, 255);
        using var highlightBrush = new Microsoft.Graphics.Canvas.Brushes.CanvasSolidColorBrush(device, highlightColor);

        session.FillRectangle(cornerRadius, 1, size.X - cornerRadius * 2, 1, highlightBrush);
    }
}
