/******************************************************************************
Module:  ExclusiveSpinResourceLock.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// An exclusive lock that always spins in user-mode.
   /// </summary>
   public sealed class ExclusiveSpinResourceLock : ResourceLock {
      private SpinWaitLock m_lock = new SpinWaitLock();

      /// <summary>
      /// Constructs an ExclusiveSpinResourceLock.
      /// </summary>
      public ExclusiveSpinResourceLock() : base(ResourceLockOptions.IsMutualExclusive) { }

      /// <summary>
      /// Implements the ResourceLock's WaitToWrite behavior.
      /// </summary>
      protected override void OnEnter(Boolean exclusive) {
         m_lock.Enter();
      }

      /// <summary>
      /// Implements the ResourceLock's DoneWriting behavior.
      /// </summary>
      protected override void OnLeave(Boolean write) {
         m_lock.Exit();
      }
   }
}


namespace Wintellect.Threading.ResourceLocks {
   // NOTE: This is a value type so it works very efficiently when used
   // as a field in a class. Avoid boxing this or you will lose thread safety!
   internal struct SpinWaitLock {
      private const Int32 c_lsFree = 0;
      private const Int32 c_lsOwned = 1;
      private volatile Int32 m_lockState;	// Defaults to 0=c_lsFree

      public void Enter() {
         while (true) {
            #pragma warning disable 420   // 'identifier': a reference to a volatile field will not be treated as volatile
            // If resource available, set it to in-use and return
            if (Interlocked.Exchange(ref m_lockState, c_lsOwned) == c_lsFree) {
               return;
            }
            #pragma warning restore 420

            // Efficiently spin, until the resource looks like it might be free
            // NOTE: m_LockState is volatile which is faster than Thread.VolatileRead
            while (m_lockState == c_lsOwned) ThreadUtility.StallThread();
         }
      }

      public void Exit() {
         // Mark the resource as available
         m_lockState = c_lsFree; // Note: m_lockState is volatile
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////}
