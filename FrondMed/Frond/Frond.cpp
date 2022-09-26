/* USER CODE BEGIN Header */
/* USER CODE END Header */
/* Includes ------------------------------------------------------------------*/
#include <stdio.h>
#include <stdlib.h>
#include "main.h"
#include "usb_device.h"
#include "usbd_cdc_if.h"
#include <cmsis_os2.h>
#include "CQueueT.h"
#include "Protocol.h"
#include "stm32h7xx_nucleo.h"

/* Private includes ----------------------------------------------------------*/
/* USER CODE BEGIN Includes */

uint32_t nSample = 0;

/* USER CODE END Includes */

/* Private typedef -----------------------------------------------------------*/
/* USER CODE BEGIN PTD */
/* USER CODE END PTD */

/* Private define ------------------------------------------------------------*/
/* USER CODE BEGIN PD */
/* USER CODE END PD */

/* Private macro -------------------------------------------------------------*/
/* USER CODE BEGIN PM */
/* USER CODE END PM */

/* Private variables ---------------------------------------------------------*/
extern ADC_HandleTypeDef hadc3;
extern DMA_HandleTypeDef hdma_adc3;
extern TIM_HandleTypeDef htim2;

/* USER CODE BEGIN PV */

/* USER CODE END PV */

/* Private function prototypes -----------------------------------------------*/
/* USER CODE BEGIN PFP */

osThreadId_t tidMain, tidData;

extern void parseCmd(uint8_t* p);
extern void dataThread(void* arg);
//extern CircularQueueT<AxonPacketPtr, 16> msgQ;
extern CircularQueueT<uint8_t, 64> rxQueue;
//extern cPacketBuffer<AxonPacket, NUM_OF_PACKETS> packets; // __attribute__((section("DMABUFFER")));
extern bool bStart;
extern bool setAcquisitionState(bool state);
extern void InitPackets(void);


extern "C" void mainThread(void* arg)
{
    uint32_t flags;
    uint8_t buf[32], c, n = 0;

    MX_USB_DEVICE_Init();
    tidMain = osThreadGetId();
  
//    msgQ.SetSemphoreId(osSemaphoreNew(1, 0, NULL));
    InitPackets();

    tidData = osThreadNew(dataThread, NULL, NULL);    // Create application main thread

//    HAL_ADC_Start_DMA(&hadc3,(uint32_t*)&adc_buffer[0], 2);

    
    while (1)
    {
        //osDelay(100);
        flags = osThreadFlagsWait( /*THREAD_FLAG_BUTTON | */THREAD_FLAG_RXMSG, osFlagsWaitAny, osWaitForever );
        /*if (flags & THREAD_FLAG_BUTTON)
        {
            setAcquisitionState(!bStart );
        }  // button
        else */
        if (flags & THREAD_FLAG_RXMSG) // USB RX
        {
            n = 0;
            while( rxQueue.Dequeue(c))
            {
                buf[n++] = c;
                if (c == 0x0d)
                {
                    buf[n] = '\0';
                    rxQueue.Reset();
                    parseCmd(buf);
                    memset(buf, 0, 32);
                    n = 0;
                }
                if (n > 31)
                {
                    memset(buf, 0, 32);
                    n = 0;
                }
  
            }
        }
    }

}
/* USER CODE END 0 */


