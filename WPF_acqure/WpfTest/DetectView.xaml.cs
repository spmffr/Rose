using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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
using System.Windows.Threading;
using System.Configuration;

namespace FrondScope
{
  //using Settings = FrondScope.Properties.Settings;
  public partial class DetectView : UserControl, INotifyPropertyChanged
  {
    private MainWindow _wnd;
    private Preprocess _cleaner;

    private Process _process;
    private Point pnt1, pnt2;
    private double zeroX;
    private Int16 _T1;
    private Int16 _T2;
    public PointCollection myPointCollection { get; set; }

    public bool InActive { get; set; }
    public bool Adjustable { get; set; }
    public Int16 T1
    {
      get { return _T1; }
      set
      {
        _T1 = value;
        NotifyPropertyChanged("T1");
      }
    }
    public Int16 T2
    {
      get { return _T2; }
      set
      {
        _T2 = value;
        NotifyPropertyChanged("T2");
      }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyPropertyChanged(string propertyName)
    {
      if (PropertyChanged != null)
      {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
      }
    }

    public DetectView(MainWindow window)
    {
      Adjustable = true;
      InActive = false;
      _wnd = window;
      myPointCollection = new PointCollection
        {
            new Point(0, 645),
            new Point(20, 645),
            new Point(0, 695)
        };
      pnt1 = new Point(100, 0);
      pnt2 = new Point(0, 800);
      zeroX = 0;
      //
      //_T1 = 100;    // 0->1
      //_T2 = -100;   // 1->0
      T1 = Settings.Default.Thresh_0_1;
      T2 = Settings.Default.Thresh_1_0;

      _process = new Process();
      _process.LocationChanged += this.ChangePosition;
      _process.SetThreshold(_T1, _T2);

      _cleaner = new Preprocess();
      _cleaner.PreprocessReady += _process.OnSample;

      InitializeComponent();
      DataContext = this;
    }
    public void EndProcessing()
    {
      _cleaner.End();
      _process.End();
    }
    public void PacketHandler(FrondPacket pa)
    {
      _cleaner.OnPacket(pa);
    }
    public void ChangePosition(float2_t x)
    {
      double newx = 1480 * x.x /247.5; //(0.5 + x.x) * 840 / 200;

      Dispatcher.Invoke(new Action(() =>
      {
        //System.Diagnostics.Debug.WriteLine("{0:F2}", x.x);
        DataContext = null;
        Point p0 = new Point(Math.Max(0, newx - 20), 645);
        Point p1 = new Point(Math.Min(1490, newx + 20), 645);
        Point p2 = new Point(newx, 695);

        myPointCollection[0] = p0;
        myPointCollection[1] = p1;
        myPointCollection[2] = p2;

        txtPos.Content = String.Format("@{0:F2}, DIST={1:F2} mm ", x.x, (x.x - zeroX));
        //zero.X1 = zero.X2 = newx;

        DataContext = this;
      }
      ));
    }
    private void canvus_Loaded(object sender, RoutedEventArgs e)
    {
      ChangePosition(new float2_t(0.0f, 0.0f));
    }

    private void ruler_MouseDown(object sender, MouseButtonEventArgs e)
    {
      Point p = e.GetPosition(this);

      zeroX = (p.X - 10) * 24.725 / 1480;
      Dispatcher.Invoke(new Action(() =>
      {
        zero.X1 = zero.X2 = p.X;
      }));
      ChangePosition(new float2_t((float)zeroX, 0));
    }

    private void canvus_MouseDown(object sender, MouseButtonEventArgs e)
    {
      Point p = e.GetPosition(canvus);
    }

    private void Window_Closed(object sender, EventArgs e)
    {
      _process.End();
    }
  }

}
