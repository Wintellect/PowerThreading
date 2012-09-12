/******************************************************************************
Module:  OptexResourceLock.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Diagnostics;
using System.Threading;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// A fast mutual-exclusive lock
   /// </summary>
   public class OptexResourceLock : ResourceLock {
      /// <summary>
      /// Constructs an OptexResourceLock.
      /// </summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily")]
      public OptexResourceLock() : base(ResourceLockOptions.IsMutualExclusive) { }

      // Bit     0: 0=Lock is free, 1=Lock is owned
      // Bits 1-31: Number of waiters
      private Int32 m_LockState = c_lsFree;

      private const Int32 c_lsFree = 0x00000000;
      private const Int32 c_lsOwned = 0x00000001;
      private const Int32 c_1Waiter = 0x00000002;

      private Semaphore m_WaiterLock = new Semaphore(0, Int32.MaxValue);

      /// <summary>
      /// Allows the object to clean itself up.
      /// </summary>
      /// <param name="disposing">true if the object is being disposed; false if being finalized.</param>
      protected override void Dispose(Boolean disposing) {
         try {
            if (disposing) { m_WaiterLock.Close(); m_WaiterLock = null; }
         }
         finally {
            base.Dispose(disposing);
         }
      }

      /// <summary>
      /// Implements the ResourceLock's WaitToWrite behavior.
      /// </summary>
      protected override void OnEnter(Boolean exclusive) {
         while (true) {
            // Turn on the "owned" bit
            Int32 ls = InterlockedEx.Or(ref m_LockState, c_lsOwned);
            StressPause();

            // If lock was free, this thread got it, return
            if ((ls & c_lsOwned) == c_lsFree) return;
            StressPause();
            // Another thread owned the lock, add 1 waiter
            if (IfThen(ref m_LockState, ls, ls + c_1Waiter)) {
               // If successfully added 1, wait for lock
               m_WaiterLock.WaitOne();
               StressPause();
            }
            // We weren't able to add 1 waiter or waiter woke, attempt to get the lock
            StressPause();
         }
      }

      /// <summary>
      /// Implements the ResourceLock's DoneWriting behavior.
      /// </summary>
      protected override void OnLeave(Boolean exclusive) {
         // Pre-condition:  Lock's state must be Owned
         // Post-condition: Lock's state must become Free (the lock is never passed)

         // Phase 1: Free the lock
         Int32 ls = InterlockedEx.And(ref m_LockState, ~c_lsOwned);
         if (ls == c_lsOwned) {
            StressPause();
            // If no waiters, nothing to do, we can just return
         } else {
            // Phase 2: Possibly wake waiters
            // If lock is free, try to subtract 1 from the number of waiters
            ls &= ~c_lsOwned;
            if (IfThen(ref m_LockState, ls, ls - c_1Waiter)) {
               StressPause();
               // We sucessfully subtracted 1, wake 1 waiter
               m_WaiterLock.Release(1);
               StressPause();
            } else {
               // Lock's state changed by other thread, other thread will deal with it
               StressPause();
            }
         }
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////

namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// Implements a recursive mutual-exclusive lock
   /// </summary>
   public sealed class RecursiveOptex : IDisposable {
      private static readonly Boolean s_uniProcessor = (Environment.ProcessorCount == 1);
      private Int32 m_spincount;
      private Int32 m_waiters = 0;
      private Int32 m_owningThreadId = 0;
      private Int32 m_recursionCount = 0;
      private AutoResetEvent m_waiterLock = new AutoResetEvent(false);

      /// <summary>
      /// Constructs a RecursiveOptex with the specified user-mode spin count
      /// </summary>
      /// <param name="spinCount">The number of times the lock should spin in user-mode
      /// when there is contention on the lock before waiting in the kernel.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily")]
      public RecursiveOptex(Int32 spinCount) {
         m_spincount = s_uniProcessor ? 0 : spinCount;
      }

      /// <summary>
      /// Allows the object to clean itself up.
      /// </summary>
      public void Dispose() {
         m_waiterLock.Close(); m_waiterLock = null;
      }

      /// <summary>
      /// Causes the calling thread to enter the lock.
      /// </summary>
      public void Enter() {
         Int32 threadId = Thread.CurrentThread.ManagedThreadId;
         if (threadId == m_owningThreadId) {
            // This thread owns the lock and wants it again
            m_recursionCount++;
            return;
         }

         for (Int32 spinCount = 0; spinCount < m_spincount; spinCount++) {
            if (Interlocked.CompareExchange(ref m_waiters, 1, 0) == 0)
               goto GotLock;
         }

         // After spinning, try 1 more time to get the lock
         if (Interlocked.Increment(ref m_waiters) == 1)
            goto GotLock;

         // We still can't get the lock; wait for it
         m_waiterLock.WaitOne();

      GotLock:
         // This thread got the lock
         m_owningThreadId = threadId;
         m_recursionCount = 1;
      }

      /// <summary>
      /// Causes the calling thread to release the lock.
      /// </summary>
      public void Exit() {
         Int32 threadId = Thread.CurrentThread.ManagedThreadId;
         if (threadId != m_owningThreadId)
            throw new SynchronizationLockException("Lock not owned by current thread");

         // If this thread doesn't completely release it, just return
         if (--m_recursionCount > 0) return;

         m_owningThreadId = 0;   // No thread owns it now

         if (Interlocked.Decrement(ref m_waiters) > 0)
            m_waiterLock.Set();
      }
   }
}
