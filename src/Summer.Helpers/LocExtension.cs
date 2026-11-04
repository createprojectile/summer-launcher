using System;
using System.Windows.Data;
using System.Windows.Markup;
using Summer.Services;

namespace Summer.Helpers;

[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
	public string Key { get; set; } = string.Empty;

	public LocExtension()
	{
	}

	public LocExtension(string key)
	{
		Key = key;
	}

	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		if (string.IsNullOrWhiteSpace(Key))
		{
			return string.Empty;
		}
		return new Binding("[" + Key + "]")
		{
			Source = LocalizationService.Instance,
			Mode = BindingMode.OneWay
		}.ProvideValue(serviceProvider);
	}
}
