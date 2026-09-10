using System;

namespace ClientPlugin.Config;

[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
public class ConfigPropertyAttribute : Attribute
{
    public bool Enabled { get; set; } = true;
    public string? Name { get; set; } = null;
    public string? ToolTip { get; set; } = null;

    public ConfigPropertyAttribute()
    {

    }

    public ConfigPropertyAttribute(string name, string? toolTip = null)
    {
        Name = name;
        ToolTip = toolTip;
    }
}
