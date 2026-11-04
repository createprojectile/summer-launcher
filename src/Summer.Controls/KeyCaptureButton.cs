using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Summer.Controls;

public sealed class KeyCaptureButton : Button
{
	public static readonly DependencyProperty KeyNameProperty = DependencyProperty.Register("KeyName", typeof(string), typeof(KeyCaptureButton), new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

	private static readonly DependencyPropertyKey IsCapturingPropertyKey = DependencyProperty.RegisterReadOnly("IsCapturing", typeof(bool), typeof(KeyCaptureButton), new PropertyMetadata(false));

	public static readonly DependencyProperty IsCapturingProperty = IsCapturingPropertyKey.DependencyProperty;

	public string KeyName
	{
		get
		{
			return (string)GetValue(KeyNameProperty);
		}
		set
		{
			SetValue(KeyNameProperty, value);
		}
	}

	public bool IsCapturing => (bool)GetValue(IsCapturingProperty);

	public KeyCaptureButton()
	{
		Focusable = true;
		IsTabStop = true;
		ToolTip = "Нажмите, затем клавишу. Esc — отмена.";
	}

	protected override void OnClick()
	{
		base.OnClick();
		if (!IsCapturing)
		{
			SetValue(IsCapturingPropertyKey, true);
			Keyboard.Focus(this);
		}
	}

	protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
	{
		base.OnLostKeyboardFocus(e);
		if (IsCapturing)
		{
			SetValue(IsCapturingPropertyKey, false);
		}
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		if (!IsCapturing)
		{
			base.OnPreviewKeyDown(e);
			return;
		}
		e.Handled = true;
		if (e.Key == Key.Escape)
		{
			SetValue(IsCapturingPropertyKey, false);
			return;
		}
		Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
		if (!IsModifierKey(key))
		{
			string text = FormatKey(key);
			if (!string.IsNullOrWhiteSpace(text))
			{
				KeyName = text;
				SetValue(IsCapturingPropertyKey, false);
			}
		}
	}

	protected override void OnPreviewKeyUp(KeyEventArgs e)
	{
		if (!IsCapturing)
		{
			base.OnPreviewKeyUp(e);
			return;
		}
		Key key = ((e.Key == Key.System) ? e.SystemKey : e.Key);
		if (!IsModifierKey(key))
		{
			base.OnPreviewKeyUp(e);
			return;
		}
		e.Handled = true;
		KeyName = FormatKey(key);
		SetValue(IsCapturingPropertyKey, false);
	}

	private static bool IsModifierKey(Key key)
	{
		if ((uint)(key - 70) <= 1u || (uint)(key - 116) <= 5u || key == Key.System)
		{
			return true;
		}
		return false;
	}

	protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
	{
		if (!IsCapturing)
		{
			base.OnPreviewMouseDown(e);
			return;
		}
		e.Handled = true;
		KeyName = e.ChangedButton switch
		{
			MouseButton.Left => "ЛКМ", 
			MouseButton.Right => "ПКМ", 
			MouseButton.Middle => "СКМ", 
			MouseButton.XButton1 => "MB4", 
			MouseButton.XButton2 => "MB5", 
			_ => KeyName, 
		};
		SetValue(IsCapturingPropertyKey, false);
	}

	private static string FormatKey(Key key)
	{
		switch (key)
		{
		case Key.LeftCtrl:
		case Key.RightCtrl:
			return "Ctrl";
		case Key.LeftShift:
		case Key.RightShift:
			return "Shift";
		case Key.LeftAlt:
		case Key.RightAlt:
		case Key.System:
			return "Alt";
		case Key.LWin:
		case Key.RWin:
			return "Win";
		case Key.Space:
			return "Space";
		case Key.Return:
			return "Enter";
		case Key.Back:
			return "Backspace";
		case Key.Delete:
			return "Delete";
		case Key.Tab:
			return "Tab";
		case Key.OemPlus:
			return "+";
		case Key.OemMinus:
			return "-";
		case Key.OemComma:
			return ",";
		case Key.OemPeriod:
			return ".";
		case Key.Oem2:
			return "/";
		case Key.D0:
			return "0";
		case Key.D1:
			return "1";
		case Key.D2:
			return "2";
		case Key.D3:
			return "3";
		case Key.D4:
			return "4";
		case Key.D5:
			return "5";
		case Key.D6:
			return "6";
		case Key.D7:
			return "7";
		case Key.D8:
			return "8";
		case Key.D9:
			return "9";
		case Key.None:
			return string.Empty;
		default:
			return key.ToString();
		}
	}
}
