/******************************************************************************
Module:  ArbitraryWaitHandle.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;
using System.ComponentModel;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Diagnostics.Contracts;

using HANDLE = System.IntPtr;

///////////////////////////////////////////////////////////////////////////////

namespace Wintellect.Threading {
   /// <summary>
   /// Converts various handles to waitable handle for synchronization.
   /// </summary>
   public sealed class ArbitraryWaitHandle : WaitHandle {
      private ArbitraryWaitHandle(HANDLE handle) {
         this.SafeWaitHandle =
            new SafeWaitHandle(DuplicateHandle(handle), true);
      }

      /// <summary>
      /// A factory method that converts a Win32 handle to an ArbitraryWaitHandle.
      /// </summary>
      /// <param name="handle">Identifies a handle to a synchronizable Win32 object.</param>
      /// <returns>An ArbitraryWaitHandle object.</returns>
      public static ArbitraryWaitHandle FromHandle(HANDLE handle) {
         return new ArbitraryWaitHandle(handle);
      }

      /// <summary>
      /// Implicitly casts a Win32 handle to an ArbitraryWaitHandle.
      /// </summary>
      /// <param name="handle">Identifies a handle to a synchronizable Win32 object.</param>
      /// <returns>An ArbitraryWaitHandle object.</returns>
      public static implicit operator ArbitraryWaitHandle(HANDLE handle) {
         return FromHandle(handle);
      }

      /// <summary>
      /// Converts a SafeHandle-derived object to an ArbitraryWaitHandle object.
      /// </summary>
      /// <param name="safeHandle">Identifies a SafeHandle to a synchronizable Win32 object.</param>
      /// <returns>An ArbitraryWaitHandle object.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle")]
      public static ArbitraryWaitHandle FromSafeHandle(SafeHandle safeHandle) {
         Contract.Requires(safeHandle != null);
         Boolean success = false;
         try {
            safeHandle.DangerousAddRef(ref success);
            if (!success) throw new InvalidOperationException("Couldn't AddRef");

            return new ArbitraryWaitHandle(safeHandle.DangerousGetHandle());
         }
         finally {
            safeHandle.DangerousRelease();
         }
      }

      /// <summary>
      /// Implicitly casts a SafeHandle-derived object to an ArbitraryWaitHandle.
      /// </summary>
      /// <param name="safeHandle">Identifies a SafeHandle to a synchronizable Win32 object.</param>
      /// <returns>An ArbitraryWaitHandle object.</returns>
      public static implicit operator ArbitraryWaitHandle(SafeHandle safeHandle) {
         Contract.Requires(safeHandle != null);
         return FromSafeHandle(safeHandle);
      }
      
      private static HANDLE DuplicateHandle(IntPtr sourceHandle) {
         HANDLE currentProcess = NativeMethods.GetCurrentProcess();
         HANDLE targetHandle;
         if (!NativeMethods.DuplicateHandle(currentProcess, sourceHandle, currentProcess, out targetHandle, 0, false, DuplicateHandleOptions.SameAcces))
            throw new Win32Exception();
         return targetHandle;
      }

      [Flags]
      private enum DuplicateHandleOptions {
         None = 0x00000000,
         CloseSource = 0x00000001,
         SameAcces = 0x00000002
      }

      private static class NativeMethods {
         [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
         internal static extern HANDLE GetCurrentProcess();

         [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean DuplicateHandle(
            HANDLE hSourceProcessHandle, HANDLE hSourceHandle,
            HANDLE hTargetProcessHandle, out HANDLE lpTargetHandle,
            UInt32 dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] Boolean bInheritHandle, DuplicateHandleOptions dwOptions);
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////
