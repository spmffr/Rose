using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Configuration;
using FrondScope;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using DocumentFormat.OpenXml.Spreadsheet;

namespace FrondScope
{
  //using Settings = FrondScope.Properties.Settings;
  public class LastQ<T> : Queue<T>
  {
    public T Last { get; private set; }

    public new void Enqueue(T item)
    {
      Last = item;
      base.Enqueue(item);
    }
  }

  public partial class Process
  {
    private param_t param;
    private state_t state;

    float2_t[] output;
    //private void readData()
    //{
    //  string filename = @"e:\frondmed\adc.bin";
    //  int n = (int)new FileInfo(filename).Length;
    //  byte[] ba = new byte[n];
    //  using (var stream = File.Open(filename, FileMode.Open))
    //  {
    //    using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
    //    {
    //      ba = reader.ReadBytes(n);
    //    }
    //  }
    //  n /= 4;
    //  ushort2_t adc;
    //  for (int i = 0; i < n; i++)
    //  {
    //    adc.x = (UInt16)(32768 + BitConverter.ToInt16(ba, i * 4));
    //    adc.y = (UInt16)(32768 + BitConverter.ToInt16(ba, i * 4 + 2));
    //    adcQueue.Enqueue(adc);
    //  }
    //}
    private void initParams()
    {
      string filename = _folder + Settings.Default.Barcode1;
      int n = (int)new FileInfo(filename).Length;
      byte[] ba = new byte[n];

      using (var stream = File.Open(filename, FileMode.Open))
      {
        using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
        {
          ba = reader.ReadBytes(n);
        }
      }
      n /= 8;
      p_barcode1 = new float2_t[n];
      for (int i = 0; i < n; i++)
      {
        p_barcode1[i].x = BitConverter.ToSingle(ba, i * 8);
        p_barcode1[i].y = BitConverter.ToSingle(ba, i * 8 + 4);
      }
      param.nBarcode1 = n;

      filename = _folder + Settings.Default.Barcode2;
      n = (int)new FileInfo(filename).Length;

      Array.Resize<byte>(ref ba, n);
      using (var stream = File.Open(filename, FileMode.Open))
      {
        using (var reader = new BinaryReader(stream, Encoding.UTF8, false))
        {
          ba = reader.ReadBytes(n);
        }
      }
      n /= 8;
      p_barcode2 = new float2_t[n];
      for (int i = 0; i < n; i++)
      {
        p_barcode2[i].x = BitConverter.ToSingle(ba, i * 8);
        p_barcode2[i].y = BitConverter.ToSingle(ba, i * 8 + 4);
      }
      param.nBarcode2 = n;

      // default
      param.adcFs_KHz = Settings.Default.adcKHz; // 50.0f;
      param.adcThreshold_0_1 = Settings.Default.Thresh_0_1;
      param.adcThreshold_1_0 = Settings.Default.Thresh_1_0;
      param.hiResEnable = Settings.Default.hiResEnable;
      //param.nBarcode1 = Settings.Default.Barcode1;
      //param.nBarcode2 = Settings.Default.BarCode2;

      state.inputCnt = 0;
      state.prevCh0Level = 0;
      state.prevCh1Level = 0;
      state.tCnt = 0;
      state.prevState = -1; // not defined   
      state.prevDir = 0;    // not defined   
      state.prevPos = 0;
      state.prevPos2 = 0;

      _min_move = Settings.Default.MinMove_mm;
    }

  } // process

  public static class DataLog
  {
    private static string _path = Directory.GetCurrentDirectory() + @"\Data\";
    private static BinaryWriter? _brAdc = null;
    private static BinaryWriter? _brLoc = null;
    private static BinaryWriter? _brClean = null;

    public static string ArchiveFolder
    {
      get { return _path; }
      private set { _path = value; }
    }
    public static void Open()
    {
      string name = DateTime.Now.ToString("yyyy_M_dd-HH_mm_ss", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
      Close();
      var stream = File.Open(_path + name + ".adc", FileMode.Create);
      _brAdc = new BinaryWriter(stream);

      var stream1 = File.Open(_path + name + ".loc", FileMode.Create);
      _brLoc = new BinaryWriter(stream1);
    }

    public static void Open(String inputFile)
    {
      Close();

      var stream1 = File.Open(_path + inputFile + ".loc", FileMode.Create);
      _brLoc = new BinaryWriter(stream1);

      var stream2 = File.Open(_path + inputFile + ".clean", FileMode.Create);
      _brClean = new BinaryWriter(stream2);
    }

    public static void Close()
    {
      if (null != _brAdc)
      {
        lock (_brAdc)
        {
          _brAdc.Flush();
          _brAdc.Close();
        }
        _brAdc = null;
      }
      if (null != _brLoc)
      {
        lock (_brLoc)
        {
          _brLoc.Flush();
          _brLoc.Close();
        }
        _brLoc = null;
      }
      if (null != _brClean)
      {
        lock (_brClean)
        {
          _brClean.Flush();
          _brClean.Close();
        }
        _brClean = null;
      }
    }

    public static void SavePacket(FrondPacket packet)
    {
      packet.SavePacket(_brAdc);
    }

    public static void SaveLocation(float2_t[] location)
    {
      if (null != _brLoc)
      {
        foreach (float2_t x in location)
        {
          _brLoc.Write(x.x);
          _brLoc.Write(x.y);
        }
      }
    }

    public static void SaveClean(short2_t x)
    {
      _brClean?.Write(x.x);
      _brClean?.Write(x.y);

    }
  }
}
