using System;
using System.Collections.Generic;
using System.Linq;
using Contr.UI.Common.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace Contr.UI.Common.Controls;

/// <summary>A line chart of recent values over three faint grid lines, newest on the right.</summary>
public sealed partial class Sparkline : UserControl
{
    public static readonly DependencyProperty ValuesProperty = DependencyProperty.Register(
        nameof(Values), typeof(IReadOnlyList<double>), typeof(Sparkline), new PropertyMetadata(null, OnChanged));

    /// <summary>The value at the top edge; zero scales to the largest value.</summary>
    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(Sparkline), new PropertyMetadata(0.0, OnChanged));

    public static readonly DependencyProperty StrokeProperty = DependencyProperty.Register(
        nameof(Stroke), typeof(Brush), typeof(Sparkline), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty IsFilledProperty = DependencyProperty.Register(
        nameof(IsFilled), typeof(bool), typeof(Sparkline), new PropertyMetadata(false, OnChanged));

    public static readonly DependencyProperty IsDashedProperty = DependencyProperty.Register(
        nameof(IsDashed), typeof(bool), typeof(Sparkline), new PropertyMetadata(false, OnChanged));

    public static readonly DependencyProperty ShowsGridProperty = DependencyProperty.Register(
        nameof(ShowsGrid), typeof(bool), typeof(Sparkline), new PropertyMetadata(true, OnChanged));

    /// <summary>Headroom above the largest value, so the line never touches the top edge.</summary>
    private const double _headroom = 1.25;

    private const int _gridLines = 3;

    private readonly Canvas _canvas = new();

    public Sparkline()
    {
        IsTabStop = false;
        Content = _canvas;
        SizeChanged += OnSizeChanged;
        ActualThemeChanged += OnActualThemeChanged;
    }

    public IReadOnlyList<double>? Values
    {
        get => (IReadOnlyList<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public Brush? Stroke
    {
        get => (Brush?)GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public bool IsFilled
    {
        get => (bool)GetValue(IsFilledProperty);
        set => SetValue(IsFilledProperty, value);
    }

    public bool IsDashed
    {
        get => (bool)GetValue(IsDashedProperty);
        set => SetValue(IsDashedProperty, value);
    }

    public bool ShowsGrid
    {
        get => (bool)GetValue(ShowsGridProperty);
        set => SetValue(ShowsGridProperty, value);
    }

    private static void OnChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        ((Sparkline)sender).Draw();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Draw();
    }

    private void OnActualThemeChanged(FrameworkElement sender, object args)
    {
        Draw();
    }

    private void Draw()
    {
        _canvas.Children.Clear();
        var width = ActualWidth;
        var height = ActualHeight;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        if (ShowsGrid)
        {
            DrawGrid(width, height);
        }

        var values = Values ?? [];

        if (values.Count < 2)
        {
            return;
        }

        var top = Maximum > 0 ? Maximum : values.Max() * _headroom;
        top = top > 0 ? top : 1;
        var step = width / (values.Count - 1);
        var points = new PointCollection();
        for (var index = 0; index < values.Count; index++)
        {
            var share = Math.Clamp(values[index] / top, 0, 1);
            points.Add(new Point(index * step, height - (share * height)));
        }

        var stroke = Stroke ?? ThemeResources.Brush(this, "AccentFillColorDefaultBrush");

        if (IsFilled)
        {
            var area = new PointCollection { new Point(0, height) };
            foreach (var point in points)
            {
                area.Add(point);
            }

            area.Add(new Point(width, height));
            _canvas.Children.Add(new Polygon {
                Points = area,
                Fill = stroke,
                Opacity = 0.25,
            });
        }

        var line = new Polyline {
            Points = points,
            Stroke = stroke,
            StrokeThickness = 1.5,
            StrokeLineJoin = PenLineJoin.Round,
        };

        if (IsDashed)
        {
            line.StrokeDashArray = [3, 2];
        }

        _canvas.Children.Add(line);
    }

    private void DrawGrid(double width, double height)
    {
        var brush = ThemeResources.Brush(this, "DividerStrokeColorDefaultBrush");
        for (var index = 0; index < _gridLines; index++)
        {
            var y = Math.Round(height * index / _gridLines) + 0.5;
            _canvas.Children.Add(new Line {
                X1 = 0,
                X2 = width,
                Y1 = y,
                Y2 = y,
                Stroke = brush,
                StrokeThickness = 1,
            });
        }
    }
}
