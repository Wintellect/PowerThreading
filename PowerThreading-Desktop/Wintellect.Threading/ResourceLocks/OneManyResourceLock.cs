/******************************************************************************
Module:  OneManyResourceLock.cs
Notices: Copyright (c) 2006-2012 by Jeffrey Richter and Wintellect
******************************************************************************/


//#define Stress
using System;
using System.Diagnostics;
using System.Threading;
using System.Globalization;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// Implements a ResourceLock by way of a high-speed reader/writer lock.
   /// </summary>
   public sealed class OneManyResourceLock : ResourceLock {
      #region Lock State Management
#if false
      private struct BitField {
         private Int32 m_mask, m_1, m_startBit;
         public BitField(Int32 startBit, Int32 numBits) {
            m_startBit = startBit;
            m_mask = unchecked((Int32)((1 << numBits) - 1) << startBit);
            m_1 = unchecked((Int32)1 << startBit);
         }
         public void Increment(ref Int32 value) { value += m_1; }
         public void Decrement(ref Int32 value) { value -= m_1; }
         public void Decrement(ref Int32 value, Int32 amount) { value -= m_1 * amount; }
         public Int32 Get(Int32 value) { return (value & m_mask) >> m_startBit; }
         public Int32 Set(Int32 value, Int32 fieldValue) { return (value & ~m_mask) | (fieldValue << m_startBit); }
      }

      private static BitField s_state = new BitField(0, 3);
      private static BitField s_readersReading = new BitField(3, 9);
      private static BitField s_readersWaiting = new BitField(12, 9);
      private static BitField s_writersWaiting = new BitField(21, 9);
      private static OneManyLockStates State(Int32 value) { return (OneManyLockStates)s_state.Get(value); }
      private static void State(ref Int32 ls, OneManyLockStates newState) {
         ls = s_state.Set(ls, (Int32)newState);
      }
#endif
      private enum OneManyLockStates {
         Free = 0x00000000,
         OwnedByWriter = 0x00000001,
         OwnedByReaders = 0x00000002,
         OwnedByReadersAndWriterPending = 0x00000003,
         ReservedForWriter = 0x00000004,
      }

      private const Int32 c_lsStateStartBit = 0;
      private const Int32 c_lsReadersReadingStartBit = 3;
      private const Int32 c_lsReadersWaitingStartBit = 12;
      private const Int32 c_lsWritersWaitingStartBit = 21;

      // Mask = unchecked((Int32) ((1 << numBits) - 1) << startBit);
      private const Int32 c_lsStateMask = unchecked((Int32)((1 << 3) - 1) << c_lsStateStartBit);
      private const Int32 c_lsReadersReadingMask = unchecked((Int32)((1 << 9) - 1) << c_lsReadersReadingStartBit);
      private const Int32 c_lsReadersWaitingMask = unchecked((Int32)((1 << 9) - 1) << c_lsReadersWaitingStartBit);
      private const Int32 c_lsWritersWaitingMask = unchecked((Int32)((1 << 9) - 1) << c_lsWritersWaitingStartBit);
      private const Int32 c_lsAnyWaitingMask = c_lsReadersWaitingMask | c_lsWritersWaitingMask;

      // FirstBit = unchecked((Int32) 1 << startBit);
      private const Int32 c_ls1ReaderReading = unchecked((Int32)1 << c_lsReadersReadingStartBit);
      private const Int32 c_ls1ReaderWaiting = unchecked((Int32)1 << c_lsReadersWaitingStartBit);
      private const Int32 c_ls1WriterWaiting = unchecked((Int32)1 << c_lsWritersWaitingStartBit);

      private static OneManyLockStates State(Int32 ls) { return (OneManyLockStates)(ls & c_lsStateMask); }
      private static void SetState(ref Int32 ls, OneManyLockStates newState) {
         ls = (ls & ~c_lsStateMask) | ((Int32)newState);
      }

      private static Int32 NumReadersReading(Int32 ls) { return (ls & c_lsReadersReadingMask) >> c_lsReadersReadingStartBit; }
      private static void AddReadersReading(ref Int32 ls, Int32 amount) { ls += (c_ls1ReaderReading * amount); }

      private static Int32 NumReadersWaiting(Int32 ls) { return (ls & c_lsReadersWaitingMask) >> c_lsReadersWaitingStartBit; }
      private static void AddReadersWaiting(ref Int32 ls, Int32 amount) { ls += (c_ls1ReaderWaiting * amount); }

      private static Int32 NumWritersWaiting(Int32 ls) { return (ls & c_lsWritersWaitingMask) >> c_lsWritersWaitingStartBit; }
      private static void AddWritersWaiting(ref Int32 ls, Int32 amount) { ls += (c_ls1WriterWaiting * amount); }

      private static Boolean AnyWaiters(Int32 ls) { return (ls & c_lsAnyWaitingMask) != 0; }

      private static String DebugState(Int32 ls) {
         return String.Format(CultureInfo.InvariantCulture,
            "State={0}, RR={1}, RW={2}, WW={3}", State(ls),
            NumReadersReading(ls), NumReadersWaiting(ls),
            NumWritersWaiting(ls));
      }

      /// <summary>
      /// Returns a string representing the state of the object.
      /// </summary>
      /// <returns>The string representing the state of the object.</returns>
      public override String ToString() { return DebugState(m_LockState); }
      #endregion

      #region State Fields
      private Int32 m_LockState = (Int32)OneManyLockStates.Free;

      // Readers wait on this if a writer owns the lock
      private Semaphore m_ReadersLock = new Semaphore(0, Int32.MaxValue);

      // Writers wait on this if a reader owns the lock
      private Semaphore m_WritersLock = new Semaphore(0, Int32.MaxValue);
      #endregion

      #region Construction and Dispose
      /// <summary>Constructs a OneManyLock object.</summary>
      public OneManyResourceLock() : base(ResourceLockOptions.None) { }

      ///<summary>Releases all resources used by the lock.</summary>
      protected override void Dispose(Boolean disposing) {
         m_WritersLock.Close(); m_WritersLock = null;
         m_ReadersLock.Close(); m_ReadersLock = null;
         base.Dispose(disposing);
      }
      #endregion

      #region Writer members
      /// <summary>Acquires the lock.</summary>
      protected override void OnEnter(Boolean exclusive) {
         if (exclusive) {
            while (WaitToWrite(ref m_LockState)) m_WritersLock.WaitOne();
         } else {
            while (WaitToRead(ref m_LockState)) m_ReadersLock.WaitOne();
         }
      }

      private static Boolean WaitToWrite(ref Int32 target) {
         Int32 start, current = target;
         Boolean wait;
         do {
            start = current;
            Int32 desired = start;
            wait = false;

            switch (State(desired)) {
               case OneManyLockStates.Free:  // If Free -> OBW, return
               case OneManyLockStates.ReservedForWriter: // If RFW -> OBW, return
                  SetState(ref desired, OneManyLockStates.OwnedByWriter);
                  break;

               case OneManyLockStates.OwnedByWriter:  // If OBW -> WW++, wait & loop around
                  AddWritersWaiting(ref desired, 1);
                  wait = true;
                  break;

               case OneManyLockStates.OwnedByReaders: // If OBR or OBRAWP -> OBRAWP, WW++, wait, loop around
               case OneManyLockStates.OwnedByReadersAndWriterPending:
                  SetState(ref desired, OneManyLockStates.OwnedByReadersAndWriterPending);
                  AddWritersWaiting(ref desired, 1);
                  wait = true;
                  break;
               default:
                  Debug.Assert(false, "Invalid Lock state");
                  break;
            }
            current = Interlocked.CompareExchange(ref target, desired, start);
         } while (start != current);
         return wait;
      }

      /// <summary>Releases the lock.</summary>
      protected override void OnLeave(Boolean write) {
         Int32 wakeup;
         if (write) {
            Debug.Assert((State(m_LockState) == OneManyLockStates.OwnedByWriter) && (NumReadersReading(m_LockState) == 0));
            // Pre-condition:  Lock's state must be OBW (not Free/OBR/OBRAWP/RFW)
            // Post-condition: Lock's state must become Free or RFW (the lock is never passed)

            // Phase 1: Release the lock
            wakeup = DoneWriting(ref m_LockState);
         } else {
            var s = State(m_LockState);
            Debug.Assert((State(m_LockState) == OneManyLockStates.OwnedByReaders) || (State(m_LockState) == OneManyLockStates.OwnedByReadersAndWriterPending));
            // Pre-condition:  Lock's state must be OBR/OBRAWP (not Free/OBW/RFW)
            // Post-condition: Lock's state must become unchanged, Free or RFW (the lock is never passed)

            // Phase 1: Release the lock
            wakeup = DoneReading(ref m_LockState);
         }

         // Phase 2: Possibly wake waiters
         if (wakeup == -1) m_WritersLock.Release();
         else if (wakeup > 0) m_ReadersLock.Release(wakeup);
      }

      // Returns -1 to wake a writer, +# to wake # readers, or 0 to wake no one
      private static Int32 DoneWriting(ref Int32 target) {
         Int32 start, current = target;
         Int32 wakeup = 0;
         do {
            Int32 desired = (start = current);

            // We do this test first because it is commonly true & 
            // we avoid the other tests improving performance
            if (!AnyWaiters(desired)) {
               SetState(ref desired, OneManyLockStates.Free);
               wakeup = 0;
            } else if (NumWritersWaiting(desired) > 0) {
               SetState(ref desired, OneManyLockStates.ReservedForWriter);
               AddWritersWaiting(ref desired, -1);
               wakeup = -1;
            } else {
               wakeup = NumReadersWaiting(desired);
               Debug.Assert(wakeup > 0);
               SetState(ref desired, OneManyLockStates.OwnedByReaders);
               AddReadersWaiting(ref desired, -wakeup);
               // RW=0, RR=0 (incremented as readers enter)
            }
            current = Interlocked.CompareExchange(ref target, desired, start);
         } while (start != current);
         return wakeup;
      }
      #endregion

      #region Reader members
      private static Boolean WaitToRead(ref Int32 target) {
         Int32 start, current = target;
         Boolean wait;
         do {
            Int32 desired = (start = current);
            wait = false;

            switch (State(desired)) {
               case OneManyLockStates.Free:  // If Free->OBR, RR=1, return
                  SetState(ref desired, OneManyLockStates.OwnedByReaders);
                  AddReadersReading(ref desired, 1);
                  break;

               case OneManyLockStates.OwnedByReaders: // If OBR -> RR++, return
                  AddReadersReading(ref desired, 1);
                  break;

               case OneManyLockStates.OwnedByWriter:  // If OBW/OBRAWP/RFW -> RW++, wait, loop around
               case OneManyLockStates.OwnedByReadersAndWriterPending:
               case OneManyLockStates.ReservedForWriter:
                  AddReadersWaiting(ref desired, 1);
                  wait = true;
                  break;

               default:
                  Debug.Assert(false, "Invalid Lock state");
                  break;
            }
            current = Interlocked.CompareExchange(ref target, desired, start);
         } while (start != current);
         return wait;
      }

      // Returns -1 to wake a writer, +# to wake # readers, or 0 to wake no one
      private static Int32 DoneReading(ref Int32 target) {
         Int32 start, current = target;
         Int32 wakeup;
         do {
            Int32 desired = (start = current);
            AddReadersReading(ref desired, -1);  // RR--
            if (NumReadersReading(desired) > 0) {
               // RR>0, no state change & no threads to wake
               wakeup = 0;
            } else if (!AnyWaiters(desired)) {
               SetState(ref desired, OneManyLockStates.Free);
               wakeup = 0;
            } else {
               Debug.Assert(NumWritersWaiting(desired) > 0);
               SetState(ref desired, OneManyLockStates.ReservedForWriter);
               AddWritersWaiting(ref desired, -1);
               wakeup = -1;   // Wake 1 writer
            }
            current = Interlocked.CompareExchange(ref target, desired, start);
         } while (start != current);
         return wakeup;
      }
      #endregion
   }
}


//////////////////////////////// End of File //////////////////////////////////