/***********************************************************\
* Generic fixed depth circular queue  
\***********************************************************/
#pragma once

#include <stdbool.h>
#include <cmsis_os2.h>
#include <stm32h7xx_HAL.h>
#include <string.h>

template <typename T, uint32_t N>
class CircularQueueT
{
public:
  CircularQueueT()
  {
	  m_sem = NULL;
    m_depth = N;
    m_w = m_r = 0;
    m_over = 0;
    memset((void*)&m_data[0], 0, N * sizeof(T));
  }

  virtual ~CircularQueueT() 
  {
    if (m_sem != NULL )
      osSemaphoreDelete(m_sem);
    m_sem = NULL;   
  }
  
  void SetSemphoreId(osSemaphoreId_t sem) { m_sem = sem;}
  bool Enqueue(T& ele)
  {
    if (m_w < (m_r + m_depth))
    {
      m_data[m_w % m_depth] = ele;
      m_w++;
      return true;
    }
    m_over++;
    return false;
  }
  bool EnqueueNotify(T& ele)
  {
    if (m_w < (m_r + m_depth))
    {
      m_data[m_w % m_depth] = ele;
      m_w++;
      osSemaphoreRelease(m_sem);
      return true;
    }
    m_over++;
    return false;
  }
  bool Enqueue(T* pArray, uint32_t n)
  {
    T ele;
      
    if ( n >   SpaceAvailable()) return false;
    for (uint32_t i = 0; i < n; i++)
    {
        ele = pArray[i];
        Enqueue(ele);
    }
    return true;
  }

 
   // return a pointer
  bool Dequeue(T& ele)
  {
    bool bRet = false;
    if (m_w > m_r)
    {
      ele = m_data[m_r % m_depth];;
      m_r++;
      bRet = true;  
    }
    return bRet;
  }

  // return a pointer
  T DequeuePending(uint32_t timeout = osWaitForever)
  {
    T ele;
    
    osSemaphoreAcquire(m_sem, timeout);
    if (m_w > m_r)
    {
      ele = m_data[m_r % m_depth];;
      m_r++;
    }
    return ele;
  }
  
  uint32_t NumberOfDataInQueue()
  {
    return (uint32_t)(m_w - m_r);
  }

	uint32_t SpaceAvailable()
	{
		return m_depth + m_r - m_w;
	}

  void Reset()
  {
    m_over = 0;
    m_w = m_r = 0;
	memset((void*)&m_data[0], 0, N * sizeof(T));
  }

  __inline bool IsFull()
  {
    return (m_w == (m_r + m_depth));
  }
  __inline bool IsEmpty()
  {
    return (m_r == m_w);
  }
  __inline uint32_t GetNumErrors()
  {
    return m_over;
  }

protected:
  osSemaphoreId_t  m_sem;
  uint32_t m_r, m_w, m_depth;  
  uint32_t m_over;  
  T m_data[N];
};

