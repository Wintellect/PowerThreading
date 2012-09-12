/******************************************************************************
Module:  SyncGate.cs
Notices: Copyright (c) 2011 by Jeffrey Richter and Wintellect
******************************************************************************/


#if INCLUDE_GATES
using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// Indicates if the SyncGate should be acquired for exclusive or shared access.
   /// </summary>
   public enum SyncGateMode {
      /// <summary>
      /// Indicates that exclusive access is required.
      /// </summary>
      Exclusive,

      /// <summary>
      /// Indicates that shared access is required.
      /// </summary>
      Shared
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.AsyncProgModel {
   using Wintellect.Threading.AsyncProgModel;
   using System.Diagnostics.Contracts;

   /// <summary>
   /// This class implements a reader/writer lock that never blocks any threads. 
   /// This class integrates very well with the AsyncEnumerator class.
   /// </summary>
   public sealed class SyncGate {
      private readonly Object m_syncLock = new Object();

      private enum SyncGateStates {
         Free = 0,
         OwnedByReaders = 1,
         OwnedByReadersAndWriterPending = 2,
         OwnedByWriter = 3,
         ReservedForWriter = 4
      }
      private SyncGateStates m_state = SyncGateStates.Free;
      private Int32 m_numReaders = 0;

      private readonly Queue<SyncGateAsyncResult> m_qWriteRequests = new Queue<SyncGateAsyncResult>();
      private readonly Queue<SyncGateAsyncResult> m_qReadRequests = new Queue<SyncGateAsyncResult>();

      /// <summary>Constructs a SyncGate object.</summary>
      public SyncGate() : this(false) { }

      /// <summary>Constructs a SyncGate object</summary>
      /// <param name="blockReadersUntilFirstWriteCompletes">Pass true to have readers block until the first writer has created the data that is being protected by the SyncGate.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily")]
      public SyncGate(Boolean blockReadersUntilFirstWriteCompletes) {
         m_state = blockReadersUntilFirstWriteCompletes ? SyncGateStates.ReservedForWriter : SyncGateStates.Free;
      }

      private sealed class SyncGateAsyncResult : AsyncResult {
         private SyncGateMode m_mode;
         internal SyncGateMode Mode { get { return m_mode; } }

         internal SyncGateAsyncResult(SyncGateMode mode, AsyncCallback asyncCallback, Object state)
            : base(asyncCallback, state) {
            m_mode = mode;
         }
      }

      #region BeginRegion/EndRegion Members
      /// <summary>
      /// Allows the caller to notify the SyncGate that it wants exclusive or shared access to a resource. 
      /// </summary>
      /// <param name="mode">Indicates if exclusive or shared access is desired.</param>
      /// <param name="asyncCallback">The callback method to invoke once access can be granted.</param>
      public void BeginRegion(SyncGateMode mode, AsyncCallback asyncCallback) {
         BeginRegion(mode, asyncCallback, null);
      }

      /// <summary>
      /// Allows the caller to notify the SyncGate that it wants exclusive or shared access to a resource. 
      /// </summary>
      /// <param name="mode">Indicates if exclusive or shared access is desired.</param>
      /// <param name="asyncCallback">The callback method to invoke once access can be granted.</param>
      /// <param name="asyncState">Additional state to pass to the callback method.</param>
      public void BeginRegion(SyncGateMode mode, AsyncCallback asyncCallback, Object asyncState) {
         Contract.Assume(m_qReadRequests != null);
         Contract.Assume(m_qWriteRequests != null);
         // This method supports the method callback version of the IAsyncResult APM only; therefore,
         // a callback method must always be specified and this is also why the method returns void 
         // instead of returning an IAsyncResult
         if (asyncCallback == null) throw new ArgumentNullException("asyncCallback");
         SyncGateAsyncResult ar = new SyncGateAsyncResult(mode, asyncCallback, asyncState);
         Boolean goodToGo = false;
         Monitor.Enter(m_syncLock);
         switch (mode) {
            case SyncGateMode.Exclusive:
               switch (m_state) {
                  case SyncGateStates.Free:             // If Free  | RFW -> OBW, invoke, return
                  case SyncGateStates.ReservedForWriter:
                     m_state = SyncGateStates.OwnedByWriter;
                     goodToGo = true;  // QueueCallback(ar);
                     break;

                  case SyncGateStates.OwnedByReaders:   // If OBR | OBRAWP -> OBRAWP, queue, return
                  case SyncGateStates.OwnedByReadersAndWriterPending:
                     m_state = SyncGateStates.OwnedByReadersAndWriterPending;
                     m_qWriteRequests.Enqueue(ar);
                     break;

                  case SyncGateStates.OwnedByWriter:   // If OBW, queue, return
                     m_qWriteRequests.Enqueue(ar);
                     break;
               }
               break;

            case SyncGateMode.Shared:
               switch (m_state) {
                  case SyncGateStates.Free:   // If Free | OBR -> OBR, NR++, invoke, return
                  case SyncGateStates.OwnedByReaders:
                     m_state = SyncGateStates.OwnedByReaders;
                     m_numReaders++;
                     goodToGo = true; // QueueCallback(ar);
                     break;

                  case SyncGateStates.OwnedByWriter:   // If OBW | OBRAWP | RFW, queue, return
                  case SyncGateStates.OwnedByReadersAndWriterPending:
                  case SyncGateStates.ReservedForWriter:
                     m_qReadRequests.Enqueue(ar);
                     break;
               }
               break;
         }
         Monitor.Exit(m_syncLock);
         if (goodToGo) ar.SetAsCompleted(null, true);
      }

      /// <summary>
      /// Call this method after accessing the resource to notify the SyncGate that it can grant access to other code.
      /// </summary>
      /// <param name="result">The IAsyncResult object given to the callback method when access was granted.</param>
      public void EndRegion(IAsyncResult result) {
         if (result == null) throw new ArgumentNullException("result");
         SyncGateAsyncResult sgar = (SyncGateAsyncResult)result;
         sgar.EndInvoke();

         Monitor.Enter(m_syncLock);
         if (sgar.Mode == SyncGateMode.Shared) {
            // Subtract a reader and return (without changing the gate's state) 
            // if this is not the last reader
            if (--m_numReaders > 0) {
               Monitor.Exit(m_syncLock);
               return;
            }

            // This was the last reader, wake up any queued requests
         }

         Contract.Assume(m_qReadRequests != null);
         Contract.Assume(m_qWriteRequests != null);

         // Wake-up any queued requests
         if (m_qWriteRequests.Count > 0) {
            // A writer is queued, invoke it
            m_state = SyncGateStates.OwnedByWriter;
            QueueCallback(m_qWriteRequests.Dequeue());
         } else if (m_qReadRequests.Count > 0) {
            // Reading requests are queued, invoke all of them
            m_state = SyncGateStates.OwnedByReaders;
            m_numReaders = m_qReadRequests.Count;
            while (m_qReadRequests.Count > 0) {
               // The 1st reader can run on this thread; the others will be on thread pool threads
               QueueCallback(m_qReadRequests.Dequeue());
            }
         } else {
            // No requests are queued, free the gate
            m_state = SyncGateStates.Free;
         }
         Monitor.Exit(m_syncLock);
      }

      private static void QueueCallback(SyncGateAsyncResult sgar) {
         ThreadPool.QueueUserWorkItem(InvokeCallback, sgar);
      }
      private static void InvokeCallback(Object o) {
         Contract.Requires(o != null);
         ((SyncGateAsyncResult)o).SetAsCompleted(null, false);
      }
      #endregion
   }
}
#endif

//////////////////////////////// End of File //////////////////////////////////