/******************************************************************************
Module:  RecursionResourceLockModifier.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/

using System;
using System.Threading;
using System.Collections.Generic;
using System.Globalization;
using System.Diagnostics.Contracts;

///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks.Diagnostics {
   /// <summary>
   /// A ResourceLock modifier that adds recursion support to the inner lock.
   /// </summary>
   public class RecursionResourceLockModifier : ResourceLockModifier {
      private struct ThreadIdAndRecurseCount {
         public Int32 m_Id, m_Count;
         public override String ToString() {
            return String.Format(CultureInfo.InvariantCulture, "Id={0}, Count={1}", m_Id, m_Count);
         }
      }

      private ThreadIdAndRecurseCount m_WriterThreadIdAndRecurseCount;
      private ThreadIdAndRecurseCount[] m_ReaderThreadIdsAndRecurseCounts;
      /// <summary>
      /// Constructs a RecursionResourceLockModifier object.
      /// </summary>
      /// <param name="resLock">A reference to a ResourceLock object that will not support recursion.</param>
      /// <param name="maxReaders">The maximum number of concurrent reader threads that this 
      /// RecursionResourceLockModifier object should support.</param>
      public RecursionResourceLockModifier(ResourceLock resLock, Int32 maxReaders)
         : base(resLock, ResourceLockOptions.AcquiringThreadMustRelease | ResourceLockOptions.SupportsRecursion |
          (resLock.IsMutualExclusive ? ResourceLockOptions.IsMutualExclusive : 0)) {
         Contract.Requires(resLock != null);
         Contract.Requires(maxReaders >= 0);
         m_ReaderThreadIdsAndRecurseCounts = new ThreadIdAndRecurseCount[maxReaders];
      }

      private Boolean TryFindThreadIdIndex(Int32 threadId, out Int32 index) {
         Contract.Assume(m_ReaderThreadIdsAndRecurseCounts != null);
         // The JITter produces more efficient code if we load the array reference into a temporary
         ThreadIdAndRecurseCount[] readerThreadIdsAndRecurseCounts = m_ReaderThreadIdsAndRecurseCounts;
         for (index = 0; index < readerThreadIdsAndRecurseCounts.Length; index++) {
            if (readerThreadIdsAndRecurseCounts[index].m_Id == threadId)
               return true;
         }
         return false;
      }

      private void AddThreadIdWithRecurseCountOf1(Int32 callingThreadId) {
         Contract.Assume(m_ReaderThreadIdsAndRecurseCounts != null);
         // The JITter produces more efficient code if we load the array reference into a temporary
         ThreadIdAndRecurseCount[] readerThreadIdsAndRecurseCounts = m_ReaderThreadIdsAndRecurseCounts;
         for (Int32 index = 0; index < readerThreadIdsAndRecurseCounts.Length; index++) {
            if (readerThreadIdsAndRecurseCounts[index].m_Id == 0) {
               if (InterlockedEx.IfThen(ref readerThreadIdsAndRecurseCounts[index].m_Id, 0, callingThreadId)) {
                  readerThreadIdsAndRecurseCounts[index].m_Count = 1;
                  return;
               } else {
                  // We found a slot but then it was taken away from us
                  index = -1; // Start the search over again from the beginning
                  continue;
               }
            }
         }
         throw new InvalidOperationException("More current reader threads than allowed!");
      }

      #region Override of ResourceLock Members
      /// <summary>Implements the ResourceLock's WaitToWrite behavior.</summary>
      protected override void OnEnter(Boolean exclusive) {
         Int32 CallingThreadId = Thread.CurrentThread.ManagedThreadId;
         if (exclusive) {
            // If the calling thread already owns the lock, add 1 to the recursion count and return
            if (CallingThreadId == m_WriterThreadIdAndRecurseCount.m_Id) {
               m_WriterThreadIdAndRecurseCount.m_Count++;
               return;
            }
            InnerLock.Enter(exclusive);
            Interlocked.Exchange(ref m_WriterThreadIdAndRecurseCount.m_Id, CallingThreadId);
            m_WriterThreadIdAndRecurseCount.m_Count = 0;
         } else {
            Int32 index;
            if (TryFindThreadIdIndex(CallingThreadId, out index)) {
               // This thread has the reader lock, increment the count and return
               m_ReaderThreadIdsAndRecurseCounts[index].m_Count++;
               return;
            }

            // This thread doesn't have the lock, wait for it
            InnerLock.Enter(exclusive);

            // Record that this thread has the reader lock once
            AddThreadIdWithRecurseCountOf1(CallingThreadId);
         }
      }

      /// <summary>Implements the ResourceLock's Leave behavior.</summary>
      protected override void OnLeave(Boolean exclusive) {
         Int32 CallingThreadId = Thread.CurrentThread.ManagedThreadId;
         if (exclusive) {
            if (m_WriterThreadIdAndRecurseCount.m_Id != CallingThreadId)
               throw new InvalidOperationException("Calling thread doesn't own this lock for writing!");

            if (--m_WriterThreadIdAndRecurseCount.m_Count > 0) return;

            Interlocked.Exchange(ref m_WriterThreadIdAndRecurseCount.m_Id, 0);
            InnerLock.Leave();
         } else {
            Int32 index;
            if (!TryFindThreadIdIndex(CallingThreadId, out index)) {
               throw new InvalidOperationException("Calling thread doesn't own the lock for reading!");
            }
            // Decrement this readers recursion count
            if (--m_ReaderThreadIdsAndRecurseCounts[index].m_Count == 0) {
               // If this reader is done (recursion count == 0), remove this reader off the list
               Interlocked.Exchange(ref m_ReaderThreadIdsAndRecurseCounts[index].m_Id, 0);

               // This reader gives up the lock too
               InnerLock.Leave();
            }
         }
      }
      #endregion
   }
}

//////////////////////////////// End of File //////////////////////////////////
