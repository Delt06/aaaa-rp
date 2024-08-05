#ifndef AAAA_MPMC_QUEUE_INCLUDED
#define AAAA_MPMC_QUEUE_INCLUDED

// https://github.com/rigtorp/MPMCQueue

#include "Packages/com.deltation.aaaa-rp/ShaderLibrary/Core.hlsl"

#define MPMC_QUEUE_VALUE_T uint3

struct MPMCQueue_Slot
{
    uint               Turn;
    MPMC_QUEUE_VALUE_T Value;
};

struct MPMCQueue
{
    globallycoherent RWByteAddressBuffer m_Slots;
    globallycoherent RWByteAddressBuffer m_HeadTail;
    uint                                 m_Capacity;

    static MPMCQueue Construct(const globallycoherent RWByteAddressBuffer slots, const globallycoherent RWByteAddressBuffer headTail,
                               const globallycoherent uint capacity)
    {
        MPMCQueue queue;
        queue.m_Slots = slots;
        queue.m_HeadTail = headTail;
        queue.m_Capacity = capacity;
        return queue;
    }

    static MPMCQueue_Slot CreateSlot(const uint turn, const MPMC_QUEUE_VALUE_T value)
    {
        MPMCQueue_Slot slot;
        slot.Turn = turn;
        slot.Value = value;
        return slot;
    }

    MPMCQueue_Slot GetSlot(const uint slotIndex)
    {
        const uint  address = slotIndex * 4 * 4;
        const uint4 loadedValue = m_Slots.Load4(address);
        return CreateSlot(loadedValue.x, loadedValue.yzw);
    }

    void StoreSlot(const uint slotIndex, const MPMCQueue_Slot slot)
    {
        const uint address = slotIndex * 4 * 4;
        m_Slots.Store4(address, uint4(slot.Turn, slot.Value));
    }

    uint _Idx(const uint i)
    {
        return i % m_Capacity;
    }

    uint _Turn(const uint i)
    {
        return i / m_Capacity;
    }

    void Enqueue(const MPMC_QUEUE_VALUE_T value)
    {
        uint head;
        m_HeadTail.InterlockedAdd(0, 1, head);
        const uint slotIndex = _Idx(head);

        UNITY_LOOP
        while (_Turn(head) * 2 != GetSlot(slotIndex).Turn)
        {

        }

        StoreSlot(slotIndex, CreateSlot(_Turn(head) * 2 + 1, value));
    }

    bool TryEnqueue(const MPMC_QUEUE_VALUE_T value)
    {
        uint head = m_HeadTail.Load(0);
        bool result = false;
        bool gotResult = false;

        while (!gotResult)
        {
            const uint slotIndex = _Idx(head);

            if (_Turn(head) * 2 == GetSlot(slotIndex).Turn)
            {
                uint oldHeadValue;
                m_HeadTail.InterlockedCompareExchange(0, head, head + 1, oldHeadValue);
                if (oldHeadValue)
                {
                    StoreSlot(slotIndex, CreateSlot(_Turn(head) * 2 + 1, value));
                    gotResult = true;
                    result = true;
                }
            }
            else
            {
                const uint prevHead = head;
                head = m_HeadTail.Load(0);
                if (head == prevHead)
                {
                    gotResult = true;
                    result = false;
                }
            }
        }

        return result;
    }

    bool TryDequeue(out MPMC_QUEUE_VALUE_T value)
    {
        uint tail = m_HeadTail.Load(4);
        bool result = false;
        bool gotResult = false;

        UNITY_LOOP
        while (!gotResult)
        {
            const uint slotIndex = _Idx(tail);
            if (_Turn(tail) * 2 + 1 == GetSlot(slotIndex).Turn)
            {
                uint oldValue;
                m_HeadTail.InterlockedCompareExchange(4, tail, tail + 1, oldValue);
                if (oldValue)
                {
                    value = GetSlot(slotIndex).Value;
                    StoreSlot(slotIndex, CreateSlot(_Turn(tail) * 2 + 2, 0));
                    gotResult = true;
                    result = true;
                }
            }
            else
            {
                const uint prevTail = tail;
                tail = m_HeadTail.Load(4);
                if (tail == prevTail)
                {
                    gotResult = true;
                    result = false;
                }
            }
        }

        return result;
    }

    int GetSize()
    {
        int2 headTail = m_HeadTail.Load2(0);
        return headTail.x - headTail.y;
    }

    bool IsEmpty()
    {
        return GetSize() <= 0;
    }
};


#endif // AAAA_MPMC_QUEUE_INCLUDED