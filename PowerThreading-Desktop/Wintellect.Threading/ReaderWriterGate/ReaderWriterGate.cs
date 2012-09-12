/******************************************************************************
Module:  ReaderWriterGate.cs
Notices: Copyright (c) 2006-2011 by Jeffrey Richter and Wintellect
******************************************************************************/


#if INCLUDE_GATES
using System;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ReaderWriterGate {
   using Wintellect.Threading.ResourceLocks;
   using Wintellect.Threading.AsyncProgModel;

   /// <summary>
   /// This class implements a reader/writer lock that never blocks any threads.
   /// </summary>
   [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1724:TypeNamesShouldNotMatchNamespaces")]
   public sealed class ReaderWriterGate : IDisposable {
      private ResourceLock m_syncLock = new MonitorResourceLock();

      /// <summary>
      /// Releases all resources associated with the ReaderWriterGate
      /// </summary>
      public void Dispose() { m_syncLock.Dispose(); }

      private enum ReaderWriterGateStates {
         Free = 0,
         OwnedByReaders = 1,
         OwnedByReadersAndWriterPending = 2,
         OwnedByWriter = 3,
         ReservedForWriter = 4
      }
      private ReaderWriterGateStates m_state = ReaderWriterGateStates.Free;
      private Int32 m_numReaders = 0;

      private Queue<ReaderWriterGateReleaser> m_qWriteRequests = new Queue<ReaderWriterGateReleaser>();
      private Queue<ReaderWriterGateReleaser> m_qReadRequests = new Queue<ReaderWriterGateReleaser>();

      /// <summary>
      /// Constructs a ReaderWriterGate object.
      /// </summary>
      public ReaderWriterGate() : this(false) { }

      /// <summary>
      /// Constructs a ReaderWriterGate object
      /// </summary>
      /// <param name="blockReadersUntilFirstWriteCompletes">Pass true to have readers block until the first writer has created the data that is being protected by the ReaderWriterGate.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily")]
      public ReaderWriterGate(Boolean blockReadersUntilFirstWriteCompletes) {
         m_state = blockReadersUntilFirstWriteCompletes ? ReaderWriterGateStates.ReservedForWriter : ReaderWriterGateStates.Free;
      }

      #region BeginWrite/EndWrite Members
      /// <summary>Initiates an asynchronous write operation.</summary>
      /// <param name="callback">The method that will perform the write operation.</param>
      /// <param name="asyncCallback">An optional asynchronous callback, to be called when the operation completes.</param>
      /// <returns>A System.IAsyncResult that represents the asynchronous operation, which could still be pending.</returns>
      public IAsyncResult BeginWrite(ReaderWriterGateCallback callback, AsyncCallback asyncCallback) {
         return BeginWrite(callback, null, asyncCallback, null);
      }

      /// <summary>Initiates an asynchronous write operation.</summary>
      /// <param name="callback">The method that will perform the write operation.</param>
      /// <param name="state">A value passed to the callback method.</param>
      /// <param name="asyncCallback">An optional asynchronous callback, to be called when the operation completes.</param>
      /// <param name="asyncState">A user-provided object that distinguishes this particular asynchronous operation request from other requests.</param>
      /// <returns>A System.IAsyncResult that represents the asynchronous operation, which could still be pending.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
      public IAsyncResult BeginWrite(ReaderWriterGateCallback callback, Object state,
        AsyncCallback asyncCallback, Object asyncState) {
         AsyncResult<Object> ar = new AsyncResult<Object>(asyncCallback, asyncState);
         ReaderWriterGateReleaser releaser = new ReaderWriterGateReleaser(callback, this, false, state, ar);
         m_syncLock.Enter(true);
         switch (m_state) {
            case ReaderWriterGateStates.Free:             // If Free "RFW -> OBW, invoke, return
            case ReaderWriterGateStates.ReservedForWriter:
               m_state = ReaderWriterGateStates.OwnedByWriter;
               ThreadPool.QueueUserWorkItem(releaser.Invoke);
               break;

            case ReaderWriterGateStates.OwnedByReaders:   // If OBR | OBRAWP -> OBRAWP, queue, return
            case ReaderWriterGateStates.OwnedByReadersAndWriterPending:
               m_state = ReaderWriterGateStates.OwnedByReadersAndWriterPending;
               m_qWriteRequests.Enqueue(releaser);
               break;

            case ReaderWriterGateStates.OwnedByWriter:   // If OBW, queue, return
               m_qWriteRequests.Enqueue(releaser);
               break;
         }
         m_syncLock.Leave();
         return ar;
      }

      /// <summary>Returns the result of the asynchronous operation.</summary>
      /// <param name="result">The reference to the pending asynchronous operation to finish.</param>
      /// <returns>Whatever the write callback method returns.</returns>
      public Object EndWrite(IAsyncResult result) {
         if (result == null) throw new ArgumentNullException("result");
         return ((AsyncResult<Object>)result).EndInvoke();
      }
      #endregion


      #region BeginRead/EndWrite Members
      /// <summary>Initiates an asynchronous read operation.</summary>
      /// <param name="callback">The method that will perform the read operation.</param>
      /// <param name="asyncCallback">An optional asynchronous callback, to be called when the operation completes.</param>
      /// <returns>A System.IAsyncResult that represents the asynchronous operation, which could still be pending.</returns>
      public IAsyncResult BeginRead(ReaderWriterGateCallback callback, AsyncCallback asyncCallback) {
         return BeginRead(callback, null, asyncCallback, null);
      }

      /// <summary>Initiates an asynchronous read operation.</summary>
      /// <param name="callback">The method that will perform the read operation.</param>
      /// <param name="state">A value passed to the callback method.</param>
      /// <param name="asyncCallback">An optional asynchronous callback, to be called when the operation completes.</param>
      /// <param name="asyncState">A user-provided object that distinguishes this particular asynchronous operation request from other requests.</param>
      /// <returns>A System.IAsyncResult that represents the asynchronous operation, which could still be pending.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
      public IAsyncResult BeginRead(ReaderWriterGateCallback callback, Object state,
         AsyncCallback asyncCallback, Object asyncState) {
         AsyncResult<Object> ar = new AsyncResult<Object>(asyncCallback, asyncState);
         ReaderWriterGateReleaser releaser = new ReaderWriterGateReleaser(callback, this, true, state, ar);
         m_syncLock.Enter(true);
         switch (m_state) {
            case ReaderWriterGateStates.Free:   // If Free | OBR -> OBR, NR++, invoke, return
            case ReaderWriterGateStates.OwnedByReaders:
               m_state = ReaderWriterGateStates.OwnedByReaders;
               m_numReaders++;
               ThreadPool.QueueUserWorkItem(releaser.Invoke);
               break;

            case ReaderWriterGateStates.OwnedByWriter:   // If OBW | OBRAWP | RFW, queue, return
            case ReaderWriterGateStates.OwnedByReadersAndWriterPending:
            case ReaderWriterGateStates.ReservedForWriter:
               m_qReadRequests.Enqueue(releaser);
               break;
         }
         m_syncLock.Leave();
         return ar;
      }

      /// <summary>Returns the result of the asynchronous read operation.</summary>
      /// <param name="result">The reference to the pending asynchronous operation to finish.</param>
      /// <returns>Whatever the read callback method returns.</returns>
      public Object EndRead(IAsyncResult result) {
         if (result == null) throw new ArgumentNullException("result");
         return ((AsyncResult<Object>)result).EndInvoke();
      }
      #endregion


      #region Helper Classes and Methods
      internal void Release(Boolean reader) {
         m_syncLock.Enter(true);
         // If writer or last reader, the lock is being freed
         Boolean freeing = reader ? (--m_numReaders == 0) : true;
         if (freeing) {
            // Wake up a writer, or all readers, or set to free
            if (m_qWriteRequests.Count > 0) {
               // A writer is queued, invoke it
               m_state = ReaderWriterGateStates.OwnedByWriter;
               ThreadPool.QueueUserWorkItem(m_qWriteRequests.Dequeue().Invoke);
            } else if (m_qReadRequests.Count > 0) {
               // Reader(s) are queued, invoke all of them
               m_state = ReaderWriterGateStates.OwnedByReaders;
               m_numReaders = m_qReadRequests.Count;
               while (m_qReadRequests.Count > 0) {
                  ThreadPool.QueueUserWorkItem(m_qReadRequests.Dequeue().Invoke);
               }
            } else {
               // No writers or readers, free the gate
               m_state = ReaderWriterGateStates.Free;
            }
         }
         m_syncLock.Leave();
      }
      #endregion
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ReaderWriterGate {
   using Wintellect.Threading.AsyncProgModel;
   using System.Diagnostics.CodeAnalysis;

   /// <summary>An object passed to a read/write callback method that encapsulates the state of the operation.</summary>
   public sealed class ReaderWriterGateReleaser : IDisposable {
      [Flags]
      private enum ReleaserFlags {
         Reader = 0x0000000,
         Writer = 0x0000001,
         Completed = 0x00000002,
      }

      private ReaderWriterGateCallback m_callback;
      private ReaderWriterGate m_gate;
      private ReleaserFlags m_flags;
      private Object m_state;
      private AsyncResult<Object> m_asyncResult;
      private Object m_resultValue;

      internal ReaderWriterGateReleaser(ReaderWriterGateCallback callback, ReaderWriterGate gate,
         Boolean reader, Object state, AsyncResult<Object> ar) {

         m_callback = callback;
         m_gate = gate;
         m_flags = reader ? ReleaserFlags.Reader : ReleaserFlags.Writer;
         m_state = state;
         m_asyncResult = ar;
      }

      /// <summary>Returns the ReaderWriterGate used to initiate the read/write operation.</summary>
      public ReaderWriterGate Gate {
         get { return m_gate; }
      }

      /// <summary>Returns the state that was passed with the read/write operation request.</summary>
      public Object State {
         get { return m_state; }
      }

      /// <summary>Allows the read/write callback method to return a value from EndRead/EndWrite.</summary>
      public Object ResultValue {
         get { return m_resultValue; }
         set { m_resultValue = value; }
      }

      [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
        Justification = "OK because exception will be thrown by EndInvoke.")]
      internal void Invoke(Object o) {
         // Called via ThreadPool.QueueUserWorkItem; argument is ignored
         try {
            Complete(null, m_callback(this), false);
         }
         catch (Exception e) {
            if (!Complete(e, null, false)) throw;
         }
      }

      /// <summary>Releases the ReaderWriterGate to that other read/write operations may start.</summary>
      public void Dispose() { Release(false); }

      /// <summary>Releases the ReaderWriterGate so that other read/write operations may start.</summary>
      public void Release(Boolean completeOnReturn = false) { Complete(null, ResultValue, completeOnReturn); }

      private Boolean Complete(Exception exception, Object resultValue, Boolean completeOnReturn) {
         Boolean success = false;  // Assume this call fails

         if (m_gate != null) {
            // Gate not already released; release it
            Boolean reader = (m_flags & ReleaserFlags.Writer) == 0;
            m_gate.Release(reader);  // Release the gate
            m_gate = null; // Mark as complete so we don't complete again
            success = true;
         }

         // If completeOnReturn is true, then the gate is being released explicitly (via Release) and we should NOT complete the operation

         // If we're returning and we're released the gate, then indicate that the operation is complete
         if (completeOnReturn) { success = true; } else {
            // Else we should complete this operation if we didn't do it already
            if ((m_flags & ReleaserFlags.Completed) == 0) {
               m_flags |= ReleaserFlags.Completed;
               // Signal the completion with the exception or the ResultValue
               if (exception != null) m_asyncResult.SetAsCompleted(exception, false);
               else m_asyncResult.SetAsCompleted(resultValue, false);
               success = true;
            }
         }
         return success;   // This call to complete succeeded
      }
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ReaderWriterGate {
   /// <summary>Identifies the prototype of the method that will do the reading or writing.</summary>
   /// <param name="releaser">An object encapsulating the state of the read/write operation.</param>
   /// <returns>The value to be returned from EndRead/EndWrite.</returns>
   public delegate Object ReaderWriterGateCallback(ReaderWriterGateReleaser releaser);
}
#endif

//////////////////////////////// End of File //////////////////////////////////