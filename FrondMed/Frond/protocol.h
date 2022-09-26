#pragma once

#include <stdbool.h>
#include <string.h>
#include <cmsis_os2.h>

#define NUM_OF_PACKETS      8
#define SAMPLES_PER_PACKET  510

// Thread flags for main thread - Button, USB RX message (every line)
#define THREAD_FLAG_RXMSG     1
#define THREAD_FLAG_BUTTON    2
#define THREAD_FLAG_QUIT      8

#define PACK_STX      0xa55a

typedef struct __attribute__((packed))
{ 
  uint16_t Stx;
  uint16_t index;     // at max 10Khz, this will be 1600 seconds data
  uint16_t samples;   // packet/sample
  uint16_t sampleHz;  // sampling rate in Hz
  // open-ended array    
  uint32_t data[SAMPLES_PER_PACKET];  // packed sample pair    
} AxonPacket, *AxonPacketPtr;


