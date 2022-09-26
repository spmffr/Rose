#include <stdio.h>
#include <stdlib.h>
#include "main.h"
#include "usb_device.h"
#include "usbd_cdc_if.h"
#include <cmsis_os2.h>
#include "CQueueT.h"
#include "protocol.h"
#include "stm32h7xx_nucleo.h"
#include "data.cpp"
extern osThreadId_t tidMain, tidData;
extern TIM_HandleTypeDef htim2;
extern ADC_HandleTypeDef hadc3;

volatile uint32_t adc_buffer __attribute__( (aligned(32), section("DMABUFFER")) ) = {0};

uint32_t ndx = 0;
uint32_t idx = 0;  // idx - index of DMA double buffer, nPack - sequence of acquied packets
uint32_t nPackIndex = 0, nDataIndex = 0;  // nDataIndex - index of sample being feed into a packet, nPacketIndex - index of current packet in ring buffer
bool forward = true;
bool bStart = false;
osMessageQueueId_t msgQ;

AxonPacket packets[NUM_OF_PACKETS]; // __attribute__((section("DMABUFFER")));

//CircularQueueT<AxonPacketPtr, NUM_OF_PACKETS> msgQ;
CircularQueueT<uint8_t, 64> rxQueue;

static bool bSimu = false;
extern "C" void usbRxCallback(uint8_t* p, uint32_t n)
{
  rxQueue.Enqueue(p, n);
  if ((p[n - 1] == 0x0d) || (p[n-1] == 0x0a))
    osThreadFlagsSet(tidMain, THREAD_FLAG_RXMSG);
}

void InitPackets(void)
{
  msgQ = osMessageQueueNew(NUM_OF_PACKETS, sizeof(AxonPacketPtr), NULL);
  
  for (int n = 0; n < NUM_OF_PACKETS; n++)
  {
    packets[n].Stx = PACK_STX;
    packets[n].index = 0;
    packets[n].samples = SAMPLES_PER_PACKET;
    packets[n].sampleHz = 25000;
  }
}
bool setAcquisitionState(bool state)
{
  bool prevState = bStart;
  if (state != bStart)
  {
    if (1 == state)
    {
      BSP_LED_On(LED_GREEN);
      nPackIndex = nDataIndex = ndx = 0;
      osMessageQueueReset(msgQ);
      
      if (bSimu)
      {
        forward = true;
        HAL_TIM_Base_Start_IT(&htim2);
      }
      else
      {
        ADC3->IER |= ADC_IER_EOCIE;
        HAL_ADC_Start_DMA(&hadc3,(uint32_t*)&adc_buffer, 2);
        HAL_TIM_Base_Start(&htim2);
      }
    }
    else
    {
      BSP_LED_Off(LED_GREEN);
      if (bSimu)
        HAL_TIM_Base_Stop_IT(&htim2);
      else
      {
        HAL_TIM_Base_Stop(&htim2);
        HAL_ADC_Stop_DMA(&hadc3);
        ADC3->IER &= ~ADC_IER_EOCIE;
      }
      TIM2->CNT = 0;
      osDelay(500);  // wait until all packet are sent
    }
    bStart = state;
  }
  return prevState;
}

/// ---- start/stop data acquisition & streaming ----////
static void stateCmd(char* p)
{
  char *end;
  bool state;

  int32_t n = strtoul(p, &end, 10);
  state = (0 != n);

  if (state != bStart)
  {
    setAcquisitionState(state);
  }
}

static void simulateCmd(char* p)
{
  char *end;

  int32_t n = strtoul(p, &end, 10);

  if (0 != n)
  {
    bSimu = true;
    BSP_LED_On(LED3);
  }
  else
  {
    bSimu = false;
    BSP_LED_Off(LED3);
  }

}


// parse commands received from USB
void parseCmd(uint8_t* p)
{
  const char* tok = " \r\n";
  char* ps = strtok((char*)p, tok);

  if (ps != NULL)
  {
    if (strncmp(ps, "STAT", 4) == 0)
    {
      ps = strtok(NULL, tok);
      stateCmd(ps);
    }
    else if (strncmp(ps, "SIMU", 4) == 0)
    {
      ps = strtok(NULL, tok);
      simulateCmd(ps);
    }
  }
}
/* USER CODE END PFP */

void dataThread(void* arg)
{
  AxonPacketPtr pPacket = NULL;

  while (1)
  {
    osMessageQueueGet(msgQ, &pPacket, NULL, osWaitForever);
    if ( pPacket != NULL)
    {
      CDC_Transmit_FS((uint8_t*)pPacket, sizeof(AxonPacket));
      osDelay(1);
    }
  }
}

//----------------- ISR callbacks    ----------------------------------------//
extern "C" void HAL_ADC_ConvCpltCallback(ADC_HandleTypeDef* hadc)
{
  SCB_InvalidateDCache_by_Addr((void*)&adc_buffer, 64);

  AxonPacket* pPacket = &packets[nPackIndex % NUM_OF_PACKETS];

  pPacket->data[nDataIndex] = *(uint32_t*)&adc_buffer;

  if (++nDataIndex == SAMPLES_PER_PACKET)
  {
    pPacket->index = nPackIndex++;
    nDataIndex = 0;
    osMessageQueuePut(msgQ, &pPacket, NULL, 0);
  }
}

/**
  * @brief This function handles TIM2 global interrupt.
  */
extern "C" void TIM2_IRQHandler(void)
{
  /* USER CODE BEGIN TIM2_IRQn 0 */
  uint32_t status = TIM2->SR & (TIM_SR_UIF | TIM_SR_CC1IF);
  AxonPacket* pPacket;
//  ndx = arrayIdx % 70000;
  
  if (status & TIM_SR_UIF)
  {
    TIM2->SR &= ~TIM_SR_UIF;
    GPIOG->BSRR = GPIO_PIN_8;
    pPacket = &packets[nPackIndex % NUM_OF_PACKETS];
    pPacket->data[nDataIndex] = *(uint32_t*)&adc[ndx];
    if (++nDataIndex == SAMPLES_PER_PACKET)
    {
      pPacket->index = nPackIndex++;
      nDataIndex = 0;
      osMessageQueuePut(msgQ, &pPacket, NULL, 0);
    }
    if (forward)
    {
      ndx++;
      if (ndx == 70000)
        forward = false;
    }
    else
    {
      ndx--;
      if (ndx == 0)
        forward = true;
    }
  }
  if (status & TIM_SR_CC1IF)
  {
    TIM2->SR &= ~TIM_SR_CC1IF;
    GPIOG->BSRR = (GPIO_PIN_8 << 16);
  }
  /* USER CODE END TIM2_IRQn 0 */
  //HAL_TIM_IRQHandler(&htim2);
  /* USER CODE BEGIN TIM2_IRQn 1 */

  /* USER CODE END TIM2_IRQn 1 */
}

//extern "C" void BSP_PB_Callback(Button_TypeDef Button)
//{
//    if (Button == BUTTON_USER)
//    {
//        osThreadFlagsSet(tidMain, THREAD_FLAG_BUTTON);
//    }
//}
