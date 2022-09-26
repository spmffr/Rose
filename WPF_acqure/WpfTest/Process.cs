using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Windows;
using FrondScope;
using static System.Windows.Forms.AxHost;

namespace FrondScope
{
  public struct param_t
  {
    public float adcFs_KHz;
    public int adcThreshold_0_1;
    public int adcThreshold_1_0;
    public int hiResEnable;
    public int nBarcode1;
    public int nBarcode2;
  }
  public struct state_t
  {
    public int inputCnt;
    public int prevCh0Level;
    public int prevCh1Level;
    public int tCnt;
    public int prevState;
    public int prevDir;
    public int prevPos;
    public float prevPos2;
  }

  public struct cmd_t
  {
    public int timeCnt;
    public int state;
    public int dir;
    public int cntInc;
    public int tCnt;
    public float cntInc2;
    public int posIndex;
    public float posIndex2;
  }

  public delegate void NewLocation(float2_t pos);
  public delegate void SimulationDone(double ms);

  public partial class Process
  {
    public event NewLocation LocationChanged;
    public event SimulationDone SimulationCompleted;

    private static readonly int[,] stateTable = new int[4, 4] { { 1, 2, 3, 4 }, { 5, 6, 7, 8 }, { 9, 10, 11, 12 }, { 13, 14, 15, 16 } };
    private float _prevX = -1.0f;
    private float _min_move = 0.1f;
    private AutoResetEvent _signal;
    private Thread _thread;
    private string _folder;

    private ConcurrentQueue<short2_t> adcQueue;
    private LastQ<cmd_t> cmdQueue;
    private Queue<float2_t> outQueue;
    private float2_t[] p_barcode1, p_barcode2;

    NewLocation _locDelegate;

    public bool LocalSim
    {
      private get { return false; } 
      set
      {
        _locDelegate = (value)? new NewLocation(queueDelegate) : new NewLocation(locationDelegate);
      }
    }
    public int ProcessCnt
    {
      get { return outQueue.Count; }
      private set { }
    }
    public Process()
    {
      _folder = System.AppDomain.CurrentDomain.BaseDirectory;

      adcQueue = new ConcurrentQueue<short2_t>();
      cmdQueue = new LastQ<cmd_t>();
      outQueue = new Queue<float2_t>();
      //p_barcode1 = new float2_t[404];  //allocated in 'initParam' while reading bar codes
      //p_barcode2 = new float2_t[806];
      initParams();
      _signal = new AutoResetEvent(false);
      _thread = new Thread(new ParameterizedThreadStart(Process.worker));
      _thread.Start(this);
    }
    public void Reset()
    {
      state.inputCnt = 0;
      state.prevCh0Level = 0;
      state.prevCh1Level = 0;
      state.tCnt = 0;
      state.prevState = -1; // not defined   
      state.prevDir = 0;    // not defined   
      state.prevPos = 0;
      state.prevPos2 = 0;
      adcQueue.Clear();
      cmdQueue.Clear();
      outQueue.Clear();
    }
    //public void OnPacket(FrondPacket pa)
    //{
    //  int n = pa.SamplesPerPacket;
    //  UInt16[] sampleArray = pa.Samples;
    //  ushort2_t s;

    //  for (int i = 0; i < n; i++)
    //  {
    //    s.x = sampleArray[2 * i];
    //    s.y = sampleArray[2 * i + 1];
    //    adcQueue.Enqueue(s);
    //  }
    //  //_signal.Set();
    //}

    public void OnSample(short2_t sample)
    {
       adcQueue.Enqueue(sample);
      _signal.Set();
    }
    public void SetThreshold(int t1, int t2)
    {
      param.adcThreshold_0_1 = t1;
      param.adcThreshold_1_0 = t2;

    }

    public void End()
    {
      _thread.Interrupt();
      _thread.Join(1000);
      Reset();

    }
    public static void worker(object o)
    {
      Process proc = (Process)o;

      proc.processLoop();
    }
    private void processLoop()
    {
      bool b;
      while (true)
      {
        try
        {
          _signal.WaitOne(20);
          while (adcQueue.Count > 0 ) //(!IsEmpty)
          {
            short2_t xn;

            if (adcQueue.TryDequeue(out xn))
            {
              //System.Diagnostics.Debug.WriteLine("{0}", xn.x);
              process1(xn);
              process2();
            }
          }
        }
        catch (ThreadInterruptedException) 
        {
          return;
        }
      }
    }

    public void process1(short2_t xn)
    {
      //ushort2_t xn;

      //if (!adcQueue.TryDequeue(out xn))
      //  return;

      // hysteresis
      int xthres = (state.prevCh0Level == 0) ? param.adcThreshold_0_1 : param.adcThreshold_1_0;
      int xch0 = (xn.x > xthres) ? 1 : 0;
      xthres = (state.prevCh1Level == 0) ? param.adcThreshold_0_1 : param.adcThreshold_1_0;
      int xch1 = (xn.y > xthres) ? 1 : 0;
      int newState = 2 * xch0 + xch1; // 0-3

      state.prevState = (state.prevState < 0) ? newState : state.prevState;
      int stateIndex = stateTable[state.prevState, newState];

      int newDir = state.prevDir;
      int tCnt = state.tCnt;
      int cntInc = 0;
      float cntInc2 = 0;

      switch (stateIndex)
      {
        case 1:
        case 6:
        case 11:
        case 16:    // (newState == prevState)
          newDir = state.prevDir;
          cntInc = 0;
          cntInc2 = 0;
          break;
        case 3:     // (prevState == 0) & (newState == 2)
          newDir = 1;
          cntInc = 0;
          cntInc2 = 0.5f;
          break;
        case 9:     // (prevState == 2) & (newState == 0)
          newDir = -1;
          cntInc = (state.prevDir == -1) ? -1 : 0;
          cntInc2 = -0.5f;
          break;
        case 12:    // (prevState == 2) & (newState == 3)
          newDir = 1;
          cntInc = (state.prevDir == 1) ? 1 : 0;
          cntInc2 = 0.5f;
          break;
        case 15:    // (prevState == 3) & (newState == 2)
          newDir = -1;
          cntInc = 0;
          cntInc2 = -0.5f;
          break;
        case 2:     // (prevState == 0) & (newState == 1)
          newDir = -1;
          cntInc = 0;
          cntInc2 = -0.5f;
          break;
        case 5:     // (prevState == 1) & (newState == 0)
          newDir = 1;
          cntInc = (state.prevDir == 1) ? 1 : 0;
          cntInc2 = 0.5f;
          break;
        case 8:     // (prevState == 1) & (newState == 3)
          newDir = -1;
          cntInc = (state.prevDir == -1) ? -1 : 0;
          cntInc2 = -0.5f;
          break;
        case 14:    // (prevState == 3) & (newState == 1)
          newDir = 1;
          cntInc = 0;
          cntInc2 = 0.5f;
          break;
        default:
          //System.Diagnostics.Debug.WriteLine("{0} illegal state", state.inputCnt);
          newDir = 0;
          cntInc = 0;
          cntInc2 = 0;
          break;
      }

      tCnt = (state.prevState != newState) ? 1 : tCnt + 1;

      cmd_t cmd;
      cmd.timeCnt = state.inputCnt;
      cmd.state = newState;
      cmd.dir = newDir;
      cmd.cntInc = cntInc;
      cmd.tCnt = tCnt;
      cmd.cntInc2 = cntInc2;

      if (cmdQueue.Count > 0) //not empty
      {
        cmd_t last = cmdQueue.Last;
        cmd.posIndex = last.posIndex + cntInc;
        cmd.posIndex2 = last.posIndex2 + cntInc2;
      }
      else
      {
        cmd.posIndex = cntInc;
        cmd.posIndex2 = cntInc2;
      }

      cmdQueue.Enqueue(cmd);

      state.prevState = newState;
      state.prevDir = newDir;
      state.inputCnt += 1;

    }


    public void process2()
    {
      cmd_t cmd = cmdQueue.Peek();
      if (cmdQueue.Count > 1)
      { // leave 1 entry to accumulate over
        cmdQueue.Dequeue();
      }

      float dt_ms = 1.0f / param.adcFs_KHz;

      float2_t xout;

      if (param.hiResEnable == 1)
      {
        int index = (int)(2 * cmd.posIndex2);
        index = (index < 0) ? 0 : index;
        index = (index >= param.nBarcode2) ? param.nBarcode2 - 1 : index;
        xout.x = p_barcode2[index].x;       // mm
      }
      else
      {
        int index = cmd.posIndex;
        index = (index < 0) ? 0 : index;
        index = (index >= param.nBarcode1) ? param.nBarcode1 - 1 : index;
        xout.x = p_barcode1[index].x;       // mm
      }

      xout.y = cmd.timeCnt * dt_ms;       // ms

      _locDelegate(xout);

      //outQueue.Enqueue(xout);
      //if (Math.Abs(xout.x - _prevX) > 0.1f)
      //{
      //  _prevX = xout.x;
      //  LocationChanged?.Invoke(xout);
      //}

    }
    private  void locationDelegate(float2_t x)
    {
      if (Math.Abs(x.x - _prevX) > 0.1f)
      {
        _prevX = x.x;
        LocationChanged?.Invoke(x);
      }
    }
    private void queueDelegate(float2_t x)
    {
      outQueue.Enqueue(x);
    }

    public float2_t[] GetResult()
    {
      return outQueue.ToArray();
    }
  }
}
