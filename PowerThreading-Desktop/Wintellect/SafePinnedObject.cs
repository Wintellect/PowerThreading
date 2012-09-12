/******************************************************************************
Module:  SafePinnedObject.cs
Notices: Copyright (c) 2006-2009 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect {
   /// <summary>
   /// This class encapsulates pinning a buffer. 
   /// </summary>
   public sealed class SafePinnedObject: SafeHandleZeroOrMinusOneIsInvalid {
      private GCHandle m_gcHandle;  // Handle of pinned object (or 0)
      private Int32 m_size = 0;     // The size (in bytes) unless overridden in ctor

      private SafePinnedObject(Object obj, Int32 offsetIntoObject, Int32 size) : base(true) {
         if (obj == null) return;

         // Pin the buffer, set the native address, and save the object's size
         m_gcHandle = GCHandle.Alloc(obj, GCHandleType.Pinned);
         unsafe { SetHandle((IntPtr) ((byte*) m_gcHandle.AddrOfPinnedObject() + offsetIntoObject)); }
         m_size = size;
      }

      /// <summary>
      /// This factory method wraps a SafePinnedObject around the specified object.
      /// </summary>
      /// <param name="value">The object that you want to pin.</param>
      /// <returns>The SafePinnedObject wrapping the desired object.</returns>
      public static SafePinnedObject FromObject(Object value) {
         // If obj is null, we create this object but it pins nothing (size will be 0)
         if (value == null) return new SafePinnedObject(null, 0, 0);

         // If obj is an Array, we pinned this array, and return
         if (value.GetType().IsArray) return FromArray((Array) value, 0, -1);

         // Validate the structure of the object before pinning it
         if (value.GetType().IsAutoLayout)
            throw new ArgumentException("object must not be auto layout");

         return new SafePinnedObject(value, 0, Marshal.SizeOf(value));
      }

      /// <summary>
      /// This factory method wraps a SafePinnedObject around the specified array.
      /// </summary>
      /// <param name="array">The array that you want to pin.</param>
      /// <param name="startOffset">The first element in the array whose address you want to pass to native code.</param>
      /// <param name="numberOfElements">The number of elements in the array you wish to pass to native code.</param>
      /// <returns>The SafePinnedObject wrapping the desired array elements.</returns>
      public static SafePinnedObject FromArray(Array array, Int32 startOffset, Int32 numberOfElements) {
         // If obj is null, we create this object but it pins nothing (size will be 0)
         if (array == null) return new SafePinnedObject(null, 0, 0);

         // Validate the structure of the object before pinning it
         if (array.Rank != 1)
            throw new ArgumentException("array Rank must be 1");

         if (startOffset < 0)
            throw new ArgumentOutOfRangeException("startOffset", "Must be >= 0");

         // Validate the structure of the array's element type
         Type elementType = array.GetType().GetElementType();
         if (!elementType.IsValueType && !elementType.IsEnum)
            throw new ArgumentException("array's elements must be value types or enum types");

         if (elementType.IsAutoLayout)
            throw new ArgumentException("array's elements must not be auto layout");

         // If numElements not specied (-1), assume the remainder of the array length
         if (numberOfElements == -1) numberOfElements = array.Length - startOffset;

         if (numberOfElements > array.Length)
            throw new ArgumentOutOfRangeException("numberOfElements", "Array has fewer elements than specified");

         // Convert startOffset from element offset to byte offset
         startOffset *= Marshal.SizeOf(elementType);

         return new SafePinnedObject(array, startOffset, 
            numberOfElements * Marshal.SizeOf(elementType));  // Convert numElements to number of bytes
      }

      /// <summary>
      /// This factory method wraps a SafePinnedObject around an instance of the specified type.
      /// </summary>
      /// <param name="type">The type, an instance of which you want to pass to native code.</param>
      /// <returns>The SafePinnedObject wrapping the desired type's instance.</returns>
      public static SafePinnedObject FromType(Type type) {
         return FromObject(System.Runtime.Serialization.FormatterServices.GetUninitializedObject(type));
      }

      /// <summary>
      /// This factory method wraps a SafePinnedObject around a native block of memory.
      /// </summary>
      /// <param name="address">The starting address of the native block of memory.</param>
      /// <param name="numberOfBytes">The number of bytes in the native block of memory.</param>
      /// <returns>The SafePinnedObject wrapping the desired native block of memory.</returns>
      public static SafePinnedObject FromPointer(IntPtr address, Int32 numberOfBytes) {
         return new SafePinnedObject(address, numberOfBytes);
      }

      private SafePinnedObject(IntPtr address, Int32 numberOfBytes)
         : base(true) {
         m_size = numberOfBytes;
         SetHandle(address);
      }
      //internal IntPtr Address { get { return this.handle; } }

      /// <summary>This method is called when it is time to release the native resource.</summary>
      /// <returns>true if the native resource is released successfully.</returns>
      protected override Boolean ReleaseHandle() {
         SetHandle(IntPtr.Zero); // Just for safety, set the address to null
         if (m_gcHandle.IsAllocated) m_gcHandle.Free();      // Unpin the object
         return true;
      }

      #region Public methods to return Object reference and size (in bytes)
      /// <summary>
      /// Returns the object of a pinned buffer or null if not specified 
      /// </summary>
      public Object Target {
         get {
            return m_gcHandle.IsAllocated ? m_gcHandle.Target : null;
         }
      }

      /// <summary>
      /// Returns the number of bytes in a pinned object or 0 if not specified 
      /// </summary>
      public Int32 Size { get { return m_size; } }
      #endregion
   }
}


//////////////////////////////// End of File //////////////////////////////////