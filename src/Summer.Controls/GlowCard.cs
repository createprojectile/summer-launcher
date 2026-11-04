using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Summer.Helpers;

namespace Summer.Controls;

public class GlowCard : Button
{
	public static readonly DependencyProperty GlowColorProperty = DependencyProperty.Register("GlowColor", typeof(Color), typeof(GlowCard), new PropertyMetadata(Color.FromRgb(225, 29, 46), OnGlowSettingsChanged));

	public static readonly DependencyProperty GlowRadiusXProperty = DependencyProperty.Register("GlowRadiusX", typeof(double), typeof(GlowCard), new PropertyMetadata(0.34, OnGlowSettingsChanged));

	public static readonly DependencyProperty GlowRadiusYProperty = DependencyProperty.Register("GlowRadiusY", typeof(double), typeof(GlowCard), new PropertyMetadata(0.5, OnGlowSettingsChanged));

	public static readonly DependencyProperty GlowBrushProperty = DependencyProperty.Register("GlowBrush", typeof(RadialGradientBrush), typeof(GlowCard), new PropertyMetadata(null));

	private Border? _glowLayer;

	private FrameworkElement? _glowHost;

	private readonly TranslateTransform _glowTranslation = new TranslateTransform();

	public Color GlowColor
	{
		get
		{
			return (Color)GetValue(GlowColorProperty);
		}
		set
		{
			SetValue(GlowColorProperty, value);
		}
	}

	public double GlowRadiusX
	{
		get
		{
			return (double)GetValue(GlowRadiusXProperty);
		}
		set
		{
			SetValue(GlowRadiusXProperty, value);
		}
	}

	public double GlowRadiusY
	{
		get
		{
			return (double)GetValue(GlowRadiusYProperty);
		}
		set
		{
			SetValue(GlowRadiusYProperty, value);
		}
	}

	public RadialGradientBrush GlowBrush
	{
		get
		{
			return (RadialGradientBrush)GetValue(GlowBrushProperty);
		}
		private set
		{
			SetValue(GlowBrushProperty, value);
		}
	}

	public GlowCard()
	{
		GlowBrush = CreateBrush(GlowColor);
		PreviewMouseMove += OnPreviewMouseMove;
		MouseEnter += OnMouseEnter;
		MouseLeave += OnMouseLeave;
	}

	public override void OnApplyTemplate()
	{
		if (_glowHost != null)
		{
			_glowHost.SizeChanged -= OnGlowHostSizeChanged;
		}
		base.OnApplyTemplate();
		_glowLayer = GetTemplateChild("PART_Glow") as Border;
		_glowHost = GetTemplateChild("PART_GlowHost") as FrameworkElement;
		if (_glowLayer != null)
		{
			_glowLayer.CacheMode = new BitmapCache();
			_glowLayer.RenderTransform = _glowTranslation;
		}
		if (_glowHost != null)
		{
			_glowHost.SizeChanged += OnGlowHostSizeChanged;
		}
		UpdateGlowSize();
	}

	protected override void OnClick()
	{
		base.OnClick();
		UiSound.PlayTap();
	}

	private static void OnGlowSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is GlowCard glowCard)
		{
			glowCard.GlowBrush = CreateBrush(glowCard.GlowColor);
			glowCard.UpdateGlowSize();
		}
	}

	private static RadialGradientBrush CreateBrush(Color color)
	{
		RadialGradientBrush radialGradientBrush = new RadialGradientBrush();
		radialGradientBrush.Center = new Point(0.5, 0.5);
		radialGradientBrush.GradientOrigin = new Point(0.5, 0.5);
		radialGradientBrush.RadiusX = 0.5;
		radialGradientBrush.RadiusY = 0.5;
		radialGradientBrush.MappingMode = BrushMappingMode.RelativeToBoundingBox;
		radialGradientBrush.GradientStops.Add(new GradientStop(WithAlpha(color, 85), 0.0));
		radialGradientBrush.GradientStops.Add(new GradientStop(WithAlpha(color, 36), 0.45));
		radialGradientBrush.GradientStops.Add(new GradientStop(WithAlpha(color, 10), 0.75));
		radialGradientBrush.GradientStops.Add(new GradientStop(WithAlpha(color, 0), 1.0));
		radialGradientBrush.Freeze();
		return radialGradientBrush;
	}

	private static Color WithAlpha(Color color, byte alpha)
	{
		return Color.FromArgb(alpha, color.R, color.G, color.B);
	}

	private void OnGlowHostSizeChanged(object sender, SizeChangedEventArgs e)
	{
		UpdateGlowSize();
	}

	private void UpdateGlowSize()
	{
		if (_glowLayer != null && _glowHost != null)
		{
			_glowLayer.Width = _glowHost.ActualWidth * SafeRadius(GlowRadiusX) * 2.0;
			_glowLayer.Height = _glowHost.ActualHeight * SafeRadius(GlowRadiusY) * 2.0;
			MoveGlow(Mouse.GetPosition(_glowHost));
		}
	}

	private static double SafeRadius(double radius)
	{
		if (!double.IsFinite(radius))
		{
			return 0.5;
		}
		return Math.Clamp(radius, 0.01, 2.0);
	}

	private void MoveGlow(Point position)
	{
		if (_glowLayer != null && _glowHost != null)
		{
			_glowTranslation.X = Math.Clamp(position.X, 0.0, _glowHost.ActualWidth) - _glowLayer.Width / 2.0;
			_glowTranslation.Y = Math.Clamp(position.Y, 0.0, _glowHost.ActualHeight) - _glowLayer.Height / 2.0;
		}
	}

	private void OnPreviewMouseMove(object sender, MouseEventArgs e)
	{
		MoveGlow(e.GetPosition(_glowHost ?? this));
	}

	private void OnMouseEnter(object sender, MouseEventArgs e)
	{
		if (_glowLayer != null)
		{
			MoveGlow(e.GetPosition(_glowHost ?? this));
			AnimateGlow(1.0, 0.22);
		}
	}

	private void OnMouseLeave(object sender, MouseEventArgs e)
	{
		AnimateGlow(0.0, 0.34);
	}

	private void AnimateGlow(double targetOpacity, double seconds)
	{
		if (_glowLayer != null)
		{
			_glowLayer.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(targetOpacity, TimeSpan.FromSeconds(seconds))
			{
				EasingFunction = new CubicEase
				{
					EasingMode = EasingMode.EaseOut
				},
				FillBehavior = FillBehavior.HoldEnd
			});
		}
	}
}
