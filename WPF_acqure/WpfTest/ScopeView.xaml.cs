using InteractiveDataDisplay.WPF;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using FrondScope.Models;
using ICG.NetCore.Utilities.Spreadsheet;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using Path = System.IO.Path;

namespace FrondScope
{
  /// <summary>
  /// Interaction logic for ScopeView.xaml
  /// </summary>
  public partial class ScopeView : UserControl, INotifyPropertyChanged
  {
    private MainWindow _wnd;
    private LineGraph _lg1;
    private LineGraph _lg2;

    private float[] _x;   // time
    private List<UInt16> _y;  // channel 0 data

    private bool _active = false;  // acquisition state
    private static readonly int DisplaySamples = 2500;
    private static readonly int SampleBufLen = 100000;  // 2S data = 2x50000 / 25000
    private int _decim;
    private string _timex;

    public bool InActive { get; set; }
    public bool Adjustable { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged(string propertyName)
    {
      if (PropertyChanged != null)
      {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }
    }

    public string TimeX
    {
      get
      {
        return _timex;
      }
      set
      {
        _timex = value;
        float t_max = float.Parse(_timex);
        _decim = (int)(t_max * 25000 / DisplaySamples);
        for (int i = 0; i < _x.Length; i++)
        {
          _x[i] = t_max * i / (_x.Length - 1);
        }
        NotifyPropertyChanged("TimeX");
      }
    }
    public ScopeView(MainWindow window)
    {
      _wnd = window;
      Adjustable = false;
      InActive = true;

      InitializeComponent();
      _lg1 = new LineGraph();
      _lg2 = new LineGraph();

      _x = new float[DisplaySamples];
      _y = new List<UInt16>(SampleBufLen);   // 2 channels, max samples6
      _decim = 10;
      //_decim = (SampleBufLen / 2) / DisplaySamples;
      for (int i = 0; i < SampleBufLen; i++)
      {
        _y.Add(0);
      }

      plotter.IsAutoFitEnabled = true;
      plotter.IsXAxisReversed = false;
      //plotter.PlotWidth = 0.5;
      //plotter.PlotHeight = 0x20000;
      //plotter.PlotOriginY = 0;
      //plotter.PlotOriginX = 0;
      //plotter.MaxHeight = 1;
      //plotter.MinHeight = 0;
      lines.Children.Add(_lg1);
      _lg1.Stroke = new SolidColorBrush(Color.FromRgb(255, 0, 0));
      _lg1.Description = String.Format("Channel 0");
      _lg1.StrokeThickness = 2;
      _lg1.Padding = new Thickness(30, 0, 0, 0);

      //lines.Children.Add(_lg2);
      _lg2.Stroke = new SolidColorBrush(Color.FromRgb(0, 255, 0));
      _lg2.Description = String.Format("Channel 1");
      _lg2.StrokeThickness = 2;
      _lg2.Padding = new Thickness(30, 0, 0, 0);

      plotter1.IsAutoFitEnabled = true;
      plotter1.IsXAxisReversed = false;

      lines1.Children.Add(_lg2);

      TimeX = "2.0";

    }
    private void Window_Closed(object sender, EventArgs e)
    {
      lines.Children.Remove(_lg1);
      lines1.Children.Remove(_lg2);
      _y.Clear();

    }
    public void Reset()
    {
      //lines.Children.Remove(_lg1);
      //lines1.Children.Remove(_lg2);
      for (int i = 0; i < SampleBufLen; i++)
      {
        _y[i] = 0;
      }
    }
    public void PacketHandler(FrondPacket pa)
    {
      //int n = 2 * pa.SamplesPerPacket;
      UInt16[] samples = pa.Samples;
      int n = samples.Length;

      lock (_y)
      {
        _y.InsertRange(0, samples);
        _y.RemoveRange(SampleBufLen - n - 1, n);
      }
        //_y.RemoveRange(0, n);
        //_y.AddRange(samples);

      Dispatcher.Invoke(new Action(() =>
      {
        try
        {
          lock (_y)
          {
            _lg1.PlotY(_x, _y.ToArray(), 0, 2 * _decim, 0.0);
            _lg2.PlotY(_x, _y.ToArray(), 1, 2 * _decim, 0.0);
          }
        }
        catch (Exception) { }
      }
      ));
      
    }

  }
}
