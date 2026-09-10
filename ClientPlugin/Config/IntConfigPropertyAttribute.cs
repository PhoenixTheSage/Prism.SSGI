using System;

namespace ClientPlugin.Config;

[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class IntConfigPropertyAttribute : ConfigPropertyAttribute
{
    public int Min { get; } = 0;
    public int Max { get; } = 100;
    public int DefaultValue { get; } = 100;

    public IntConfigPropertyAttribute()
    {

    }

    public IntConfigPropertyAttribute(string name, int min, int max, int defaultValue, string? toolTip = null)
        : base(name, toolTip)
    {
        Min = min;
        Max = max;
        DefaultValue = defaultValue;
    }
}
