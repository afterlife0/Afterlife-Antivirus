using System;
using System.Collections.Generic;
using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media.Animation;

namespace AfterlifeWinUI.Animations;

/// <summary>
/// Helper class for creating buttery smooth animations using Composition API
/// with spring physics and optimized easing curves for 60fps fluidity
/// </summary>
public static class PageAnimations
{
    // Pre-computed easing curve control points for ultra-smooth animations
    // Based on Apple's fluid design principles and Material Design motion
    private static readonly Vector2 FluidEaseOutP1 = new(0.0f, 0.0f);
    private static readonly Vector2 FluidEaseOutP2 = new(0.15f, 1.0f);
    
    // Spring-like easing for interactive elements
    private static readonly Vector2 SpringEaseP1 = new(0.175f, 0.885f);
    private static readonly Vector2 SpringEaseP2 = new(0.32f, 1.275f);
    
    // Deceleration curve for entrance animations (iOS-inspired)
    private static readonly Vector2 DecelerateP1 = new(0.0f, 0.0f);
    private static readonly Vector2 DecelerateP2 = new(0.2f, 1.0f);

    /// <summary>
    /// Animate elements with a smooth staggered slide-up and fade-in effect
    /// Uses GPU-accelerated composition animations for 60fps fluidity
    /// </summary>
    public static void AnimateEntranceStaggered(
        IList<UIElement> elements,
        int staggerDelay = 50,
        int duration = 400,
        float slideDistance = 40f)
    {
        if (elements == null || elements.Count == 0)
            return;

        for (int i = 0; i < elements.Count; i++)
        {
            var element = elements[i];
            if (element == null) continue;

            var delay = TimeSpan.FromMilliseconds(i * staggerDelay);
            AnimateElementEntranceSmooth(element, delay, duration, slideDistance);
        }
    }

    /// <summary>
    /// Animate a single element with buttery smooth slide-up and fade-in
    /// using optimized cubic-bezier easing for natural deceleration
    /// </summary>
    private static void AnimateElementEntranceSmooth(
        UIElement element,
        TimeSpan delay,
        int duration,
        float slideDistance)
    {
        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        // Set initial state - slightly more opacity for smoother fade
        visual.Opacity = 0f;
        visual.Offset = new Vector3(0, slideDistance, 0);

        // Create ultra-smooth deceleration easing (iOS-inspired)
        var easing = compositor.CreateCubicBezierEasingFunction(DecelerateP1, DecelerateP2);

        // Create opacity animation with implicit animation group for sync
        var fadeAnimation = compositor.CreateScalarKeyFrameAnimation();
        fadeAnimation.InsertKeyFrame(0f, 0f);
        fadeAnimation.InsertKeyFrame(0.6f, 0.85f, easing); // Fade in faster than slide
        fadeAnimation.InsertKeyFrame(1f, 1f, easing);
        fadeAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        fadeAnimation.DelayTime = delay;
        fadeAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        // Create slide animation with smooth deceleration
        var slideAnimation = compositor.CreateVector3KeyFrameAnimation();
        slideAnimation.InsertKeyFrame(0f, new Vector3(0, slideDistance, 0));
        slideAnimation.InsertKeyFrame(1f, Vector3.Zero, easing);
        slideAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        slideAnimation.DelayTime = delay;
        slideAnimation.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

        // Start animations
        visual.StartAnimation("Opacity", fadeAnimation);
        visual.StartAnimation("Offset", slideAnimation);
    }

    /// <summary>
    /// Animate page exit with smooth fade-out effect
    /// </summary>
    public static void AnimatePageExit(UIElement page, int duration = 150)
    {
        if (page == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(page);
        var compositor = visual.Compositor;

        // Fast ease-out for exits
        var easing = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.4f, 0.0f),
            new Vector2(0.6f, 1.0f));

        var fadeAnimation = compositor.CreateScalarKeyFrameAnimation();
        fadeAnimation.InsertKeyFrame(0f, 1f);
        fadeAnimation.InsertKeyFrame(1f, 0f, easing);
        fadeAnimation.Duration = TimeSpan.FromMilliseconds(duration);

        // Optional subtle slide up on exit
        var slideAnimation = compositor.CreateVector3KeyFrameAnimation();
        slideAnimation.InsertKeyFrame(0f, Vector3.Zero);
        slideAnimation.InsertKeyFrame(1f, new Vector3(0, -10f, 0), easing);
        slideAnimation.Duration = TimeSpan.FromMilliseconds(duration);

        visual.StartAnimation("Opacity", fadeAnimation);
        visual.StartAnimation("Offset", slideAnimation);
    }

    /// <summary>
    /// Reset element visual state
    /// </summary>
    public static void ResetVisualState(UIElement element)
    {
        if (element == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        visual.Opacity = 1f;
        visual.Offset = Vector3.Zero;
        visual.Scale = Vector3.One;
    }

    /// <summary>
    /// Animate scale with spring physics for buttery smooth interactive feedback
    /// </summary>
    public static void AnimateScaleSpring(UIElement element, float targetScale, float dampingRatio = 0.6f, float period = 50f)
    {
        if (element == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        // Set center point for scaling
        SetCenterPoint(element, visual);

        // Use spring animation for natural physics-based motion
        var springAnimation = compositor.CreateSpringVector3Animation();
        springAnimation.FinalValue = new Vector3(targetScale, targetScale, 1f);
        springAnimation.DampingRatio = dampingRatio;
        springAnimation.Period = TimeSpan.FromMilliseconds(period);
        springAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;

        visual.StartAnimation("Scale", springAnimation);
    }

    /// <summary>
    /// Create a smooth scale animation with bezier easing
    /// </summary>
    public static void AnimateScale(UIElement element, float scale, int duration = 200)
    {
        if (element == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        // Set center point for scaling
        SetCenterPoint(element, visual);

        var easing = compositor.CreateCubicBezierEasingFunction(FluidEaseOutP1, FluidEaseOutP2);

        var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(0f, visual.Scale);
        scaleAnimation.InsertKeyFrame(1f, new Vector3(scale, scale, 1f), easing);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(duration);

        visual.StartAnimation("Scale", scaleAnimation);
    }

    /// <summary>
    /// Animate with a spring bounce effect for press feedback
    /// </summary>
    public static void AnimateBounce(UIElement element)
    {
        if (element == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        SetCenterPoint(element, visual);

        var springAnimation = compositor.CreateSpringVector3Animation();
        springAnimation.InitialValue = new Vector3(0.92f, 0.92f, 1f);
        springAnimation.FinalValue = Vector3.One;
        springAnimation.DampingRatio = 0.5f;
        springAnimation.Period = TimeSpan.FromMilliseconds(40);
        springAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;

        visual.StartAnimation("Scale", springAnimation);
    }

    /// <summary>
    /// Animate press down state with quick scale reduction
    /// </summary>
    public static void AnimatePressDown(UIElement element)
    {
        if (element == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        SetCenterPoint(element, visual);

        var easing = compositor.CreateCubicBezierEasingFunction(
            new Vector2(0.2f, 0.0f),
            new Vector2(0.4f, 1.0f));

        var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
        scaleAnimation.InsertKeyFrame(0f, visual.Scale);
        scaleAnimation.InsertKeyFrame(1f, new Vector3(0.96f, 0.96f, 1f), easing);
        scaleAnimation.Duration = TimeSpan.FromMilliseconds(100);

        visual.StartAnimation("Scale", scaleAnimation);
    }

    /// <summary>
    /// Animate press release with spring physics
    /// </summary>
    public static void AnimatePressRelease(UIElement element)
    {
        AnimateBounce(element);
    }

    /// <summary>
    /// Animate hover enter with subtle scale up
    /// </summary>
    public static void AnimateHoverEnter(UIElement element, float scale = 1.02f)
    {
        if (element == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        SetCenterPoint(element, visual);

        var springAnimation = compositor.CreateSpringVector3Animation();
        springAnimation.FinalValue = new Vector3(scale, scale, 1f);
        springAnimation.DampingRatio = 0.7f;
        springAnimation.Period = TimeSpan.FromMilliseconds(50);
        springAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;

        visual.StartAnimation("Scale", springAnimation);
    }

    /// <summary>
    /// Animate hover exit with spring return to normal
    /// </summary>
    public static void AnimateHoverExit(UIElement element)
    {
        if (element == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        SetCenterPoint(element, visual);

        var springAnimation = compositor.CreateSpringVector3Animation();
        springAnimation.FinalValue = Vector3.One;
        springAnimation.DampingRatio = 0.65f;
        springAnimation.Period = TimeSpan.FromMilliseconds(45);
        springAnimation.StopBehavior = AnimationStopBehavior.SetToFinalValue;

        visual.StartAnimation("Scale", springAnimation);
    }

    /// <summary>
    /// Animate element with a subtle float/breathe effect
    /// </summary>
    public static void AnimateFloat(UIElement element, float amplitude = 3f, int duration = 2000)
    {
        if (element == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        var floatAnimation = compositor.CreateVector3KeyFrameAnimation();
        floatAnimation.InsertKeyFrame(0f, visual.Offset);
        floatAnimation.InsertKeyFrame(0.5f, visual.Offset + new Vector3(0, -amplitude, 0));
        floatAnimation.InsertKeyFrame(1f, visual.Offset);
        floatAnimation.Duration = TimeSpan.FromMilliseconds(duration);
        floatAnimation.IterationBehavior = AnimationIterationBehavior.Forever;

        visual.StartAnimation("Offset", floatAnimation);
    }

    /// <summary>
    /// Helper to set center point for scaling animations
    /// </summary>
    private static void SetCenterPoint(UIElement element, Visual visual)
    {
        if (element is FrameworkElement fe && fe.ActualWidth > 0 && fe.ActualHeight > 0)
        {
            visual.CenterPoint = new Vector3((float)fe.ActualWidth / 2, (float)fe.ActualHeight / 2, 0);
        }
    }

    /// <summary>
    /// Animate opacity smoothly
    /// </summary>
    public static void AnimateOpacity(UIElement element, float targetOpacity, int duration = 200)
    {
        if (element == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        var easing = compositor.CreateCubicBezierEasingFunction(FluidEaseOutP1, FluidEaseOutP2);

        var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
        opacityAnimation.InsertKeyFrame(0f, visual.Opacity);
        opacityAnimation.InsertKeyFrame(1f, targetOpacity, easing);
        opacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);

        visual.StartAnimation("Opacity", opacityAnimation);
    }

    /// <summary>
    /// Animate combined scale and opacity for smooth reveal effects
    /// </summary>
    public static void AnimateReveal(UIElement element, bool show, int duration = 250)
    {
        if (element == null) return;

        var visual = ElementCompositionPreview.GetElementVisual(element);
        var compositor = visual.Compositor;

        SetCenterPoint(element, visual);

        var easing = compositor.CreateCubicBezierEasingFunction(DecelerateP1, DecelerateP2);

        if (show)
        {
            // Reveal: scale up and fade in
            var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
            scaleAnimation.InsertKeyFrame(0f, new Vector3(0.95f, 0.95f, 1f));
            scaleAnimation.InsertKeyFrame(1f, Vector3.One, easing);
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(duration);

            var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.InsertKeyFrame(0f, 0f);
            opacityAnimation.InsertKeyFrame(1f, 1f, easing);
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);

            visual.StartAnimation("Scale", scaleAnimation);
            visual.StartAnimation("Opacity", opacityAnimation);
        }
        else
        {
            // Hide: scale down and fade out
            var scaleAnimation = compositor.CreateVector3KeyFrameAnimation();
            scaleAnimation.InsertKeyFrame(0f, visual.Scale);
            scaleAnimation.InsertKeyFrame(1f, new Vector3(0.95f, 0.95f, 1f), easing);
            scaleAnimation.Duration = TimeSpan.FromMilliseconds(duration);

            var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
            opacityAnimation.InsertKeyFrame(0f, visual.Opacity);
            opacityAnimation.InsertKeyFrame(1f, 0f, easing);
            opacityAnimation.Duration = TimeSpan.FromMilliseconds(duration);

            visual.StartAnimation("Scale", scaleAnimation);
            visual.StartAnimation("Opacity", opacityAnimation);
        }
    }
}
