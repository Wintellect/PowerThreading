/******************************************************************************
Module:  OneManySpinResourceLock.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// A reader/writer lock that always spins in user-mode.
   /// </summary>
   public sealed class OneManySpinResourceLock : ResourceLock {
      private const Int32 c_lsFree = 0x00000000;
      private const Int32 c_lsOwnedByWriter = 0x00000001;

      private const Int32 c_1WritersPending = 0x00000100;
      private const Int32 c_WritersPendingMask = 0x0000FF00;

      private const Int32 c_1ReadersReading = 0x00010000;
      private const Int32 c_ReadersReadingMask = 0x00FF0000;

      // Here's how to interpret the m_LockState field's 4 bytes: nu RR WP WW
      // nu = not used, RR=ReadersReading, WP=WritersPending, WW=Writer is writing
      private Int32 m_LockState = c_lsFree;

      /// <summary>
      /// Constucts a OneManySpinResourceLock object.
      /// </summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily")]
      public OneManySpinResourceLock() : base(ResourceLockOptions.None) { }

      /// <summary>
      /// Implements the ResourceLock's WaitToWrite behavior.
      /// </summary>
      protected override void OnEnter(Boolean exclusive) {
         if (exclusive) {
            // Indicate that a writer wants to write: WP++
            Interlocked.Add(ref m_LockState, c_1WritersPending);
            StressPause();

            // OK to write if no readers are reading and 
            // no writers are pending: RR=0, WP=don't care, WW=0
            // Set the Writer is writing bit on.
            InterlockedEx.MaskedOr(ref m_LockState, c_lsOwnedByWriter, c_WritersPendingMask);
         } else {
            // OK to read if no writers are waiting: RR=don't care, WP=0, WW=0
            // If we're good, add 1 to the RR
            InterlockedEx.MaskedAdd(ref m_LockState, c_1ReadersReading, c_ReadersReadingMask);
         }
         StressPause();
      }

      /// <summary>
      /// Implements the ResourceLock's DoneWriting behavior.
      /// </summary>
      protected override void OnLeave(Boolean write) {
         if (write) {
            // Subtract 1 from waiting pending & turn off the writer is writing bit.
            StressPause();
            Interlocked.Add(ref m_LockState, -c_1WritersPending - c_lsOwnedByWriter);
         } else {
            // Subtract 1 from the RR
            Interlocked.Add(ref m_LockState, -c_1ReadersReading);
         }
         StressPause();
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////}
