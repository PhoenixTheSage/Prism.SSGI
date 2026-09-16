using ClientPlugin.Config;
using ClientPlugin.Gui.Controls;
using ClientPlugin.SSGI;
using Sandbox;
using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using VRage.Utils;
using VRageMath;

namespace ClientPlugin.Gui;

public abstract class PropertyBinding
{
    public abstract MyGuiControlBase Control { get; }
    public abstract void Save();
}

public class PropertyBinding<TControl, TValue> : PropertyBinding where TControl : MyGuiControlBase
{
    public override MyGuiControlBase Control => _control;
    private readonly TControl _control;
    private readonly object _target;
    private readonly PropertyInfo _property;
    private readonly Func<TControl, TValue> _onSave;

    public PropertyBinding(TControl control, object target, PropertyInfo property, Func<TControl, TValue> getValueFromControl)
    {
        _control = control;
        _target = target;
        _property = property;
        _onSave = getValueFromControl;
    }

    public override void Save()
    {
        _property.SetValue(_target, _onSave(_control));
    }
}

public class GuiScreenSSGIConfig : MyGuiScreenBase
{
    struct PresetData
    {
        public float GIIntensity;
        public int InputMipLevel;
        public int SliceCount;
        public int StepCount;
        public float Radius;
        public float ExpFactor;
        public float Thickness;
        public int DenoiserMaxHistory;
        public int DenoiserBlurIterations;

        public void ApplyTo(GuiScreenSSGIConfig target)
        {
            ((MyGuiControlSlider)target._controlsByPropertyName["GIIntensity"])       .Value = GIIntensity;
            ((MyGuiControlSlider)target._controlsByPropertyName["InputMipLevel"])     .Value = InputMipLevel;
            ((MyGuiControlSlider)target._controlsByPropertyName["SliceCount"])        .Value = SliceCount;
            ((MyGuiControlSlider)target._controlsByPropertyName["StepCount"])         .Value = StepCount;
            ((MyGuiControlSlider)target._controlsByPropertyName["Radius"])            .Value = Radius;
            ((MyGuiControlSlider)target._controlsByPropertyName["ExpFactor"])         .Value = ExpFactor;
            ((MyGuiControlSlider)target._controlsByPropertyName["Thickness"])         .Value = Thickness;
            ((MyGuiControlSlider)target._controlsByPropertyName["DenoiserMaxHistory"]).Value = DenoiserMaxHistory;
            ((MyGuiControlSlider)target._controlsByPropertyName["DenoiserBlurIterations"]).Value = DenoiserBlurIterations;
        }
    }

    static readonly PresetData[] _presets =
    {
        new PresetData // low
        {
            GIIntensity = 5,
            InputMipLevel = 4,
            SliceCount = 1,
            StepCount = 8,
            Radius = 5.0f,
            ExpFactor = 1.5f,
            Thickness = 1.0f,
            DenoiserMaxHistory = 24,
            DenoiserBlurIterations = 5,
        },
        new PresetData // medium
        {
            GIIntensity = 5,
            InputMipLevel = 3,
            SliceCount = 2,
            StepCount = 16,
            Radius = 7.5f,
            ExpFactor = 1.5f,
            Thickness = 1.0f,
            DenoiserMaxHistory = 20,
            DenoiserBlurIterations = 5,
        },
        new PresetData // high
        {
            GIIntensity = 5,
            InputMipLevel = 3,
            SliceCount = 4,
            StepCount = 32,
            Radius = 10.0f,
            ExpFactor = 1.5f,
            Thickness = 1.0f,
            DenoiserMaxHistory = 20,
            DenoiserBlurIterations = 4,
        },
    };

    private readonly List<PropertyBinding> _bindings = [];
    private readonly Dictionary<string, MyGuiControlBase> _controlsByPropertyName = [];
    private readonly SSGIConfig _config;

    public GuiScreenSSGIConfig(SSGIConfig config)
        : base(new Vector2(0.5f), MyGuiConstants.SCREEN_BACKGROUND_COLOR, new Vector2(0.6f, 0.7f), false, null, MySandboxGame.Config.UIBkOpacity, MySandboxGame.Config.UIOpacity)
    {
        _config = config;

        EnabledBackgroundFade = true;
        m_closeOnEsc = true;
        m_drawEvenWithoutFocus = true;
        CanHideOthers = true;
        CanBeHidden = true;
        CloseButtonEnabled = true;
    }

    public override string GetFriendlyName() => GetType().FullName;

    public override void LoadContent()
    {
        base.LoadContent();
        RecreateControls(false);
    }

    public override void RecreateControls(bool constructor)
    {
        base.RecreateControls(constructor);

        AddCaption("SSGI Settings");

        float columnWidth = (Size!.Value.X - 0.1f) / 2;
        float rowHeight = 0.05f;

        var grid = new UniformGrid
        {
            ColumnWidth = columnWidth,
            RowHeight = rowHeight,
        };

        // preset buttons
        var dropdown = new MyGuiControlCombobox
        {
            Position = new Vector2(0.0355f, -0.222f),
            Size = new Vector2(0.174f, 0),
            OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER,
        };
        dropdown.AddItem(0, "Low");
        dropdown.AddItem(1, "Medium");
        dropdown.AddItem(2, "High");
        dropdown.AddItem(99, "Quality Preset");
        dropdown.SelectItemByKey(99);
        dropdown.ItemSelected += () =>
        {
            _presets[dropdown.GetSelectedKey()].ApplyTo(this);
            dropdown.SelectItemByKey(99, false);
        };
        AddControl(dropdown);

        int row = 0;
        foreach (var property in GetBindingTargets())
        {
            var control = CreateControl(property, grid, 0, row++);
            _bindings.Add(control);
            _controlsByPropertyName.Add(property.Name, control.Control);
        }

        grid.AddControlsToScreen(this, Vector2.Zero, false);

        // add footer buttons
        {
            float yPos = (Size!.Value.Y * 0.5f) - (MyGuiConstants.SCREEN_CAPTION_DELTA_Y / 2f);
            var status = new MyGuiControlButton(onButtonClick: _ => SSGIStatus.Show())
            {
                Position = new Vector2(-0.12f, yPos),
                Text = "Status",
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM,
            };
            AddControl(status);
            var button = new MyGuiControlButton(onButtonClick: OnSaveButtonClick)
            {
                Position = new Vector2(0.12f, yPos),
                Text = "Save",
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM,
            };
            AddControl(button);
        }
    }

    private void OnSaveButtonClick(MyGuiControlButton btn)
    {
        _bindings.ForEach(i => i?.Save());
        _config.Save();
        _config.FlushPending(true);
    }

    private IEnumerable<PropertyInfo> GetBindingTargets()
    {
        return typeof(SSGIConfig).GetProperties().Where(i => i.GetCustomAttribute<ConfigPropertyAttribute>() != null);
    }

    private PropertyBinding CreateControl(PropertyInfo prop, UniformGrid grid, int column, int row)
    {
        var attribute = prop.GetCustomAttribute<ConfigPropertyAttribute>();
        if (prop.PropertyType == typeof(bool))
        {
            grid.AddLabel(column, row, attribute.Name ?? prop.Name, HorizontalAlignment.Left);
            var control = grid.AddCheckbox(column + 1, row, attribute.Enabled, (bool)prop.GetValue(_config), attribute.ToolTip, HorizontalAlignment.Left);
            return new PropertyBinding<MyGuiControlCheckbox, bool>(control, _config, prop, checkbox => checkbox.IsChecked);
        }
        else if (prop.PropertyType == typeof(float) && prop.GetCustomAttribute<FloatConfigPropertyAttribute>() is FloatConfigPropertyAttribute floatProp)
        {
            grid.AddLabel(column, row, attribute.Name ?? prop.Name, HorizontalAlignment.Left);
            var control = grid.AddFloatSlider(column + 1, row, attribute.Enabled, (float)prop.GetValue(_config), floatProp.Min, floatProp.Max, floatProp.DefaultValue, true, HorizontalAlignment.Left);
            control.SetToolTip(floatProp.ToolTip);
            return new PropertyBinding<MyGuiControlSlider, float>(control, _config, prop, slider => slider.Value);
        }
        else if (prop.PropertyType == typeof(int) && prop.GetCustomAttribute<IntConfigPropertyAttribute>() is IntConfigPropertyAttribute intProp)
        {
            grid.AddLabel(column, row, attribute.Name ?? prop.Name, HorizontalAlignment.Left);
            var control = grid.AddIntegerSlider(column + 1, row, attribute.Enabled, (int)prop.GetValue(_config), intProp.Min, intProp.Max, intProp.DefaultValue, true, HorizontalAlignment.Left);
            control.SetToolTip(intProp.ToolTip);
            return new PropertyBinding<MyGuiControlSlider, int>(control, _config, prop, slider => (int)slider.Value);
        }
        else
        {
            return null;
        }
    }

}
