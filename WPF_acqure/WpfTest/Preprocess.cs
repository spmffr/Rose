using FrondScope;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Diagnostics;

namespace FrondScope
{
  public delegate void Preprocessed(short2_t xn);
  public class Preprocess
  {
    public event Preprocessed? PreprocessReady;

    private float[] arg;
    private cleanParam_t cleanParam;
    private cleanState_t cleanState;

    private BlockingCollection<ushort2_t> adcQueue;
    private BlockingCollection<ushort2_t> medianQueue;
    private BlockingCollection<float2_t> filterQueue;
    //private BlockingCollection<short2_t> clipQueue;

    private Thread? thd0, thd1, thd2;
    //private Process? _tail;

    public Preprocess()
    {
      arg = new float[2];
      cleanParam = new cleanParam_t();  // TODO - read from config
      cleanState = new cleanState_t(cleanParam);

      adcQueue = new BlockingCollection<ushort2_t>();
      medianQueue = new BlockingCollection<ushort2_t>();
      filterQueue = new BlockingCollection<float2_t>();

      thd0 = new Thread(this.cleanProcess0);
      thd1 = new Thread(this.cleanProcess1);
      thd2 = new Thread(this.cleanProcess2);
      thd0.Start();
      thd1.Start();
      thd2.Start();

    }

    public void End()
    {
      thd0.Interrupt();
      thd1.Interrupt();
      thd2.Interrupt();
      thd0.Join();
      thd1.Join();
      thd2.Join();
    }

    public void Reset()
    {
      End();
      adcQueue.TakeWhile <ushort2_t> (qItem => true);
      medianQueue.TakeWhile<ushort2_t>(qItem => true);
      filterQueue.TakeWhile<float2_t>(qItem => true);
      cleanState = new cleanState_t(cleanParam);
      thd0 = new Thread(this.cleanProcess0);
      thd1 = new Thread(this.cleanProcess1);
      thd2 = new Thread(this.cleanProcess2);
      thd0.Start();
      thd1.Start();
      thd2.Start();
    }
    //public void Simulate(byte[] ba)
    //{
    //  ushort2_t s;
    //  int nPair = ba.Length / 4;

    //  for (int i = 0; i < nPair; i++)
    //  {
    //    s.x = BitConverter.ToUInt16(ba, i * 4);
    //    s.y = BitConverter.ToUInt16(ba, i * 4 + 2);
    //    adcQueue.Add(s);
    //  }

    //  thd0.Interrupt();
    //  thd1.Interrupt();
    //  thd2.Interrupt();
    //  thd0.Join();
    //  thd1.Join();
    //  thd2.Join();

    //using (var stream = File.Open(@"e:\AL\output.bin", FileMode.Create))
    //{
    //  using (BinaryWriter bw = new BinaryWriter(stream))
    //  {
    //    while (clipQueue.Count > 0)
    //    {
    //      short2_t s2 = clipQueue.Take();
    //      bw.Write(s2.x);
    //      bw.Write(s2.y);
    //    }
    //  }
    //}
    //}

    /// <summary>
    /// This is packet handler for data received from the board
    /// </summary>
    /// <param name="pa"></param>
    public void OnPacket(FrondPacket pa)
    {
      int n = pa.SamplesPerPacket;
      UInt16[] sampleArray = pa.Samples;
      ushort2_t s;

      for (int i = 0; i < n; i++)
      {
        s.x = sampleArray[2 * i];
        s.y = sampleArray[2 * i + 1];
        adcQueue.Add(s);
      }

      DataLog.SavePacket(pa);
    }

    /// <summary>
    /// This is used in local simulation
    /// </summary>
    /// <param name="pa"></param>
    public void OnSample(ushort2_t sample)
    {
      adcQueue.Add(sample);
    }

    public void cleanProcess0()
    {
      ushort2_t xn;
      while (true)
      {
        try
        {
          if (adcQueue.TryTake(out xn, 1))
            process0(xn);
        }
        catch (ThreadInterruptedException)
        {
          break;
        }
      }
    }

    public void process0(ushort2_t xn)
    {

      int inCnt = cleanState.medianInputCnt;

      if (inCnt < cleanState.medianROS)
      {
        //cleanState.sort0[inCnt] = ((uint)xn.x << 16) | (uint)inCnt;
        //cleanState.sort1[inCnt] = ((uint)xn.y << 16) | (uint)inCnt;

        cleanState.sort0[inCnt].value = xn.x;
        cleanState.sort0[inCnt].index = (ushort)inCnt;
        cleanState.sort1[inCnt].value = xn.y;
        cleanState.sort1[inCnt].index = (ushort)inCnt;

        cleanState.medianInputCnt += 1;
      }
      else
      {
        ushort2_t yn;
        cleanState.insertionSort0();
        cleanState.insertionSort1();

        cleanState.MedianValue(out yn);

        medianQueue.Add(yn);
        cleanState.medianOutputCnt += 1;

        cleanState.insertSample0(xn.x);
        cleanState.insertSample1(xn.y);
        cleanState.medianInputCnt += 1;
      }
    }  // process0
    public void cleanProcess1()
    {
      ushort2_t xn;
      while (true)
      {
        try
        {
          if (medianQueue.TryTake(out xn, 1))
            process1(xn);
        }
        catch (ThreadInterruptedException)
        {
          break;
        }
      }
    }
    public void process1(ushort2_t xn)
    {
      float2_t zn;
      zn.x = xn.x * cleanParam.Qadc;
      zn.y = xn.y * cleanParam.Qadc;

      int inCnt = cleanState.filterInputCnt;

      if (inCnt < cleanState.filterROS)
      {
        cleanState.filterData[cleanState.filterHead] = zn;
        cleanState.filterInputCnt += 1;
        cleanState.filterHead += 1;
        cleanState.filterHead = (cleanState.filterHead == cleanState.filterROS) ? 0 : cleanState.filterHead;
      }
      else
      {
        float2_t xsum;
        xsum.x = 0;
        xsum.y = 0;
        int ptr = cleanState.filterTail;
        for (int k = 0; k < cleanState.filterROS; k++)
        {
          float coef = cleanState.filterCoef[k];
          float2_t an = cleanState.filterData[ptr];
          xsum.x += an.x * coef;
          xsum.y += an.y * coef;
          ptr += 1;
          ptr = (ptr == cleanState.filterROS) ? 0 : ptr;
        }

        filterQueue.Add(xsum);
        cleanState.filterOutputCnt += 1;

        ptr = cleanState.filterTail + 1;
        cleanState.filterTail = (ptr == cleanState.filterROS) ? 0 : ptr;

        cleanState.filterData[cleanState.filterHead] = zn;
        cleanState.filterInputCnt += 1;
        cleanState.filterHead += 1;
        cleanState.filterHead = (cleanState.filterHead == cleanState.filterROS) ? 0 : cleanState.filterHead;

      }
    }

    public void cleanProcess2()
    {
      float2_t xn; 
      while (true)
      {
        try
        {
          if (filterQueue.TryTake(out xn, 1))
            process2(xn);
        }
        catch (ThreadInterruptedException)
        {
          break;
        }
      }
    }
    public void process2(float2_t xn)
    {

      for (int kch = 0; kch < 2; kch++)
      {
        float xd = (kch == 0) ? xn.x : xn.y;

        if (xd < cleanState.thresCh[kch])
        {     // ch kch is low
          cleanState.levelIirCh[kch].x = cleanParam.levelIirCoef * cleanState.levelIirCh[kch].x + (1 - cleanParam.levelIirCoef) * xd;
        }
        else
        {                                // ch kch is high
          cleanState.levelIirCh[kch].y = cleanParam.levelIirCoef * cleanState.levelIirCh[kch].y + (1 - cleanParam.levelIirCoef) * xd;
        }
        cleanState.thresCh[kch] = 0.5f * (cleanState.levelIirCh[kch].x + cleanState.levelIirCh[kch].y);

        arg[kch] = xd - cleanState.thresCh[kch];
      }

      xn.x = 2.0f / (1.0f + (float)Math.Exp(-cleanParam.levelClipFactor * arg[0])) - 1.0f;
      xn.y = 2.0f / (1.0f + (float)Math.Exp(-cleanParam.levelClipFactor * arg[1])) - 1.0f;

      short2_t yn;

      yn.x = (short)(xn.x * 32000);
      yn.y = (short)(xn.y * 32000);

      PreprocessReady(yn);

      //DataLog.SaveClean(yn);
    }
  }
}
