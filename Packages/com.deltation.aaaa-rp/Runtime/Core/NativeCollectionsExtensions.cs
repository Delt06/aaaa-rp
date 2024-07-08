using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace DELTation.AAAARP.Core
{
    public static class NativeCollectionsExtensions
    {
        public static unsafe ref T ElementAtRef<T>(this NativeArray<T> array, int index) where T : struct
        {
            if (index < 0 || index >= array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index out of range");
            }
            
            return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafePtr(), index);
        }
        
        public static unsafe ref readonly T ElementAtRefReadonly<T>(this NativeArray<T> array, int index) where T : struct
        {
            if (index < 0 || index >= array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index out of range");
            }
            
            return ref UnsafeUtility.ArrayElementAsRef<T>(array.GetUnsafeReadOnlyPtr(), index);
        }
        
        public static unsafe T* ElementPtr<T>(this NativeArray<T> array, int index) where T : unmanaged
        {
            if (index < 0 || index >= array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index out of range");
            }
            
            return (T*) array.GetUnsafePtr() + index;
        }
        
        public static unsafe T* ElementPtrReadonly<T>(this NativeArray<T> array, int index) where T : unmanaged
        {
            if (index < 0 || index >= array.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "Index out of range");
            }
            
            return (T*) array.GetUnsafeReadOnlyPtr() + index;
        }
    }
}