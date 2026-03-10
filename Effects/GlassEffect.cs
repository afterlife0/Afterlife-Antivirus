using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using System.Numerics;
using Windows.UI;

namespace AfterlifeWinUI.Effects;

/// <summary>
/// Provides GPU-accelerated frosted glass effects using Win2D
/// </summary>
public sealed class GlassEffect : IDisposable
{
    private bool _disposed;

    public float BlurAmount { get; set; } = 30f;
    public float Opacity { get; set; } = 0.7f;
    public Color TintColor { get; set; } = Color.FromArgb(40, 255, 255, 255);
    public Color BorderColor { get; set; } = Color.FromArgb(80, 255, 255, 255);
    public float BorderWidth { get; set; } = 1f;
    public float CornerRadius { get; set; } = 12f;

    /// <summary>
    /// Creates a frosted glass effect that can be applied to a background
    /// </summary>
    public ICanvasImage CreateFrostedGlassEffect(ICanvasImage source)
    {
        // Step 1: Apply Gaussian blur for the frosted effect
        var blurEffect = new GaussianBlurEffect
        {
            Source = source,
            BlurAmount = BlurAmount,
            BorderMode = EffectBorderMode.Hard
        };

        // Step 2: Add color tint overlay
        var tintEffect = new ColorSourceEffect
        {
            Color = TintColor
        };

        // Step 3: Blend the blur with tint
        var blendEffect = new BlendEffect
        {
            Background = blurEffect,
            Foreground = tintEffect,
            Mode = BlendEffectMode.Overlay
        };

        // Step 4: Add subtle saturation adjustment for glass look
        var saturationEffect = new SaturationEffect
        {
            Source = blendEffect,
            Saturation = 1.2f
        };

        // Step 5: Apply opacity
        var opacityEffect = new OpacityEffect
        {
            Source = saturationEffect,
            Opacity = Opacity
        };

        return opacityEffect;
    }

    /// <summary>
    /// Creates an advanced frosted glass effect with edge glow and noise
    /// </summary>
    public ICanvasImage CreateLiquidGlassEffect(ICanvasImage source, Vector2 size)
    {
        // Heavy blur for frosted appearance
        var blurEffect = new GaussianBlurEffect
        {
            Source = source,
            BlurAmount = BlurAmount,
            BorderMode = EffectBorderMode.Hard
        };

        // Add turbulence for liquid glass distortion
        var turbulence = new TurbulenceEffect
        {
            Size = new Vector2(80, 80),
            Frequency = new Vector2(0.02f, 0.02f),
            Octaves = 2
        };

        // Apply displacement for subtle distortion
        var displacementEffect = new DisplacementMapEffect
        {
            Source = blurEffect,
            Displacement = turbulence,
            Amount = 5f,
            XChannelSelect = EffectChannelSelect.Red,
            YChannelSelect = EffectChannelSelect.Green
        };

        // Color matrix for glass tint
        var colorMatrix = new ColorMatrixEffect
        {
            Source = displacementEffect,
            ColorMatrix = new Matrix5x4
            {
                M11 = 1.0f, M12 = 0, M13 = 0, M14 = 0,
                M21 = 0, M22 = 1.0f, M23 = 0, M24 = 0,
                M31 = 0, M32 = 0, M33 = 1.0f, M34 = 0,
                M41 = 0, M42 = 0, M43 = 0, M44 = Opacity,
                M51 = TintColor.R / 255f * 0.1f,
                M52 = TintColor.G / 255f * 0.1f,
                M53 = TintColor.B / 255f * 0.1f,
                M54 = 0
            }
        };

        // Add specular highlight for glass shine
        var specularLighting = new SpotSpecularEffect
        {
            Source = source,
            LightPosition = new Vector3(size.X * 0.3f, -100f, 200f),
            LightTarget = new Vector3(size.X * 0.5f, size.Y * 0.5f, 0),
            Focus = 10f,
            SpecularExponent = 16f,
            SpecularAmount = 0.3f,
            LightColor = Colors.White
        };

        // Composite specular on top
        var compositeEffect = new CompositeEffect
        {
            Mode = CanvasComposite.Add
        };
        compositeEffect.Sources.Add(colorMatrix);
        compositeEffect.Sources.Add(new OpacityEffect { Source = specularLighting, Opacity = 0.15f });

        return compositeEffect;
    }

    /// <summary>
    /// Creates a simple glass panel effect optimized for performance
    /// </summary>
    public ICanvasImage CreateSimpleGlassEffect(ICanvasImage source)
    {
        // Blur
        var blur = new GaussianBlurEffect
        {
            Source = source,
            BlurAmount = BlurAmount
        };

        // Tint
        var tint = new TintEffect
        {
            Source = blur,
            Color = TintColor
        };

        // Opacity
        var opacity = new OpacityEffect
        {
            Source = tint,
            Opacity = Opacity
        };

        return opacity;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
        }
    }
}

/// <summary>
/// Predefined glass effect configurations
/// </summary>
public static class GlassPresets
{
    public static GlassEffect DarkGlass => new()
    {
        BlurAmount = 40f,
        Opacity = 0.75f,
        TintColor = Color.FromArgb(60, 18, 18, 26),
        BorderColor = Color.FromArgb(60, 255, 255, 255),
        CornerRadius = 16f
    };

    public static GlassEffect LightGlass => new()
    {
        BlurAmount = 30f,
        Opacity = 0.6f,
        TintColor = Color.FromArgb(40, 255, 255, 255),
        BorderColor = Color.FromArgb(80, 255, 255, 255),
        CornerRadius = 12f
    };

    public static GlassEffect CyanAccentGlass => new()
    {
        BlurAmount = 35f,
        Opacity = 0.7f,
        TintColor = Color.FromArgb(30, 0, 243, 255),
        BorderColor = Color.FromArgb(100, 0, 243, 255),
        CornerRadius = 12f
    };

    public static GlassEffect SidebarGlass => new()
    {
        BlurAmount = 50f,
        Opacity = 0.85f,
        TintColor = Color.FromArgb(80, 18, 18, 26),
        BorderColor = Color.FromArgb(40, 255, 255, 255),
        CornerRadius = 20f
    };

    public static GlassEffect CardGlass => new()
    {
        BlurAmount = 25f,
        Opacity = 0.65f,
        TintColor = Color.FromArgb(50, 30, 30, 40),
        BorderColor = Color.FromArgb(50, 255, 255, 255),
        CornerRadius = 16f
    };
}
