using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace Summer.Controls;

public partial class SummerLogo : UserControl, IComponentConnector
{
	public static readonly DependencyProperty TextSizeProperty = DependencyProperty.Register("TextSize", typeof(double), typeof(SummerLogo), new FrameworkPropertyMetadata(22.0));

	public static readonly DependencyProperty ShowTaglineProperty = DependencyProperty.Register("ShowTagline", typeof(bool), typeof(SummerLogo), new FrameworkPropertyMetadata(false));

	public double TextSize
	{
		get
		{
			return (double)GetValue(TextSizeProperty);
		}
		set
		{
			SetValue(TextSizeProperty, value);
		}
	}

	public bool ShowTagline
	{
		get
		{
			return (bool)GetValue(ShowTaglineProperty);
		}
		set
		{
			SetValue(ShowTaglineProperty, value);
		}
	}

	public SummerLogo()
	{
		InitializeComponent();
	}
}
