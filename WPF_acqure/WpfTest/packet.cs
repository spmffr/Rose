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
  public class FrondPacket
  {
    private const int DefaultSamples = 510;

    private static readonly UInt16 STX = 0xa55a;
    private const int offsetStx = 0;
    private const int offsetIndex = 1;
    private const int offsetSamples = 2;
    private const int offsetRate = 3;
    public const int offsetData = 4;
    private const int MaxU16 = offsetData + DefaultSamples * 2;
    public FrondPacket()   // 
    {
      _buf = new UInt16[MaxU16];
      _buf[offsetStx] = STX;
      _buf[offsetIndex] = 0;
      _buf[offsetSamples] = DefaultSamples;
      _buf[offsetRate] = 10000;
    }

    public UInt16 Stx
    {
      get { return _buf[offsetStx]; }
      set { _buf[offsetStx] = value; }
    }
    public UInt16 Index
    {
      get { return _buf[offsetIndex]; }
      set { _buf[offsetIndex] = value; }
    }
    public UInt16 SamplesPerPacket
    {
      get { return _buf[offsetSamples]; }
      set { _buf[offsetSamples] = value; }
    }
    public UInt16 SamplingRate
    {
      get { return (UInt16)_buf[offsetRate]; }
      set { _buf[offsetRate] = value; }
    }
    public UInt16[] Samples
    {
      get { return _buf.Skip(offsetData).ToArray(); }
    }
    public ushort2_t[] SamplePairs
    {
      get
      {
        ushort2_t[] a = new ushort2_t[SamplesPerPacket];
        for (int i = 0; i < SamplesPerPacket; i++)
        {
          a[i].x = _buf[offsetData + 2 * i];
          a[i].y = _buf[offsetData + 2 * i + 1];
        }
        //Buffer.BlockCopy(_buf, offsetData, a, 0, 4 * SamplesPerPacket);
        return a;
      }
    }
    public UInt16[] Channel1
    {
      get
      {
        return _buf.Skip(offsetData).Take(510).ToArray();
      }
      private set { }
    }
    public UInt16[] Channel2
    {
      get
      {
        return _buf.Skip(offsetData + 510).ToArray();
      }
      private set { }
    }

    public FrondPacket(UInt16 samples)
    {
      SamplesPerPacket = samples;
    }

    public FrondPacket(byte[] ba)
    {
      _buf = new UInt16[MaxU16];
      int elements = ba.Count();

      //Debug.Assert(elements <= MaxU16 * 2);

      Buffer.BlockCopy(ba, 0, _buf, 0, elements);

      Debug.Assert(Stx == STX);
      //Debug.Assert(elements == SamplesPerPacket * 2 + 8);
      //Debug.WriteLine("Packet {0}, {1}", Index, elements);

    }

    public void SavePacket(BinaryWriter? writer)
    {
      if (null != writer)
      {
        Span<byte> bytes = MemoryMarshal.Cast<UInt16, byte>(_buf.Skip(16).ToArray().AsSpan());
        writer.Write(bytes);
        writer.Flush();
      }
    }
    private UInt16[] _buf;
  }

}
