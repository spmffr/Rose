#pragma once
#include <math.h>
#include "stm32h7xx_hal.h"

class IIR1
{
public: 
  IIR1() 
  { 
    Reset(0.0f);
  } 
  IIR1(float a)
  {
      Reset(a);
  }
  float Filter(float sample)
  {
    float f = m_fOldSample;
    m_fOldSample = m_B * sample +  m_A * f;
    return m_fOldSample;
  }
  float  Reset(float A) 
  { 
    m_fOldSample = 0.0f;
    m_A = A; 
    m_B = 1.0f - A;
    return m_fOldSample;
  }
  
private:
  float m_A;
  float m_B;
  float m_fOldSample;
};


