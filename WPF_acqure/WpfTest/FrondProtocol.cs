using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Management;
using System.IO.Ports;
using System.Windows;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FrondScope
{
  public delegate void PacketDelegate(FrondPacket packet);
  public delegate void Connected();

  public class FrondProtocol : IDisposable
  {
    private SerialPort _port;

    private bool _bActive;
    private int _packet_size;
    //private Thread _thread;
    private List<byte[]> _buffer;
    private int _buf_index, _data_index;

    public event PacketDelegate PacketReceived;
    public event Connected Connected;

    public bool IsConnected { get; private set; }
    public FrondProtocol()
    {
      _packet_size = 2048;
      IsConnected = false;
      _buffer = new List<byte[]>();
      _buffer.Add(new byte[_packet_size]);
      _buffer.Add(new byte[_packet_size]);
      _buf_index = _data_index = 0;

      //_path = Directory.GetCurrentDirectory() + @"\Data\";

    }
    public void TryConnect()
    {
      ThreadPool.QueueUserWorkItem(o =>
      {
        taskConnection();
      });
    }
    private void taskConnection()
    {
      bool bContinue = true;

      while (bContinue)
      {
        string name = findPort();


        if (name != String.Empty)
        {
          _port = new SerialPort(name);
          _port.BaudRate = 921600;
          _port.DataBits = 8;
          _port.Handshake = Handshake.None;
          _port.StopBits = StopBits.One;
          _port.ReceivedBytesThreshold = 2048;
          _port.ReadBufferSize = 4 * 2048;
          _port.ReadTimeout = 500; // SerialPort.InfiniteTimeout;

          try
          {
            _port.Open();

            Send("STAT 0\r\n");  // stop streaming in case
            Thread.Sleep(100);

            _port.DiscardInBuffer();
            _port.DiscardOutBuffer();


            bContinue = false;
            IsConnected = true;
            _port.DataReceived += OnSerialData;
            _port.ErrorReceived += OnSerialError;
            Connected?.Invoke();
          }
          catch (Exception)
          {
            _port.Dispose();
            Thread.Sleep(500);
            continue;
          }
        }
      }

    }
    private void OnSerialError(object sender, SerialErrorReceivedEventArgs e)
    {
      System.Diagnostics.Debug.WriteLine("Port error");
    }
    public void Dispose()
    {
      if (null == _port)
        return;

      lock (_port)
      {
        _port.DataReceived -= OnSerialData;
        Thread.Sleep(1000);
        _port.Dispose();
        //_port = null;
      }
      _buffer.Clear();
    }
    public bool Active
    {
      get { return _bActive; }
      set
      {
        if (value)
        {
          //openArchiver();
          _data_index = _buf_index = 0;
          //_port.DataReceived += OnSerialData;
          SetAcquisitionState(true);
        }
        else
        {
          SetAcquisitionState(false);
          //_port.DataReceived -= OnSerialData;
          Thread.Sleep(1000);
          _port?.DiscardInBuffer();

          //CloseArchiver();
        }
      }
    }

    public bool Simulation
    {
      set
      {
        String cmd;
        if (value)
        {
          cmd = "SIMU 1\r\n";
        }
        else
        {
          cmd = "SIMU 0\r\n";
        }
        byte[] ba = Encoding.ASCII.GetBytes(cmd);
        _port.Write(ba, 0, ba.Count());
      }
    }

    public void SetSamplingRate(UInt16 freq)
    {
      String cmd = String.Format("RATE %d\r\n", freq);
      byte[] ba = Encoding.ASCII.GetBytes(cmd);

      _port.Write(ba, 0, ba.Count());

    }
    public void SetSamplesPerPacket(UInt16 samples)
    {
      String cmd = String.Format("SIZE %d\r\n", samples);
      byte[] ba = Encoding.ASCII.GetBytes(cmd);

      bool bPrev = SetAcquisitionState(false);
      _port.Write(ba, 0, ba.Count());

      if (bPrev)
        SetAcquisitionState(true);

    }
    public void Send(String msg)
    {
      byte[] ba = Encoding.ASCII.GetBytes(msg);
      try
      {
        _port.Write(ba, 0, ba.Count());
        Thread.Sleep(100);
      }
      catch (Exception) { }
    }


    private bool SetAcquisitionState(bool state)
    {
      bool bPrev = _bActive;
      if (state != _bActive)
      {
        String cmd = String.Format("STAT {0}\r\n", (state) ? 1 : 0);
        Send(cmd);
        _bActive = state;
      }
      return bPrev;
    }
    private void OnSerialData(object sender, SerialDataReceivedEventArgs e)
    {
      //SerialPort p = (SerialPort)sender;
      int n;

      try
      {
        try
        {
          lock (_port)
            n = _port.Read(_buffer[_buf_index], _data_index, _packet_size - _data_index);
        }
        catch (Exception ex)
        {
          return;
        }
        if (n > 0)
        {
          _data_index += n;
          if (_data_index == _packet_size)
          {
            _buf_index = 1 - _buf_index;
            _data_index = 0;
            try
            {
              if (false == ThreadPool.QueueUserWorkItem(o =>
              {
                FrondPacket packet = new FrondPacket(_buffer[1 - _buf_index]);
                PacketReceived?.Invoke(packet);
                //lock (_br) { packet.SavePacket(_br); }
              }))
                System.Diagnostics.Debug.WriteLine("Fail workitem");
            }
            catch (Exception ex)
            {
              System.Diagnostics.Debug.WriteLine("Except workitem");
            }

          }
        }
      }
      catch (Exception ex) { }
    }

    private String findPort()
    {
      String sz;
      try
      {
        ManagementObjectSearcher searcher =
            new ManagementObjectSearcher("root\\CIMV2",
            "SELECT * FROM Win32_SerialPort");

        foreach (ManagementObject queryObj in searcher.Get())
        {

          sz = queryObj["Name"].ToString();
          if (sz.Contains("USB Serial Device") ||
              sz.Contains("STMicroelectronics Virtual COM Port")
              )
            return queryObj["DeviceID"].ToString();
        }
      }
      catch (ManagementException e)
      {
        MessageBox.Show("An error occurred while querying for WMI data: " + e.Message);
      }
      return String.Empty;
    }

    //private static void DataThread(Object arg)
    //{
    //    FrondProtocol proto = (FrondProtocol)arg;
    //    try
    //    {
    //        proto.read();
    //    }
    //    catch (ThreadAbortException ex)
    //    {

    //    }
    //}
    //private string _path;
    //private BinaryWriter _br;
    //public string GetArchiveFolder()
    //{
    //  return _path;
    //}
    //private void openArchiver()
    //{
    //  string name = DateTime.Now.ToString("yyyy_M_dd-HH_mm_ss", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
    //  CloseArchiver();
    //  var stream = File.Open(_path +  name + ".bin", FileMode.Create);
    //  _br = new BinaryWriter(stream);
    //}

    //public void CloseArchiver()
    //{
    //  if (null != _br)
    //  {
    //    lock (_br)
    //    {
    //      _br.Flush();
    //      _br.Close();
    //    }
    //  }
    //  _br = null;
    //}

  }
}
