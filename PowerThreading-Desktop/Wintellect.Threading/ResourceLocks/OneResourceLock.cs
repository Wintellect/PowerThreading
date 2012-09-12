/******************************************************************************
Module:  OneResourceLock.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


//#define Stress
using System;
using System.Diagnostics;
using System.Threading;
using System.Globalization;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// Implements a ResourceLock via a high-speed mutual-exclusive lock.
   /// </summary>
   public sealed class OneResourceLock : ResourceLock {
      // m_LockState's 4 bytes are interpreted like this: 00 00 WW ls
      #region Lock State Management
      private enum OneLockStates {
         Free = 0x00000000,
         OwnedByWriter = 0x00000001
      }
      private const Int32 c_lsStateMask = 0x000000ff;
      private const Int32 c_lsWritersWaitingMask = 0x0000ff00;
      private const Int32 c_ls1WriterWaiting = 0x00000100;
      private static OneLockStates State(Int32 ls) {
         return (OneLockStates)(ls & c_lsStateMask);
      }
      private static void State(ref Int32 ls, OneLockStates newState) {
         ls = (ls & ~c_lsStateMask) | ((Int32)newState);
      }

      private static Int32 NumWritersWaiting(Int32 ls) { return (ls & c_lsWritersWaitingMask) >> 8; }
      private static void IncWritersWaiting(ref Int32 ls) { ls += c_ls1WriterWaiting; }
      private static void DecWritersWaiting(ref Int32 ls) { ls -= c_ls1WriterWaiting; }

      private enum WakeUp { None, Writer }
      private Int32 NumWritersToWake() {
         Int32 ls = m_LockState;

         // If lock is Free && WW>0, try to subtract 1 writer
         while ((State(ls) == OneLockStates.Free) && (NumWritersWaiting(ls) > 0)) {
            Int32 desired = ls;
            DecWritersWaiting(ref desired);
            if (InterlockedEx.IfThen(ref m_LockState, ls, desired, out ls)) {
               // We sucessfully subtracted 1 waiting writer, wake it up
               return 1;
            }
         }
         return 0;
      }

      /// <summary>
      /// Returns a string representing the state of the object.
      /// </summary>
      /// <returns>The string representing the state of the object.</returns>
      public override string ToString() {
         Int32 ls = m_LockState;
         return String.Format(CultureInfo.InvariantCulture,
            "State={0}, WW={1}", State(ls), NumWritersWaiting(ls));
      }
      #endregion

      #region State Fields
      private Int32 m_LockState = (Int32)OneLockStates.Free;

      // Writers wait on this if another writer owns the lock
      private Semaphore m_WritersLock = new Semaphore(0, Int32.MaxValue);
      #endregion

      #region Construction and Dispose
      /// <summary>
      /// Constructs a OneResourceLock object.
      /// </summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily")]
      public OneResourceLock() : base(ResourceLockOptions.IsMutualExclusive) { }

      /// <summary>
      /// Allow the object to clean itself up.
      /// </summary>
      /// <param name="disposing">true if the object is being disposed; false if it is being finalzied.</param>
      protected override void Dispose(Boolean disposing) {
         try {
            if (disposing) {
               m_WritersLock.Close(); m_WritersLock = null;
            }
         }
         finally {
            base.Dispose(disposing);
         }
      }
      #endregion

      #region Writer members
      /// <summary>
      /// Implements the ResourceLock's WaitToWrite behavior.
      /// </summary>
      protected override void OnEnter(Boolean exclusive) {
         while (WaitToWrite(ref m_LockState)) m_WritersLock.WaitOne();
      }

      private static Boolean WaitToWrite(ref Int32 target) {
         Int32 i, j = target;
         Boolean wait;
         do {
            i = j;
            Int32 desired = i;
            wait = false;

            switch (State(desired)) {
               case OneLockStates.Free:  // If Free->OBW, return
                  State(ref desired, OneLockStates.OwnedByWriter);
                  break;

               case OneLockStates.OwnedByWriter:  // If OBW -> WW++, wait & loop around
                  IncWritersWaiting(ref desired);
                  wait = true;
                  break;

               default:
                  Debug.Assert(false, "Invalid Lock state");
                  break;
            }
            j = Interlocked.CompareExchange(ref target, desired, i);
         } while (i != j);
         return wait;
      }

      /// <summary>
      /// Implements the ResourceLock's DoneWriting behavior.
      /// </summary>
      protected override void OnLeave(Boolean write) {
         Debug.Assert(State(m_LockState) == OneLockStates.OwnedByWriter);
         // Pre-condition:  Lock's state must be OBW (not Free)
         // Post-condition: Lock's state must become Free (the lock is never passed)

         // Phase 1: Release the lock
         WakeUp wakeup = DoneWriting(ref m_LockState);

         // Phase 2: Possibly wake waiters
         switch (wakeup) {
            case WakeUp.None:
               break;
            case WakeUp.Writer:
               Int32 numWritersToWake = NumWritersToWake();
               Debug.Assert(numWritersToWake < 2);    // Must be 0 or 1
               if (numWritersToWake > 0) m_WritersLock.Release(numWritersToWake);
               break;
         }
      }

      private static WakeUp DoneWriting(ref Int32 target) {
         Int32 i, j = target;
         WakeUp wakeup = WakeUp.None;
         do {
            i = j;
            Int32 desired = i;

            // The lock should become free
            State(ref desired, OneLockStates.Free);

            // Possible wake a waiting writer
            wakeup = (NumWritersWaiting(desired) > 0) ? WakeUp.Writer : WakeUp.None;
            j = Interlocked.CompareExchange(ref target, desired, i);
         } while (i != j);
         return wakeup;
      }
      #endregion
   }
}


//////////////////////////////// End of File //////////////////////////////////