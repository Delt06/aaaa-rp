#ifndef AAAA_GROUP_SHARED_MPMC_QUEUE_INCLUDED
#define AAAA_GROUP_SHARED_MPMC_QUEUE_INCLUDED

// https://github.com/rigtorp/MPMCQueue

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

#define MPMC_QUEUE_CAPACITY 2048

static groupshared uint2 g_Slots[MPMC_QUEUE_CAPACITY + 1];
static groupshared uint g_Head;
static groupshared uint g_Tail;

struct GroupSharedMPMCQueue
{
    #define SLOT_TURN(slotIndex) (g_Slots[(slotIndex)].x)
    #define SLOT_ITEM(slotIndex) (g_Slots[(slotIndex)].y)

    static uint _GetSlotTurnInterlocked(const uint slotIndex)
    {
        uint value;
        InterlockedAdd(SLOT_TURN(slotIndex), 0, value);
        return value;
    }
    
    static uint _Idx(const uint i)
    {
        return i % MPMC_QUEUE_CAPACITY;
    }

    static uint _Turn(const uint i)
    {
        return i / MPMC_QUEUE_CAPACITY;
    }

    static void Init(const uint groupThreadID, const uint groupSize)
    {
        GroupMemoryBarrierWithGroupSync();

        if (groupThreadID == 0)
        {
            g_Head = 0;
            g_Tail = 0;
        }

        UNITY_LOOP
        for (uint i = groupThreadID; i < MPMC_QUEUE_CAPACITY + 1; i += groupSize)
        {
            g_Slots[i] = 0u;
        }

        GroupMemoryBarrierWithGroupSync();
    }

    static void Enqueue(const uint item)
    {
        uint head;
        InterlockedAdd(g_Head, 1, head);
        const uint slotIndex = _Idx(head);

        UNITY_LOOP
        while (_Turn(head) * 2 != _GetSlotTurnInterlocked(slotIndex))
        {
            
        }

        SLOT_ITEM(slotIndex) = item;
        SLOT_TURN(slotIndex) = _Turn(head) * 2 + 1; 
    }

    static bool TryEnqueue(const uint item)
    {
        uint head = g_Head;
        bool result = false;
        bool gotResult = false;

        while (!gotResult)
        {
            const uint slotIndex = _Idx(head);
            
            if (_Turn(head) * 2 == SLOT_TURN(slotIndex))
            {
                uint oldHeadValue;
                InterlockedCompareExchange(g_Head, head, head + 1, oldHeadValue);
                if (oldHeadValue)
                {
                    SLOT_ITEM(slotIndex) = item;
                    SLOT_TURN(slotIndex) = _Turn(head) * 2 + 1;
                    gotResult = true;
                    result = true;
                }
            }
            else
            {
                const uint prevHead = head;
                head = g_Head;
                if (head == prevHead)
                {
                    gotResult = true;
                    result = false;
                }
            }
        }

        return result;
    }

    static bool TryDequeue(out uint item)
    {
        uint tail = g_Tail;
        bool result = false;
        bool gotResult = false;
        
        while (!gotResult)
        {
            const uint slotIndex = _Idx(tail);
            if (_Turn(tail) * 2 + 1 == SLOT_TURN(slotIndex))
            {
                uint oldValue;
                InterlockedCompareExchange(g_Tail, tail, tail + 1, oldValue);
                if (oldValue)
                {
                    item = SLOT_ITEM(slotIndex);
                    SLOT_TURN(slotIndex) = _Turn(tail) * 2 + 2;
                    gotResult = true;
                    result = true;
                }
            }
            else
            {
                const uint prevTail = tail;
                tail = g_Tail;
                if (tail == prevTail)
                {
                    gotResult = true;
                    result = false;
                }
            }
        }

        return result;
    }
};


#endif // AAAA_GROUP_SHARED_MPMC_QUEUE_INCLUDED