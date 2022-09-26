// Copyright (c) Microsoft Corporation. All Rights Reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using InteractiveDataDisplay.WPF;
using ICG.NetCore.Utilities.Spreadsheet;
using Microsoft.Extensions.DependencyInjection;
using FrondScope.Models;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.ComponentModel;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using System.DirectoryServices.ActiveDirectory;
using System.Runtime.CompilerServices;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Windows.Controls;
using System.Xml;
using Microsoft.Win32;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FrondScope
{


    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
  public partial class MainWindow : Window, INotifyPropertyChanged
  {
    private static FrondProtocol _protocol = new FrondProtocol();

    public ScopeView ScopeView { get; private set; }
    public DetectView DetectView { get; private set; }

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
    public bool Simulated { get; set; }

    public MainWindow()
    {
      Simulated = false;
      InActive = true;
      Adjustable = false;
      InitializeComponent();
      DataContext = this;
      ScopeView = new ScopeView(this);
      DetectView = new DetectView(this);
      _protocol.Connected += connectedHandler;
    }

    private void WindowLoaded(object sender, RoutedEventArgs e)
    {
      btnScope.IsChecked = true;
      btnDetect.IsChecked = false;
      DisplayedUserControl.Content = ScopeView;
      DataContext = ScopeView;
      _protocol.TryConnect();
      //_protocol.PacketReceived += DetectView.PacketHandler;

    }

    private void connectedHandler()
    {
      string cmd = string.Format("SIMU {0}\r\n", Simulated ? 1 : 0);
      _protocol.Send("STAT 0\r\n");  // stop streaming in case

      Dispatcher.Invoke(() => { btnStart.IsEnabled = true; });
    }
    // disposal
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
      Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
      _protocol.Active = false;
      Thread.Sleep(500);
      Thread CloseDown = new Thread(new ThreadStart(CloseSerialOnExit)); //close port in new thread 
      CloseDown.Start();

      DetectView.EndProcessing();

      LayoutRoot.Children.Remove(ScopeView);
      LayoutRoot.Children.Remove(DetectView);


      //System.IO.File.Delete(_protocol.GetArchiveFolder());
    }

    private void CloseSerialOnExit()
    {
      try
      {
        _protocol.Dispose();
        Mouse.OverrideCursor = null;
      }
      catch (Exception ex)
      {
      }
    }

    private void btnScope_Click(object sender, RoutedEventArgs e)
    {
      btnScope.IsChecked = true;
      btnDetect.IsChecked = false;

      DisplayedUserControl.Content = ScopeView;
      //DataContext = ScopeView;
      _protocol.PacketReceived -= DetectView.PacketHandler;
      _protocol.PacketReceived += ScopeView.PacketHandler;
    }
    private void btnLocation_Click(object sender, RoutedEventArgs e)
    {
      btnScope.IsChecked = false;
      btnDetect.IsChecked = true;

      DisplayedUserControl.Content = DetectView;
      //DataContext = DetectView;
      _protocol.PacketReceived -= ScopeView.PacketHandler;
      _protocol.PacketReceived += DetectView.PacketHandler;
    }

    // Start/Stop
    private void btnStart_Click(object sender, RoutedEventArgs e)
    {
      bool btnState = (bool)btnStart?.IsChecked;
      if (!btnState)
      {
        startAcquisition(false);
        btnSave.IsEnabled = true;
        btnStart.Content = "Start";
        if (ScopeView == DisplayedUserControl.Content)
          ScopeView.InActive = false;
        else
          DetectView.InActive = true;
        btnScope.IsEnabled = btnDetect.IsEnabled = true;
      }
      else
      {
        btnScope.IsEnabled = btnDetect.IsEnabled = false;

        _protocol.Simulation = Simulated;

        startAcquisition(true);
        btnSave.IsEnabled = false;
        btnStart.Content = "Stop";
        if (ScopeView == DisplayedUserControl.Content)
          ScopeView.InActive = false;
        else
          DetectView.InActive = true;
      }

    }
    // export data to excel
    private void btnSave_Export(object sender, RoutedEventArgs e)
    {
      string path = string.Empty;  //_protocol.GetArchiveFolder() + stamp.ToString("dd-MM-YYYY_hh-mm-ss") + ".xlsx";
      var services = new ServiceCollection();
      services.UseIcgNetCoreUtilitiesSpreadsheet();
      var provider = services.BuildServiceProvider();

      Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
      //_protocol.CloseArchiver();

      //Get our generator and export
      var exportGenerator = provider.GetService<ISpreadsheetGenerator>();
      var exportDefinition = new SpreadsheetConfiguration<SimpleExportData>
      {
        RenderTitle = true,
        DocumentTitle = "Frond Medical",
        RenderSubTitle = true,
        DocumentSubTitle = "Raw ADC data",
        ExportData = GetSampleExportData(ref path),
        WorksheetName = "Export"
      };
      var fileContent = exportGenerator.CreateSingleSheetSpreadsheet(exportDefinition);

      System.IO.File.WriteAllBytes(path, fileContent);
      Mouse.OverrideCursor = null;
    }
    private List<SimpleExportData> GetSampleExportData(ref string name)
    {
      string fileName = string.Empty;
      var listData = new List<SimpleExportData>();
      uint t, dt;

      Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog();

      {
        //openFileDialog.InitialDirectory = _protocol.GetArchiveFolder();
        openFileDialog.Filter = "bin files (*.bin)|*.bin|All files (*.*)|*.*";
        openFileDialog.FilterIndex = 2;
        openFileDialog.RestoreDirectory = true;

        bool? result = openFileDialog.ShowDialog();
        if (result == true)
        {
          //Get the path of specified file
          fileName = openFileDialog.FileName;
          using (var stream = File.Open(fileName, FileMode.Open))
          {
            using (var reader = new BinaryReader(stream))
            {
              t = 0;
              while (true)
              {
                byte[] ba = reader.ReadBytes(2040);
                if (ba != null && ba.Count() == 2040)
                {
                  UInt16[] us = new ushort[1020];
                  dt = 1000000 / 25000;
                  Buffer.BlockCopy(ba, 0, us, 0, 2040);
                  for (int i = 0; i < 510; i++)
                  {
                    listData.Add(new SimpleExportData { Time = t, ch0 = us[2 * i], ch1 = us[2 * i + 1] });
                    t += dt;
                  }
                }
                else
                  break;
              } // while
            } // BinaryReader
          } // using stream
        }
      }
      if (string.Empty != fileName)
      {
        string extension = Path.GetExtension(fileName);
        name = fileName.Substring(0, fileName.Length - extension.Length) + ".xlsx";
      }
      return listData;
    }

    private void startAcquisition(bool state)
    {
      if (state)
      {
        DataLog.Open();
        btnSave.IsEnabled = false;
        btnStart.Content = "Stop";
        _protocol.Active = true;
      }
      else
      {
        _protocol.Active = false;
        btnSave.IsEnabled = true;
        btnStart.Content = "Start";
        DataLog.Close();
      }
    }

    private void cbSimulate_Checked(object sender, RoutedEventArgs e)
    {
      Simulated = true;
    }

    private void cbSimulate_Unchecked(object sender, RoutedEventArgs e)
    {
      Simulated = false;

    }

    /// <summary>
    /// LOcal file based simulation
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void btnSim_Click(object sender, RoutedEventArgs e)
    {
      Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
      dlg.DefaultExt = ".bin"; // Default file extension
      dlg.Filter = "Data FIless (.bin)|*.bin"; // Filter files by extension

      Nullable<bool> result = dlg.ShowDialog();

      if (result == true)
      {
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        // Open document
          byte[] ba = File.ReadAllBytes(dlg.FileName);
        ushort2_t s;

          DataLog.Open(System.IO.Path.GetFileNameWithoutExtension(dlg.FileName));

          Process detect = new Process();
          Preprocess clean = new Preprocess();
          clean.PreprocessReady += detect.OnSample;
          detect.LocalSim = true;
          int nPairs = ba.Length / 4;
          for (int i = 0; i < nPairs; i++)
          {
            s.x = BitConverter.ToUInt16(ba, i * 4);
            s.y = BitConverter.ToUInt16(ba, i * 4 + 2);
            clean.OnSample(s);
          }
          Thread.Sleep(2000);
          //do { Thread.Sleep(100); }
          //while (detect.ProcessCnt < nPairs - 2);
          clean.End();

          DataLog.SaveLocation(detect.GetResult());
          detect.End();

          DataLog.Close();
        Mouse.OverrideCursor = null;

      }
    }
  }

  public class VisibilityToCheckedConverter : IValueConverter
  {
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      return ((Visibility)value) == Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
      return ((bool)value) ? Visibility.Visible : Visibility.Collapsed;
    }
  }
}
