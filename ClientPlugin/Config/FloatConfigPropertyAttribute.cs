using System;

namespace ClientPlugin.Config;

[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public sealed class FloatConfigPropertyAttribute : ConfigPropertyAttribute
{
    public float Min { get; } = 0f;
    public float Max { get; } = 1f;
    public float DefaultValue { get; } = 1f;

    public FloatConfigPropertyAttribute()
    {

    }

    public FloatConfigPropertyAttribute(string name, float min, float max, float defaultValue, string? toolTip = null)
        : base(name, toolTip)
    {
        Min = min;
        Max = max;
        DefaultValue = defaultValue;
    }
}
