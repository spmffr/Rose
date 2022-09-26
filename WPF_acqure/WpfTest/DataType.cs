using DocumentFormat.OpenXml.Wordprocessing;
using FrondScope;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;
using System.Threading.Tasks;

namespace FrondScope
{
  [Serializable]
  public struct short2_t
  {
    public short x;
    public short y;
    public short2_t(short v1, short v2) : this()
    {
      this.x = v1;
      this.y = v2;
    }
    public short2_t(short2_t src) : this()
    {
      this.x = src.x;
      this.y = src.y;
    }

  }

  [Serializable]
  public struct ushort2_t
  {
    public ushort x;  // LSB - index
    public ushort y;  // MSB - value

    public ushort2_t()
    {
      this.x = 
      this.y = 0;
    }

    public ushort2_t(ushort v1, ushort v2) : this()
    {
      this.x = v1;
      this.y = v2;
    }
    public ushort2_t(ushort2_t src) : this()
    {
      this.x = src.x;
      this.y = src.y;
    }
  }
  [Serializable]
  [StructLayout(LayoutKind.Explicit)]
  public struct sort_t
  {
    [FieldOffset(0)] public ushort index;  // LSB - index
    [FieldOffset(2)] public ushort value;  // MSB - value
    [FieldOffset(0)] public uint u;        // as a uint

    public sort_t(ushort index, ushort x) : this()
    {
      this.index = index;
      this.value = x;
    }
    public sort_t(uint ui)
    {
      this.u = ui;
    }
    public sort_t(sort_t src) : this()
    {
      this.u = src.u;
    }
  }

  [Serializable]
  public struct float2_t
  {
    public float x;
    public float y;
    public float2_t(float a, float b) { x = a; y = b; }
    public float2_t(float2_t src)
    {
      x = src.x;
      y = src.y;
    }
  }

  public struct cleanParam_t
  {
    public float Qadc;         // input
    public float adcFs_KHz;    // input
    public float tmedian_ms;   // input, 0 disables
    public float tfilter_ms;   // input, 0 disables
    public ushort2_t[] initLevelCh;  // 2
    public float levelIirCoef;
    public float levelClipFactor;

    public cleanParam_t()
    {
      Qadc = Settings.Default.Qadc;
      adcFs_KHz = Settings.Default.adcKHz;        // input
      tmedian_ms = Settings.Default.tmedian_ms;   // input, 0 disables
      tfilter_ms = Settings.Default.tfilter_ms;   // input, 0 disables

      initLevelCh = new ushort2_t[2]; // { new ushort2_t( 45000, 62000), new ushort2_t( 2000, 64000) };
      initLevelCh[0].x = Settings.Default.initLevelCh0_x;
      initLevelCh[0].y = Settings.Default.initLevelCh0_y;
      initLevelCh[1].x = Settings.Default.initLevelCh1_x;
      initLevelCh[1].y = Settings.Default.initLevelCh1_y;

      levelIirCoef = Settings.Default.levelIirCoef;
      levelClipFactor = Settings.Default.levelClipFactor;

    }
  }

  public struct cleanState_t
  {
    // LSB - index, MSB - value
    public sort_t[] sort0;   // medianROS x 1
    public sort_t[] sort1;   // medianROS x 1

    public int medianROS;
    public int medianCenterIndex;
    public int medianInputCnt;
    public int medianOutputCnt;

    public float[] filterCoef;     // filterROS x 1
    public float2_t[] filterData;  // filterROS x 1
    public int filterHead;         // points to next available entry (for latest data)
    public int filterTail;         // points to earliest data
    public int filterROS;
    public int filterCenterIndex;
    public int filterInputCnt;
    public int filterOutputCnt;

    public float2_t[] levelCh;  // 2
    public float2_t[] levelIirCh; // 2
    public float[] thresCh;  // 2

    public cleanState_t(cleanParam_t param)
    {
      int nMedian = (int)(param.tmedian_ms * param.adcFs_KHz) / 2;
      //nMedian = (nMedian >> 1);

      this.medianCenterIndex = nMedian; // index start from 0
      this.medianROS = nMedian * 2 + 1; // (nMedian << 1) + 1;  // odd

      this.medianInputCnt = 0;
      this.medianOutputCnt = 0;

      int nFilter = (int)(param.tfilter_ms * param.adcFs_KHz) / 2;
      //nFilter = (nFilter >> 1);

      this.filterCenterIndex = nFilter;
      this.filterROS = nFilter * 2 + 1; // (nFilter << 1) + 1;  // odd

      this.filterInputCnt = 0;
      this.filterOutputCnt = 0;

      this.filterHead = 0;      // points to next available entry (for latest data)
      this.filterTail = 0;      // points to earliest data

      levelCh = new float2_t[2];
      levelIirCh = new float2_t[2];
      thresCh = new float[2];

      for (int k = 0; k < 2; k++)
      {
        this.levelCh[k].x = (float)(param.initLevelCh[k].x) * param.Qadc;
        this.levelCh[k].y = (float)(param.initLevelCh[k].y) * param.Qadc;

        this.levelIirCh[k].x = this.levelCh[k].x;
        this.levelIirCh[k].y = this.levelCh[k].y;

        this.thresCh[k] = (this.levelCh[k].x + this.levelCh[k].y) * 0.5f;
      }

      //int sortSize_byte = this.medianROS * 4; // sizeof(uint)
      //this.sort0 = new uint[this.medianROS];  // packed (value,index)
      //this.sort1 = new uint[this.medianROS];  // packed (value,index)
      this.sort0 = new sort_t[this.medianROS];  // union (value,index), uint
      this.sort1 = new sort_t[this.medianROS];  // union (value,index), uint

      this.filterCoef = new float[this.filterROS]; // * sizeof(float));          // filter coefficients
      this.filterData = new float2_t[this.filterROS];   // filter ROS

      initCoef();
    }
    void initCoef()
    {
      float delta = (float)(2.0f * Math.PI / this.filterROS), coef;
      float xarg =  -delta * this.filterCenterIndex;  // '-' cos is symmetric
      float xsum = 0;
      for (int k = 0; k < this.filterROS; k++)
      {
        coef = this.filterCoef[k] = (float)(0.5 + 0.5 * Math.Cos(xarg));
        xarg += delta;
        xsum += coef;
      }
      for (int k = 0; k < this.filterROS; k++)
      {
        this.filterCoef[k] /= xsum;

      }
    }

    public void MedianValue(out ushort2_t idx)
    {
      idx.x = sort0[medianCenterIndex].value;
      idx.y = sort1[medianCenterIndex].value;
    }
    public void insertionSort0()
    {
      int j;
      uint key;
      for (int i = 1; i < medianROS; i++)
      {

        key = sort0[i].u;   // take value
        j = i;
        while (j > 0 && sort0[j - 1].u > key)
        {
          sort0[j] =new sort_t( sort0[j - 1]);
          j--;
        }
        sort0[j].u = key;   // insert in right place
      }
    }
    public void insertionSort1()
    {
      int j;
      uint key;
      for (int i = 1; i < medianROS; i++)
      {
        key = sort1[i].u;   // take value
        j = i;
        while (j > 0 && sort1[j - 1].u > key)
        {
          sort1[j] = new sort_t(sort1[j - 1]);
          j--;
        }
        sort1[j].u = key;   // insert in right place
      }
    }
    public void insertSample0(ushort xn)
    {
      ushort xvalue, xindex ;

      for (int k = 0; k < medianROS; k++)
      {
        xvalue = sort0[k].value; // & 0xffff0000;
        xindex = sort0[k].index; // & 0x0000ffff;
        if (xindex == 0)
        {
          xindex = (ushort)(medianROS - 1);
          xvalue = xn; // (uint)xn << 16;
        }
        else
        {
          xindex--; // = xindex - 1;
        }
        sort0[k].index = xindex; // = xvalue | xindex;
        sort0[k].value = xvalue;
      }
    }
    public void insertSample1(ushort yn)
    {
      ushort yvalue, yindex;

      for (int k = 0; k < medianROS; k++)
      {
        yvalue = sort1[k].value; // & 0xffff0000;
        yindex = sort1[k].index; // & 0x0000ffff;
        if (yindex == 0)
        {
          yindex = (ushort)(medianROS - 1);
          yvalue = yn; // (uint)yn << 16;
        }
        else
        {
          yindex--; // = yindex - 1;
        }
        sort1[k].value = yvalue; // = yvalue | yindex;
        sort1[k].index = yindex;

      }
    }
  } // class

}
