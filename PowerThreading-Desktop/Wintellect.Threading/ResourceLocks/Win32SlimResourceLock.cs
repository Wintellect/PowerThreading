/******************************************************************************
Module:  Win32SlimResourceLock.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;
using System.Runtime.InteropServices;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// Implements a ResourceLock by way of the Windows SlimResourceLock. 
   /// This class is only available when running on Windows Vista or later.
   /// </summary>
   public sealed class Win32SlimResourceLock : ResourceLock {
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2006:UseSafeHandleToEncapsulateNativeResources")]
      private IntPtr m_SRWLock;

      /// <summary>
      /// Constructs a Win32SlimResourceLock.
      /// </summary>
      public Win32SlimResourceLock()
         : base(ResourceLockOptions.AcquiringThreadMustRelease) {
         NativeMethods.InitializeSRWLock(out m_SRWLock);
      }

      /// <summary>
      /// Implements the ResourceLock's WaitToWrite behavior.
      /// </summary>
      protected override void OnEnter(Boolean exclusive) {
         if (exclusive) NativeMethods.AcquireSRWLockExclusive(ref m_SRWLock);
         else NativeMethods.AcquireSRWLockShared(ref m_SRWLock);
      }

      /// <summary>
      /// Implements the ResourceLock's DoneWriting behavior.
      /// </summary>
      protected override void OnLeave(Boolean exclusive) {
         if (exclusive) NativeMethods.ReleaseSRWLockExclusive(ref m_SRWLock);
         else NativeMethods.ReleaseSRWLockShared(ref m_SRWLock);
      }

      private static class NativeMethods {
         [DllImport("Kernel32", CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
         internal static extern void InitializeSRWLock(out IntPtr srw);

         [DllImport("Kernel32", CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
         internal static extern void AcquireSRWLockExclusive(ref IntPtr srw);

         [DllImport("Kernel32", CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
         internal static extern void AcquireSRWLockShared(ref IntPtr srw);

         [DllImport("Kernel32", CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
         internal static extern void ReleaseSRWLockExclusive(ref IntPtr srw);

         [DllImport("Kernel32", CallingConvention = CallingConvention.Winapi, ExactSpelling = true)]
         internal static extern void ReleaseSRWLockShared(ref IntPtr srw);
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////}
