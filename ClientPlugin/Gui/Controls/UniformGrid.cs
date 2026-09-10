using Sandbox.Graphics.GUI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Utils;
using VRageMath;

namespace ClientPlugin.Gui.Controls
{
    public class UniformGrid
    {
        private class Item
        {
            public int Column { get; set; } = 0;
            public int Row { get; set; } = 0;
            public HorizontalAlignment HorizontalAlignment { get; set; } = HorizontalAlignment.Center;
            public VerticalAlignment VerticalAlignment { get; set; } = VerticalAlignment.Center;

            public readonly MyGuiControlBase Control;

            public Item(MyGuiControlBase control)
            {
                Control = control;
            }
        };

        public int MinColumns { get; set; }
        public int MinRows { get; set; }
        public float ColumnWidth { get; set; }
        public float RowHeight { get; set; }

        private readonly List<Item> _controls = new List<Item>();

        public UniformGrid()
        {

        }

        private void Add(MyGuiControlBase control, int column, int row, HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment)
        {
            _controls.Add(new Item(control)
            {
                Column = column,
                Row = row,
                HorizontalAlignment = horizontalAlignment,
                VerticalAlignment = verticalAlignment,
            });
        }

        public MyGuiControlLabel AddLabel(
            int column, int row,
            string text,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment verticalAlignment = VerticalAlignment.Center)
        {
            var label = new MyGuiControlLabel
            {
                Text = text,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
            };
            label.SetMaxSize(new Vector2(ColumnWidth, RowHeight));
            Add(label, column, row, horizontalAlignment, verticalAlignment);
            return label;
        }

        public MyGuiControlCheckbox AddCheckbox(
            int column, int row,
            bool isEnabled,
            bool isChecked,
            string? toolTip = null,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment verticalAlignment = VerticalAlignment.Center)
        {
            var checkbox = new MyGuiControlCheckbox(toolTip: toolTip)
            {
                Enabled = isEnabled,
                IsChecked = isChecked,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
            };
            checkbox.SetMaxSize(new Vector2(ColumnWidth, RowHeight));
            Add(checkbox, column, row, horizontalAlignment, verticalAlignment);
            return checkbox;
        }

        public MyGuiControlSlider AddIntegerSlider(
            int column, int row,
            bool isEnabled,
            int value, int minValue, int maxValue, int defaultValue,
            bool showValueLabel = true,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment verticalAlignment = VerticalAlignment.Center)
        {
            var slider = new MyGuiControlSlider(
                showLabel: showValueLabel,
                labelText: "{0}",
                labelFont: "Blue",
                labelSpaceWidth: 0.04f,
                labelDecimalPlaces: 0,
                intValue: true,
                minValue: minValue,
                maxValue: maxValue,
                defaultValue: defaultValue)
            {
                Enabled = isEnabled,
                Value = value,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
            };
            slider.SetMaxSize(new Vector2(ColumnWidth, RowHeight));
            Add(slider, column, row, horizontalAlignment, verticalAlignment);
            return slider;
        }

        public MyGuiControlSlider AddFloatSlider(
            int column, int row,
            bool isEnabled,
            float value, float minValue, float maxValue, float defaultValue,
            bool showValueLabel = true,
            HorizontalAlignment horizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment verticalAlignment = VerticalAlignment.Center)
        {
            var slider = new MyGuiControlSlider(
                showLabel: showValueLabel,
                labelText: "{0}",
                labelFont: "Blue",
                labelSpaceWidth: 0.04f,
                labelDecimalPlaces: 2,
                intValue: false,
                minValue: minValue,
                maxValue: maxValue,
                defaultValue: defaultValue)
            {
                Enabled = isEnabled,
                Value = value,
                OriginAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
            };
            slider.SetMaxSize(new Vector2(ColumnWidth, RowHeight));
            Add(slider, column, row, horizontalAlignment, verticalAlignment);
            return slider;
        }

        public void AddControlsToScreen(MyGuiScreenBase screen, Vector2 centerPos, bool drawBorderLines)
        {
            int columns = Math.Max(MinColumns, _controls.Max(i => i.Column) + 1);
            int rows = Math.Max(MinRows, _controls.Max(i => i.Row) + 1);

            var cellSize = new Vector2(ColumnWidth, RowHeight);
            var totalSize = new Vector2(columns, rows) * cellSize;
            var topLeft = centerPos - totalSize * 0.5f;

            foreach (var item in _controls)
            {
                item.Control.Position = topLeft + new Vector2(item.Column + 0.5f * (int)item.HorizontalAlignment, item.Row + 0.5f * (int)item.VerticalAlignment) * cellSize;
                item.Control.OriginAlign = (MyGuiDrawAlignEnum)((int)item.HorizontalAlignment * 3 + (int)item.VerticalAlignment);
                screen.Controls.Add(item.Control);
            }

            if (drawBorderLines) // debug feature
            {
                var separators = new MyGuiControlSeparatorList
                {
                    Position = centerPos,
                };

                for (int x = 0; x < columns + 1; x++)
                {
                    separators.AddVertical(new Vector2(topLeft.X + ColumnWidth * x, topLeft.Y), totalSize.Y, 0.001f);
                }

                for (int y = 0; y < rows + 1; y++)
                {
                    separators.AddHorizontal(new Vector2(topLeft.X, topLeft.Y + RowHeight * y), totalSize.X, 0.0015f);
                }

                screen.Controls.Add(separators);
            }
        }
    }
}
