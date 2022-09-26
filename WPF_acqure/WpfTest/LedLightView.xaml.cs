using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FrondScope
{
  /// <summary>
  ///     Interaction logic for LedLightView.xaml
  /// </summary>
  public partial class LedLightView : UserControl
  {
    #region Constructors And Destructors

    public LedLightView()
    {
      InitializeComponent();
    }

    #endregion

    #region Properties

    public Color FillColor
    {
      get
      {
        var value = GetValue(FillColorProperty);
        if (value != null) return (Color)value;
        else return _defaultFillColor;
      }
      set => SetValue(FillColorProperty, value);
    }

    public Color LedBorderColor
    {
      get
      {
        var value = GetValue(LedBorderColorProperty);
        if (value != null) return (Color)value;
        return _defaultLedBorderColor;
      }
      set => SetValue(LedBorderColorProperty, value);
    }

    public double LedBorderThickness
    {
      get
      {
        var value = GetValue(LedBorderThicknessProperty);
        if (value != null) return (double)value;
        return _defaultLedBorderWidth;
      }
      set => SetValue(LedBorderThicknessProperty, value);
    }

    public bool ShowLight
    {
      get
      {
        var value = GetValue(ShowLightProperty);
        return value != null && (bool)value;
      }
      set => SetValue(ShowLightProperty, value);
    }

    #endregion

    #region Non-Public Methods

    private static void OnShowLightPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var ledLight = d as LedLightView;

      if (ledLight == null)
        return;

      var showLight = (bool)e.NewValue;

      if (showLight)
        ledLight.UxLedEllipse.Visibility = Visibility.Visible;
      else
        ledLight.UxLedEllipse.Visibility = Visibility.Hidden;
    }

    private static void OnFillColorPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var ledLight = d as LedLightView;

      if (ledLight == null)
        return;

      var color = e.NewValue as Color?;
      ledLight.UxLedEllipse.Fill = color != null ? new SolidColorBrush(color.Value) : null;
    }

    private static void OnLedBorderColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var ledLight = d as LedLightView;

      if (ledLight == null)
        return;

      var color = e.NewValue as Color?;

      ledLight.UxLedEllipseBorder.Stroke = color != null ? new SolidColorBrush(color.Value) : null;
    }

    #endregion

    #region Dependency Properties

    public static readonly DependencyProperty ShowLightProperty =
        DependencyProperty.Register("ShowLight", typeof(bool), typeof(LedLightView),
            new PropertyMetadata(default(bool), OnShowLightPropertyChanged));

    public static readonly DependencyProperty FillColorProperty =
        DependencyProperty.Register("FillColor", typeof(Color), typeof(LedLightView),
            new PropertyMetadata(default(Color), OnFillColorPropertyChanged));

    public static readonly DependencyProperty LedBorderColorProperty =
        DependencyProperty.Register("LedBorderColor", typeof(Color), typeof(LedLightView),
            new PropertyMetadata(default(Color), OnLedBorderColorChanged));

    public static readonly DependencyProperty LedBorderThicknessProperty =
        DependencyProperty.Register("LedBorderThickness", typeof(double), typeof(LedLightView),
            new PropertyMetadata(default(double), OnLedBorderThicknessChanged));

    private Color _defaultFillColor = Colors.Green;
    private double _defaultLedBorderWidth = 10;
    private Color _defaultLedBorderColor = Colors.Black;

    private static void OnLedBorderThicknessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
      var ledLight = d as LedLightView;

      if (ledLight == null)
        return;

      var thickness = e.NewValue as double?;
      if (thickness != null)
        ledLight.UxLedEllipseBorder.StrokeThickness = thickness.Value;
      else
      {
        ledLight.UxLedEllipseBorder.StrokeThickness = 0;
      }
    }

    #endregion
  }
}