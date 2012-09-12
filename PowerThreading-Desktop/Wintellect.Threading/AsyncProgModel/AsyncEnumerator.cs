//#define PreserveContext
/******************************************************************************
Module:  AsyncEnumerator.cs
Notices: Copyright (c) 2006-2011 by Jeffrey Richter and Wintellect
******************************************************************************/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;

///////////////////////////////////////////////////////////////////////////////

#if DEBUG
internal sealed class DebuggerStepThroughAttribute : Attribute { }
#endif

///////////////////////////////////////////////////////////////////////////////

namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// A class that can drive an iterator asynchronously allowing for scalable and responsive applications and components.
   /// </summary>
   [ContractVerification(true)]
   [DebuggerStepThrough]
   public partial class AsyncEnumerator {
      #region Static members
      // One delegate for ALL instances of this class
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private static readonly SendOrPostCallback s_syncContextResumeIterator = SyncContextResumeIterator;
      private static void SyncContextResumeIterator(Object asyncEnumerator) {
         Contract.Requires(asyncEnumerator != null);
         // This method calls the desired object's MoveNext method 
         // via the SynchronizationContext's desired thread
         ((AsyncEnumerator)asyncEnumerator).ResumeIterator(ResumeIteratorFlag.CalledFromSyncContextThread);
      }

      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private static readonly WaitCallback s_threadPoolResumeIterator = ThreadPoolResumeIterator;
      private static void ThreadPoolResumeIterator(Object asyncEnumerator) {
         Contract.Requires(asyncEnumerator != null);
         // This method calls the desired object's MoveNext method via a thread pool thread
         ((AsyncEnumerator)asyncEnumerator).ResumeIterator(ResumeIteratorFlag.CalledFromArbitraryThread);
      }

      /// <summary>
      /// Returns the AsyncEnumerator object used to obtain this IAsyncResult object.
      /// </summary>
      /// <param name="result">An IAsyncResult object previously returned 
      /// by calling BeginExecute.</param>
      /// <returns>A reference to the AsyncEnumerator object that was used to call BeginExecute.</returns>
      public static AsyncEnumerator FromAsyncResult(IAsyncResult result) {
         Contract.Requires(result != null);
         AsyncResult ar = (AsyncResult)result;
         return (AsyncEnumerator)ar.InitiatingObject;
      }
      #endregion

      #region Instance fields
#if CANCELLATIONTOKEN
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private CancellationToken m_cancellationToken;

      /// <summary>
      /// Returns the CancellationToken associated with the AsyncEnumerator object when it was constructed.
      /// </summary>
      public CancellationToken CancellationToken { get { return m_cancellationToken; } }
#endif

#if !SILVERLIGHT && !PocketPC
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private Int64 m_beginTime = -1, m_endTime = -1;

      /// <summary>
      /// Returns the time (in ticks) when the BeginExecute method was called or -1 if BeginExecute hasn't been called yet.
      /// </summary>
      public Int64 BeginTime { get { return m_beginTime; } }

      /// <summary>
      /// Returns the time (in ticks) when the iterator method completed its processing or -1 if the iterator hasn't finished its processing yet.
      /// </summary>
      public Int64 EndTime { get { return m_endTime; } }
#endif

      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private String m_debugAETag;

      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      internal String Tag { get { return m_debugAETag; } }

      // Save the SynchronizationContext of the thread calling the constructor.
      // This is used to call OnResumeIterator so that the iterator executes via the right SyncContext
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private SynchronizationContext m_syncContext = SynchronizationContext.Current;

      /// <summary>
      /// Gets or sets the SynchronizationContext object that the AsyncEnumerator uses when resuming your iterator.
      /// All value of null (the default), means that your iterator will execute via various thread pool threads. 
      /// </summary>
      public SynchronizationContext SyncContext {
         get { return m_syncContext; }
         set { m_syncContext = value; }
      }

      // The collection of completed asynchronous operations (IAsyncResult or AsyncOp objects)
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private readonly List<AsyncResultWrapper> m_inbox = new List<AsyncResultWrapper>();

      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode"), DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
      private List<AsyncResultWrapper> OperationsCompleted { get { return m_inbox; } }

      // The Wait and Inbox counters that are atomically manipulated
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private WaitAndInboxCounts m_waitAndInboxCounts = new WaitAndInboxCounts();

      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private Boolean m_throwOnMissingDiscardGroup = false;

      /// <summary>
      /// Sets a flag indicating whether the AsyncEnumerator should throw an exception
      /// if you call a BeginXxx method without calling the End or EndVoid method passing a discard group.
      /// This method exists to allow some runtime checks to help ensure that you are writing your code correctly. 
      /// </summary>
      /// <param name="throw">Pass true to turn checking on; false to turn it off.</param>
      public void ThrowOnMissingDiscardGroup(Boolean @throw) {
         m_throwOnMissingDiscardGroup = @throw;
      }

      // The IEnumerator object that we call MoveNext/Current/Dispose over
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private IEnumerator<Int32> m_enumerator;

      /// <summary>
      /// The IAsyncResult object that allows the iteration to execute asynchronously
      /// </summary>
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      protected AsyncResult m_asyncResult = null;
      #endregion

      #region Constructors
      /// <summary>
      /// Initializes a new instance of the AsyncEnumerator class.
      /// </summary>
      public AsyncEnumerator() {
         m_waitAndInboxCounts.Initialize();
         m_enqueueCompletedOpToInboxDelegate = new AsyncCallback(EnqueueCompletedOpToInbox);

         // If debugging, create debug info collection & add this AE object to in-progress list
         if (IsDebuggingEnabled()) {
            m_debugInfos = new List<DiscardableAsyncResult>();
            UpdateInProgressList(true, this);
         }
      }

      /// <summary>
      /// Initializes a new instance of the AsyncEnumerator class identified with a debug tag.
      /// </summary>
      /// <param name="debugTag">The desired debug tag name for this AsyncEnumerator object.</param>
      public AsyncEnumerator(String debugTag) : this() { m_debugAETag = debugTag; }

#if CANCELLATIONTOKEN
      /// <summary>
      /// Initializes a new instance of the AsyncEnumerator class.
      /// </summary>
      public AsyncEnumerator(CancellationToken cancellationToken = default(CancellationToken), String debugTag = null) {
         // When canceled, execute the code 
         m_cancellationToken = cancellationToken;
         m_cancellationToken.Register(() => {
            // This code executes when the CancellationTokenSource is canceled.
            if (m_waitAndInboxCounts.AtomicCanCancel()) // If iterator is suspended now, resume it
               ResumeIterator(ResumeIteratorFlag.CalledFromArbitraryThread);   // Force iterator to resume
         });

         m_debugAETag = debugTag;
         m_waitAndInboxCounts.Initialize();
         m_enqueueCompletedOpToInboxDelegate =
            new AsyncCallback(EnqueueCompletedOpToInbox);

         // If debugging, create debug info collection & add this AE object to in-progress list
         if (IsDebuggingEnabled()) {
            m_debugInfos = new List<DiscardableAsyncResult>();
            UpdateInProgressList(true, this);
         }
      }
#endif
      #endregion

      #region BeginExecute and EndExecute
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private Int32 m_executionStarted = 0;  // 0=false, 1=true

      /// <summary>
      /// Begins executing an iterator and returns after the iterator's first yield return statement executes.
      /// </summary>
      /// <param name="enumerator">Identifies the iterator method to be driven by the AsyncEnumerator,</param>
      /// <param name="callback">An optional asynchronous callback, to be called when the iterator completes.</param>
      /// <param name="state">A user-provided object that distinguishes this particular asynchronous operation from other operations.</param>
      public IAsyncResult BeginExecute(IEnumerator<Int32> enumerator, AsyncCallback callback, Object state = null) {
         if (Interlocked.CompareExchange(ref m_executionStarted, 1, 0) == 1)
            throw new InvalidOperationException("This object has started executing; use a different object.");

         m_enumerator = enumerator;
         m_asyncResult = OnConstructAsyncResult(callback, state);
#if !SILVERLIGHT && !PocketPC
         m_beginTime = Stopwatch.GetTimestamp();
#endif
         ResumeIterator(ResumeIteratorFlag.FirstCallEver); // Start the enumeration
         return m_asyncResult;
      }

      /*protected*/
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "SyncContext"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "AsyncEnumerator's"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "AsyncEnumerator")]
      internal void TestForSyncContextDeadlock(IAsyncResult result) {
         Contract.Requires(result != null);
         if (result.IsCompleted) return;     // If the operation has completed, then deadlock isn't possible
         if (SyncContext == null) return;    // If there is no SyncContext, deadlock isn't possible
         //if (!Debugger.IsAttached) return;   // If not debugging, just trust that the caller did the right thing

         // If the calling thread's SyncContext doesn't match, then deadlock isn't possible
         if (SyncContext != SynchronizationContext.Current) return;

         switch (SyncContext.GetType().FullName) {
            // I use string compares instead of casts to avoid having my 
            // library take a dependency on the DLLs defining these types.
            case "System.Windows.Forms.WindowsFormsSynchronizationContext":
            case "System.Windows.Threading.DispatcherSynchronizationContext":
               throw new Exception<FrozenUIExceptionArgs>(
                         new FrozenUIExceptionArgs(this),
                         "The UI is frozen because it is waiting for an AsyncEnumerator to complete. " +
                         "However, the AsyncEnumerator can't complete because its SyncContext property " +
                         "requires that the UI thread complete the work. \r\n" +
                         "Either change the AsyncEnumerator's SyncContext property or do not have " +
                         "the UI thread block waiting for the AsyncEnumerator to complete.");
         }
         return;  // All is well
      }

      /// <summary>Waits for the pending asynchronous operation to complete.</summary>
      /// <param name="result">The reference to the pending asynchronous operation to finish.</param>
      public void EndExecute(IAsyncResult result) {
         Contract.Requires(result != null);
         try {
            TestForSyncContextDeadlock(result);
            Contract.Assume(m_asyncResult != null);
            m_asyncResult.EndInvoke(); // If not done, block; else throw exception if necessary
         }
         finally {
            EndExecuteCleanup();
         }
      }

      /*protected*/
      internal void EndExecuteCleanup() {
         // Let the GC cleanup anything we don't need any more.
         // Note: Even though the AE has ended, some operations may still complete and 
         // have to be discarded; Don't null out anything required for discarding
         //m_asyncResult = null;      // NOT OK, because we still want to return one of these from BeginExecute even if we invoked a callback method (ASP.NET ashx required this).
         m_cancelSentinel = null;   // OK, Cancel is no longer valid.

         // The following are OK to null out because the iterator ended.
         m_enumerator = null;
         m_debugOpTag = null;
         m_enqueueCompletedOpToInboxDelegate = null;
         if (m_timer != null) { m_timer.Dispose(); m_timer = null; }

         // There is no need to zero the value type fields.
         // m_discardGroupFlags, m_executionStarted, m_lastYieldReturnTickCount, m_threadsInMoveNext, m_throwOnMissingDiscardGroup

         // The following are NOT OK because ops are still completing: 
         // m_debugInfos, m_syncContext, m_debugAETag
      }

      /// <summary>
      /// Called to construct an AsyncResult object with the specified callback function and state.
      /// </summary>
      /// <param name="callback">An optional asynchronous callback, to be called when the iterator completes.</param>
      /// <param name="state">A user-provided object that distinguishes this particular asynchronous operation from other operations.</param>
      /// <returns>The AsyncResult object.</returns>
      protected virtual AsyncResult OnConstructAsyncResult(AsyncCallback callback, Object state) {
         return new AsyncResult(callback, state, this);
      }

      /// <summary>
      /// Called when the asynchronous operation completes.
      /// </summary>
      protected virtual void OnCompleteAsyncResult() {
         Contract.Requires(m_asyncResult != null);
         m_asyncResult.SetAsCompleted(null, false);
      }
      #endregion

      #region Members called by Iterator
      // Construct the 1 delegate that we need that End returns
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private AsyncCallback m_enqueueCompletedOpToInboxDelegate;

      /// <summary>
      /// Pass this to a BegixXxx method's AsyncCallback argument to have the operation
      /// complete to advance the enumerator. The operation is implicitly part of discard group 0.
      /// </summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "EndVoid")]
      public AsyncCallback End() {
         if (m_throwOnMissingDiscardGroup)
            throw new InvalidOperationException("You must call End or EndVoid passing a discard group.");
         // If not debugging, take fast path and just return our AsyncCallback
         // If debugging, create object that has the debug info in it.
         AsyncCallback cb = (m_debugInfos == null) ? m_enqueueCompletedOpToInboxDelegate
            : GetDiscardableAsyncCallback(c_DefaultDiscardGroup, null, null, m_debugOpTag);
#if PreserveContext
         cb = PreserveContext(cb);
#endif
         return cb;
      }
      #endregion

      #region Internal Infrastructure
#if DEBUG
      private Int32 m_threadsInMoveNext = 0;
#endif

      private enum ResumeIteratorFlag {
         CalledFromSyncContextThread = 0,
         CalledFromArbitraryThread = 1,
         FirstCallEver = 2   // Implies CalledFromArbitraryThread
      }


      [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes")]
      private void ResumeIterator(ResumeIteratorFlag resumeIteratorFlag) {
         // If called from any thread and m_syncContext is null, then 
         // this call is OK and this thread can call the enumerator's MoveNext
         if ((resumeIteratorFlag >= ResumeIteratorFlag.CalledFromArbitraryThread) && m_syncContext != null) {
            // Call this function (MoveNext) using the 
            // initializing thread's synchronization context
            m_syncContext.Post(s_syncContextResumeIterator, this);
            return;
         }

         Boolean iteratorNotInAfterState = false;
         Exception exception = null;
         try {
            // While there are more operations to perform...
            while (true) {
#if DEBUG
               //  Make sure that there is no more than 1 thread in MoveNext at a time
               Contract.Assume(Interlocked.Increment(ref m_threadsInMoveNext) == 1);
#endif
               if (resumeIteratorFlag != ResumeIteratorFlag.FirstCallEver)
                  InvokeSuspendResumeCallback(false);
               else resumeIteratorFlag = ResumeIteratorFlag.CalledFromArbitraryThread;
               Contract.Assume(m_enumerator != null);
               iteratorNotInAfterState = m_enumerator.MoveNext();
               if (iteratorNotInAfterState) InvokeSuspendResumeCallback(true);
#if DEBUG
               Contract.Assume(Interlocked.Decrement(ref m_threadsInMoveNext) == 0);
#endif
               if (!iteratorNotInAfterState) break;    // Exit the while loop if the iterator is done

#if CANCELLATIONTOKEN
               // If canceled, immediately restart the iterator; never wait. The iterator should detect 
               // cancel after a yield return by querying CancellationToken.IsCancellationRequested and it 
               // should then yield break or exit causing this while loop to stop
               if (CancellationToken.IsCancellationRequested) continue;
#else
               // If canceled, immediately restart the iterator; never wait
               // The iterator should detect cancel after a yield return by
               // calling IsCanceled and it should then yield break or exit 
               // causing this while loop to stop
               if (IsCanceled()) continue;
#endif
               // Get the value returned from the enumerator
               if (IsDebuggingEnabled()) m_lastYieldReturnTime = DateTime.Now;
               Contract.Assume(m_enumerator != null);
               UInt16 numberOpsToWaitFor = checked((UInt16)m_enumerator.Current);

               if (numberOpsToWaitFor == 0) {
                  ThreadPool.QueueUserWorkItem(s_threadPoolResumeIterator, this);
                  break;
               }

               // If inbox has less than the number of items requested, keep the iterator suspended
               if (!m_waitAndInboxCounts.AtomicSetWait(numberOpsToWaitFor)) break;

               // Inbox has enough items, loop to resume the iterator
            }
         }
         catch (Exception e) {
            exception = e;
            iteratorNotInAfterState = false;
         }
         finally {
            // The iterator is done (in after state), perform final cleanup
            if (!iteratorNotInAfterState) {
               // If no exception occurred in the iterator, automatically 
               // discard any and all uncompleted operations
               if (exception == null) {
                  try { DiscardAllGroups(); }
                  catch (Exception e) { exception = e; }
               }

               // Execute the iterator's finally code (whether an exception 
               // occurred or not because this iterator code is in a finally block)
               Contract.Assume(m_enumerator != null);
               m_enumerator.Dispose();

               // Reset the debugging information
               m_lastYieldReturnTime = DateTime.MaxValue;
               UpdateInProgressList(false, this);

               // Complete our IAsyncResult indicating that enumeration is done
#if !SILVERLIGHT && !PocketPC
               m_endTime = Stopwatch.GetTimestamp();
#endif
               if (exception != null) m_asyncResult.SetAsCompleted(exception, false);
               else {
                  Contract.Assume(m_asyncResult != null); OnCompleteAsyncResult();
               }
            }
         }
      }
      #endregion

      #region Inbox Methods
      /// <summary>
      /// Called internally when an asynchronous operation completes
      /// </summary>
      /// <param name="asyncResultOrDiscardableAsyncResult">The IAsyncResult (for a non-discardable) or the DiscardableAsyncResult (for a discardable) operation.</param>
      private void EnqueueCompletedOpToInbox(Object asyncResultOrDiscardableAsyncResult) {
         Contract.Requires(m_inbox != null);

         // Try to add this item to the inbox
         if (TryEnqueueCompletedOpToInbox(new AsyncResultWrapper(asyncResultOrDiscardableAsyncResult))) {
            // If successful, add 1 to inbox. If this thread detects that 
            // the inbox has enough items in it; this thread should call MoveNext
            if (m_waitAndInboxCounts.AtomicIncrementInbox()) ResumeIterator(ResumeIteratorFlag.CalledFromArbitraryThread);
         }
      }

      /// <summary>
      /// Rejects a completed DiscardableAsyncResult if its group has been discarded; if not rejected, 
      /// the DiscardableAsyncResult is added to the inbox.
      /// </summary>
      /// <param name="asyncResultWrapper">The completed AsyncResultWrapper</param>
      /// <returns>True if the AsyncResultWrapper was added to the inbox; false if it was rejected.</returns>
      private Boolean TryEnqueueCompletedOpToInbox(AsyncResultWrapper asyncResultWrapper) {
         Contract.Requires(m_inbox != null);
         // Remove the item from the debug information while the lock is held
         if (m_debugInfos != null) {
            Monitor.Enter(m_debugInfos);
            m_debugInfos.Remove(asyncResultWrapper.DiscardableAsyncResult);
            Monitor.Exit(m_debugInfos);
         }

         Boolean enqueued = false;
         Monitor.Enter(m_inbox);
         // Add the item if its discard group hasn't been discarded
         if (!HasGroupBeenDiscarded(asyncResultWrapper.DiscardGroup)) {
            Debug.WriteLine("Appending: " + asyncResultWrapper.ToString());
            m_inbox.Add(asyncResultWrapper);
            enqueued = true;
         }
         Monitor.Exit(m_inbox);

         if (!enqueued) {
            // Reject this result if it is part of a discarded discarded group
            Debug.WriteLine("Rejecting: " + asyncResultWrapper.ToString());
            asyncResultWrapper.SelfComplete(this);
         }
         return enqueued;
      }

      /// <summary>
      /// Dequeues a completed AsyncResultWrapper's IAsyncResult object from the inbox.
      /// </summary>
      /// <returns>The completed DiscardableAsyncResult's IAsyncResult object.</returns>
      public IAsyncResult DequeueAsyncResult() {
         Contract.Assume(m_inbox != null);
         Monitor.Enter(m_inbox);
         Contract.Assume(m_inbox.Count > 0);
         AsyncResultWrapper asyncResultWrapper = m_inbox[0];	// Extract DiscardableAsyncResult objects in FIFO order
         m_inbox.RemoveAt(0);
         Monitor.Exit(m_inbox);
         Debug.WriteLine("Dequeuing: " + asyncResultWrapper.ToString());

         IAsyncResult asyncResult = asyncResultWrapper.AsyncResult;   // Save the IAsyncResult because Insert clears it.
         return asyncResult;  // Returned the saved value
      }

      /// <summary>
      /// Discards previously-completed DiscardableAsyncResult objects from the inbox if their discard group has been discarded
      /// </summary>
      private void DiscardAsyncResultsBelongingToDiscardedGroups() {
         Int32 numRemoved = 0;
         Contract.Assume(m_inbox != null);
         Monitor.Enter(m_inbox);
         for (Int32 n = 0; n < m_inbox.Count; n++) {
            AsyncResultWrapper asyncResultWrapper = m_inbox[n];
            if (HasGroupBeenDiscarded(asyncResultWrapper.DiscardGroup)) {
               Debug.WriteLine("Discarding: " + asyncResultWrapper.ToString());
               m_inbox.RemoveAt(n--);
               numRemoved++;

               // It would be best to SelfComplete outside the lock but more allocations 
               // are required to do this and I don't think it's worth it.
               asyncResultWrapper.SelfComplete(this);
            }
         }
         Monitor.Exit(m_inbox);

         // Update the Inbox counter to reflect the number of discarded items
         m_waitAndInboxCounts.AtomicDecrementInbox(numRemoved);
      }
      #endregion
   }
}


///////////////////////////////////////////////////////////////////////////////


#region Debugging Support
namespace Wintellect.Threading.AsyncProgModel {
   public partial class AsyncEnumerator {
      #region Static Members
      [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
      private static volatile List<AsyncEnumerator> s_InProgressList = null;

      /// <summary>
      /// Returns true if the EnableDebugging method has ever been called.
      /// </summary>
      /// <returns>true if EnableDebugging has ever been called.</returns>
      public static Boolean IsDebuggingEnabled() { return s_InProgressList != null; }

      /// <summary>
      /// Call this method to enable operation debugging support. When enabled, call 
      /// stacks are captured and recorded when End methods are invoked. As operations
      /// complete, the call stacks are discarded. Calling ToString on an AsyncEnumerator
      /// object will shows its tag, the time stamp of the last 'yield return' statement 
      /// executed and the collection of its outstanding operations (tags and line/file 
      /// where the End method was called for it). 
      /// Typically, you'd examine this information in a debugger. Because capturing call 
      /// stacks hurts performance, you should only enable debugging support to help you 
      /// solve a problem related to operations that do not complete.
      /// </summary>
      public static void EnableDebugSupport() {
         Contract.Ensures(s_InProgressList != null);
         // This method is idempotent: once enable, debugging support stays enabled
         if (s_InProgressList != null) return; // Previously enabled, nothing to do
#pragma warning disable 420   // a reference to a volatile field will not be treated as volatile
         Interlocked.CompareExchange(ref s_InProgressList, new List<AsyncEnumerator>(), null);
#pragma warning restore 420
      }

      [ContractInvariantMethod]
      private void ObjectInvariant() {
         Contract.Invariant(!IsDebuggingEnabled() || this.m_debugInfos != null);
      }

      /// <summary>
      /// Returns the AsyncEnumerator objects are are in progress of executing an iterator.
      /// The list returned is sorted. Element 0 identifies the AsyncEnumerator that has been 
      /// waiting the longest for its operations to complete. The last element has been waiting
      /// the shortest amount of time. 
      /// </summary>
      /// <returns>The sorted list of in-progress AsyncEnumerator objects.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "GetInProgressList"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "EnableDebugSupport"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
      public static IList<AsyncEnumerator> GetInProgressList() {
         if (!IsDebuggingEnabled())
            throw new InvalidOperationException("EnableDebugSupport must be called prior to calling GetInProgressList.");

         Monitor.Enter(s_InProgressList);
         AsyncEnumerator[] list = s_InProgressList.ToArray();
         Monitor.Exit(s_InProgressList);

         // Sort these so that the smallest timestamp (oldest) is first
         Array.Sort(list, (ae1, ae2) => {
            var ts = ae1.LastYieldReturnTimestamp - ae2.LastYieldReturnTimestamp;
            return Math.Sign(ts.TotalMilliseconds);
         });
         return list;
      }

      private static void UpdateInProgressList(Boolean add, AsyncEnumerator ae) {
         if (!IsDebuggingEnabled()) return;
         Monitor.Enter(s_InProgressList);
         if (add) s_InProgressList.Add(ae);
         else {
            // There is a chance that we might try to remove an object that 
            // is not in the collection. This can happen if an AE is created, 
            // then EnableDebugging() is called and then the AE exits.
            // Remove will just return false in this case so there is
            // nothing special to do here.
            s_InProgressList.Remove(ae);
         }
         Monitor.Exit(s_InProgressList);
      }
      #endregion

      #region Instance Members
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private DateTime m_lastYieldReturnTime = DateTime.MaxValue;

      /// <summary>
      /// Returns the timestamp at the last 'yield return' statement.
      /// </summary>
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      public DateTime LastYieldReturnTimestamp {
         get {
            return m_lastYieldReturnTime;
         }
      }

      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private String m_debugOpTag;

      /// <summary>
      /// Sets the debug tag name of the next operation that you are initiating as 
      /// indicated by the next call to an End method. This method is marked with 
      /// the [Conditional("AsyncEnumeratorDebug")] attribute.
      /// </summary>
      /// <param name="debugOperationTag">The debug tag name for the next operation you are initiating.</param>
      [Conditional("AsyncEnumeratorDebug")]
      public void SetOperationTag(String debugOperationTag) { m_debugOpTag = debugOperationTag; }

      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private List<DiscardableAsyncResult> m_debugInfos;

      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode"), DebuggerBrowsable(DebuggerBrowsableState.Collapsed)]
      private List<DiscardableAsyncResult> OperationsPending { get { return m_debugInfos; } }

      /// <summary>
      /// Returns debug information about the AsyncEnumerator object. The information includes
      /// the Name (passed in the constructor). If EnableDebugging has been called, then the
      /// last yield timestamp and source code line/file is also displayed.
      /// </summary>
      /// <returns>String containing helpful debugging information.</returns>
      public override string ToString() {
         StringBuilder debugInfo = new StringBuilder();
         debugInfo.Append((m_debugAETag == null) ? "(No tag)" : "Tag=" + m_debugAETag);
         if (!IsDebuggingEnabled()) return debugInfo.ToString();

         if (m_lastYieldReturnTime == DateTime.MaxValue)
            debugInfo.Append(", LastYieldTime=n/a");
         else
            debugInfo.AppendFormat(CultureInfo.InvariantCulture,
               ", LastYieldTime={0:MM/dd/yyyy HH:mm:ss.fff}", LastYieldReturnTimestamp);

         // Add the in-progress operation debug information to this string
         Contract.Assume(m_debugInfos != null);
         Monitor.Enter(m_debugInfos);
         foreach (var info in m_debugInfos)
            debugInfo.Append("\r\n  " + info);  // Compact Fx doesn't have Environment.NewLine
         Monitor.Exit(m_debugInfos);
         return debugInfo.ToString();
      }
      #endregion
   }
}
#endregion


///////////////////////////////////////////////////////////////////////////////


#region Suspend/Resume Support
namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// This class contains data useful when suspending/resuming an iterator.
   /// </summary>
   [DebuggerStepThrough]
   public sealed class SuspendResumeArgs {
      private readonly AsyncEnumerator m_asyncEnumerator;
      private Object m_state;
      internal SuspendResumeArgs(AsyncEnumerator ae) {
         m_asyncEnumerator = ae;
      }
      /// <summary>Returns the AsyncEnumerator object that is being suspended/resumed.</summary>
      public AsyncEnumerator AsyncEnumerator { get { return m_asyncEnumerator; } }

      /// <summary>Arbitrary data that can be set when the AsyncEnumerator is suspending and retrieved when it is resuming.</summary>
      public Object State { get { return m_state; } set { m_state = value; } }
   }
}


namespace Wintellect.Threading.AsyncProgModel {
   public partial class AsyncEnumerator {
      #region Instance Members
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private Action<SuspendResumeArgs> m_suspendCallback = null;

      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private Action<SuspendResumeArgs> m_resumeCallback = null;

      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private SuspendResumeArgs m_suspendResumeArgs = null;

      /// <summary>
      /// Gets or sets a callback method that will be invoked every time the iterator suspends itself by calling "yield return"
      /// If null, then no callback is invoked. The callback is invoked on the thread that executes the iterator's "yield return" statement.
      /// </summary>
      public Action<SuspendResumeArgs> SuspendCallback {
         get { return m_suspendCallback; }
         set { m_suspendCallback = value; }
      }

      /// <summary>
      /// Gets or sets a callback method that will be invoked every time the AsyncEnumerator object resumes executing the iterator.
      /// If null, then no callback is invoked. The callback is invoked on the thread that will execute the code after the iterator's "yield return" statement.
      /// </summary>
      public Action<SuspendResumeArgs> ResumeCallback {
         get { return m_resumeCallback; }
         set { m_resumeCallback = value; }
      }
      #endregion

      private void InvokeSuspendResumeCallback(Boolean suspend) {
         Action<SuspendResumeArgs> cb = suspend ? m_suspendCallback : m_resumeCallback;
         if (cb != null) {
            m_suspendResumeArgs = m_suspendResumeArgs ?? new SuspendResumeArgs(this);
            cb(m_suspendResumeArgs);
         }
      }
   }
}
#endregion


///////////////////////////////////////////////////////////////////////////////


#region Discard Group Support (Core Members)
namespace Wintellect.Threading.AsyncProgModel {
   public partial class AsyncEnumerator {
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private const Int32 c_DefaultDiscardGroup = 0;
#if PocketPC
      /// <summary>
      /// A discard group can be any number from 0 to MaxDiscardGroup.
      /// </summary>
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      public const Int32 MaxDiscardGroup = 31;

      // Flags indicating which discard groups (0 -> 31) to discard
      // NOTE: I want this to be UInt64 but the Interlocked class has no CompareExchange 
      // method that operates on UInt64 (only Int32)
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private Int32 m_discardGroupFlags = 0;   // Initially, no groups are to be discarded
#else
      /// <summary>
      /// A discard group can be any number from 0 to MaxDiscardGroup.
      /// </summary>
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      public const Int32 MaxDiscardGroup = 63;

      // Flags indicating which discard groups (0 -> 63) to discard
      // NOTE: I want this to be UInt64 but the Interlocked class has no CompareExchange 
      // method that operates on UInt64 (only Int64)
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private Int64 m_discardGroupFlags = 0;   // Initially, no groups are to be discarded
#endif

      private Boolean HasGroupBeenDiscarded(Int32 group) {
#if PocketPC
         return ((1U << group) & unchecked((UInt32)m_discardGroupFlags)) != 0;
#else
         return ((1UL << group) & unchecked((UInt64)m_discardGroupFlags)) != 0;
#endif
      }

      private static void VailidateDiscardGroup(Int32 group) {
         if ((group < 0) || (group > MaxDiscardGroup))
#if !SILVERLIGHT && !PocketPC
            throw new ArgumentOutOfRangeException("group", group,
               String.Format(CultureInfo.InvariantCulture,
                  "Discard group must be between 0 and {0}", MaxDiscardGroup));
#else
            throw new ArgumentOutOfRangeException("group");
#endif
      }

      /// <summary>
      /// Discards all operations that are part of the specified discard group from the Inbox.
      /// </summary>
      /// <param name="group">The discard group number (0-MaxDiscardGroup).</param>
      public void DiscardGroup(Int32 group) {
         VailidateDiscardGroup(group);
         // Reject any more results that are part of this discard group from the Inbox
#if PocketPC
         InterlockedEx.Or(ref m_discardGroupFlags, (1 << group));
#else
         InterlockedEx.Or(ref m_discardGroupFlags, (1L << group));
#endif
         // Discard any results from the inbox that belong to any discarded groups
         DiscardAsyncResultsBelongingToDiscardedGroups();
      }

      private void DiscardAllGroups() {
#if PocketPC
         // Reject operations from ALL 32 (0-31) discard groups
         InterlockedEx.Or(ref m_discardGroupFlags, unchecked((Int32)0xFFFFFFFF));
#else
         // Reject operations from ALL 64 (0-63) discard groups
         InterlockedEx.Or(ref m_discardGroupFlags, unchecked((Int64)0xFFFFFFFFFFFFFFFFL));
#endif
         // Discard any results from the inbox that belong to any discarded groups
         DiscardAsyncResultsBelongingToDiscardedGroups();
      }

      /// <summary>
      /// Identifies an EndXxx method that takes an IAsyncResult and returns some result
      /// </summary>
      /// <param name="result">The IAsyncResult of the completion operation.</param>
      /// <returns>The EndXxx method's return value.</returns>
      public delegate Object EndObjectXxx(IAsyncResult result);

      /// <summary>
      /// Pass the result of this method to a BegixXxx method's AsyncCallback argument to have a cancelable operation
      /// complete to advance the enumerator.
      /// </summary>
      /// <param name="group">Identifies an operation sequence number used for cancelation. The number passed must be between 0 and MaxDiscardGroup.</param>
      /// <param name="callback">The EndXxx method that must be called when this canceled operation eventually completes.</param>
      /// <returns>The value that should be passed to a BeginXxx method's AsyncCallback argument.</returns>
      public AsyncCallback End(Int32 group, EndObjectXxx callback) {
         VailidateDiscardGroup(group);
         if (callback == null) throw new ArgumentNullException("callback");
         AsyncCallback cb = GetDiscardableAsyncCallback(group, callback, null, m_debugOpTag);
#if PreserveContext
         cb = PreserveContext(cb);
#endif
         return cb;
      }

      private AsyncCallback GetDiscardableAsyncCallback(Int32 discardGroup,
         EndObjectXxx endObjectXxx, EndVoidXxx endVoidXxx, String debugOpTag) {
         DiscardableAsyncResult dar = new DiscardableAsyncResult(this, discardGroup, endObjectXxx, endVoidXxx, debugOpTag);
         if (m_debugInfos != null) {
            Monitor.Enter(m_debugInfos);
            m_debugInfos.Add(dar);
            Monitor.Exit(m_debugInfos);
         }
         return dar.AsyncCallback;
      }

#if PreserveContext
      private static AsyncCallback PreserveContext(AsyncCallback callback) {
         if (callback == null || ExecutionContext.IsFlowSuppressed()) return callback;
         ExecutionContext context = ExecutionContext.Capture();
         return result => ExecutionContext.Run(context.CreateCopy(), state => callback(result), null);
      }
#endif

      /// <summary>
      /// Identifies an EndXxx method that takes an IAsyncResult and doesn't return a value
      /// </summary>
      /// <param name="result">The IAsyncResult of the completion operation.</param>
      public delegate void EndVoidXxx(IAsyncResult result);

      /// <summary>
      /// Pass the result of this method to a BegixXxx method's AsyncCallback argument to have a cancelable operation
      /// complete to advance the enumerator.
      /// </summary>
      /// <param name="group">Identifies an operation sequence number used for cancelation. The number passed must be between 0 and 63.</param>
      /// <param name="callback">The EndXxx method that must be called when this canceled operation eventually completes.</param>
      /// <returns>The value that should be passed to a BeginXxx method's AsyncCallback argument.</returns>
      public AsyncCallback EndVoid(Int32 group, EndVoidXxx callback) {
         VailidateDiscardGroup(group);
         if (callback == null) throw new ArgumentNullException("callback");
         return GetDiscardableAsyncCallback(group, null, callback, m_debugOpTag);
      }
   }
}
#endregion


///////////////////////////////////////////////////////////////////////////////


#region Discard Group Support (DiscardableAsyncResult)
namespace Wintellect.Threading.AsyncProgModel {
   public partial class AsyncEnumerator {
      /// <summary>
      /// Class that wraps an asynchronous operation and its AsyncCallback method.
      /// </summary>
      [DebuggerStepThrough]
      internal sealed class DiscardableAsyncResult {
         [DebuggerStepThrough]
         private struct DebugOpInfo {
            public String Tag;
            public StackTrace BeginLocation;
            public DebugOpInfo(String tag) {
               Tag = tag ?? String.Empty;
#if !SILVERLIGHT && !PocketPC
               BeginLocation = IsDebuggingEnabled() ? new StackTrace(true) : null;
#else
               BeginLocation = IsDebuggingEnabled() ? new StackTrace() : null;
#endif
            }

            public override String ToString() {
               String debugInfo = String.Empty;
               if (BeginLocation != null) {
                  // We have a stack, try to find the "MoveNext" frame
                  StackFrame frame = null;
                  for (Int32 frameIndex = 0; (frameIndex < BeginLocation.FrameCount) && (frame == null); frameIndex++) {
                     frame = BeginLocation.GetFrame(frameIndex);
                     Contract.Assume(frame != null);
                     MethodBase mb = frame.GetMethod();
                     Contract.Assume(mb != null);
                     if (!String.Equals(mb.Name, "MoveNext", StringComparison.Ordinal)) frame = null;
                  }

#if !SILVERLIGHT && !PocketPC
                  if ((frame != null) && (frame.GetFileLineNumber() != 0) && (frame.GetFileName() != null)) {
                     debugInfo = String.Format(CultureInfo.InvariantCulture, "Line={0} in {1}, ",
                        frame.GetFileLineNumber(), Path.GetFileName(frame.GetFileName()));
                  }
#else
                  if ((frame != null) && (frame.GetFileLineNumber() != 0)) {
                     debugInfo = String.Format(CultureInfo.InvariantCulture, "Line={0}, ", frame.GetFileLineNumber());
                  }
#endif
               }
               return debugInfo;
            }
         }

#if DEBUG
         // For debugging, each operation gets a unique ID to track it
         private static Int32 s_operationId = 0;
         private Int32 m_operationId = Interlocked.Increment(ref s_operationId);
#endif

         // Delegate referring to this object's AsyncCallback method
         // This delegate is created once and lives the lifetime of the object
         private readonly AsyncCallback m_asyncCallback;
         internal AsyncCallback AsyncCallback { get { return m_asyncCallback; } }

         // The AsyncEnumerator that cares when this operation completes
         private AsyncEnumerator m_asyncEnumerator = null;

         // The operation's discard group
         private Int32 m_discardGroup = 0;
         internal Int32 DiscardGroup { get { return m_discardGroup; } }

         // The operation's debug information
         private DebugOpInfo m_debugInfo;

         // The operation's self-complete method
         private EndObjectXxx m_endObjectXxx;  // Identifies an EndXxx method with a non-void return value
         private EndVoidXxx m_endVoidXxx;      // Identifies an EndXxx method with a void return value

         // The IAsyncResult of the completed operation
         private IAsyncResult m_asyncResult;
         internal IAsyncResult AsyncResult { get { return m_asyncResult; } }

         // NOTE: This method assumes that all arguments are valid
         internal DiscardableAsyncResult(AsyncEnumerator asyncEnumerator, Int32 discardGroup,
            EndObjectXxx endObjectXxx, EndVoidXxx endVoidXxx, String debugOpTag) {

            Contract.Assume(asyncEnumerator != null);
            Contract.Assume((0 <= discardGroup) && (discardGroup <= MaxDiscardGroup));
            Contract.Assume((endObjectXxx == null) || (endVoidXxx == null));  // At least 1 of them is null
            Contract.Assume((discardGroup == c_DefaultDiscardGroup) ? true : ((endObjectXxx != null) && (endVoidXxx != null)));  // Both are not null (if not default discard group)

            m_asyncEnumerator = asyncEnumerator;
            m_discardGroup = discardGroup;

            m_endObjectXxx = endObjectXxx;
            m_endVoidXxx = endVoidXxx;

            m_debugInfo = new DebugOpInfo(debugOpTag);
            m_asyncCallback = this.AsyncCallbackMethod;
            Debug.WriteLine("Preparing: " + ToString());
         }

         public override string ToString() {
            StringBuilder debugInfo = new StringBuilder();
#if DEBUG
            // JMR: I commented out the 2 Assumes below because it is possible that both m_endObjectXxx & m_endVoidXxx are null
            // See End() which calls GetDiscardableAsyncCallback
            //Contract.Assume(m_endObjectXxx != null && m_endObjectXxx.Method != null);
            //Contract.Assume(m_endVoidXxx != null && m_endVoidXxx.Method != null);
            debugInfo.AppendFormat(CultureInfo.InvariantCulture,
               "OpId={0}, EndMethod={1}, ", m_operationId,
               (m_endObjectXxx != null) ? m_endObjectXxx.Method.Name :
                  ((m_endVoidXxx != null) ? m_endVoidXxx.Method.Name : "none"));
#endif
            debugInfo.AppendFormat(CultureInfo.InvariantCulture, "OpTag={0}, ", m_debugInfo.Tag ?? "none");
            debugInfo.Append(m_debugInfo.ToString());
            debugInfo.AppendFormat(CultureInfo.InvariantCulture, "DiscardGroup={0}", m_discardGroup);
            return debugInfo.ToString();
         }

         private void AsyncCallbackMethod(IAsyncResult result) {
            Contract.Requires(m_asyncEnumerator != null);
            Contract.Requires(m_asyncEnumerator.m_inbox != null);

            m_asyncResult = result;
            m_asyncEnumerator.EnqueueCompletedOpToInbox(this);
         }

         internal Boolean CanSelfComplete {
            // true if there is 1 EndXxx method callable
            get { return (m_endObjectXxx != null) || (m_endVoidXxx != null); }
         }

         /// <summary>
         /// Called when an DiscardableAsyncResult is being rejected/discarded to ensure that its EndXxx method is invoked
         /// </summary>
         internal void SelfComplete() {
            if (m_endObjectXxx != null) m_endObjectXxx(m_asyncResult);
            else { Contract.Assume(m_endVoidXxx != null); m_endVoidXxx(m_asyncResult); }
         }
      }
   }
}
#endregion


///////////////////////////////////////////////////////////////////////////////


#region Discard Group Support (AsyncResultWrapper)
namespace Wintellect.Threading.AsyncProgModel {
   public partial class AsyncEnumerator {
      /// <summary>
      /// A ValueType that abstracts an IAsyncResult (used for non-discard group operations) 
      /// from a DiscardableAsyncResult (used for discard group operation).
      /// This type makes an IAsyncResult look like a DiscardableAsyncResult object without construction of another object
      /// </summary>
      [DebuggerStepThrough]
      internal struct AsyncResultWrapper {
         // One of these is null and one is not
         private readonly DiscardableAsyncResult m_discardableAsyncResult;
         private readonly IAsyncResult m_asyncResult;

         internal AsyncResultWrapper(Object asyncResultOrDiscardableAsyncResult) {
            m_asyncResult = asyncResultOrDiscardableAsyncResult as IAsyncResult;
            m_discardableAsyncResult = asyncResultOrDiscardableAsyncResult as DiscardableAsyncResult;
            Contract.Assume((m_asyncResult != null) || (m_discardableAsyncResult != null));  // Ensure that 1 field is not null
         }

         internal IAsyncResult AsyncResult {
            get {
               //Contract.Assume(m_discardableAsyncResult != null);
               return (m_asyncResult != null) ? m_asyncResult : m_discardableAsyncResult.AsyncResult;
            }
         }
         internal Int32 DiscardGroup {
            get {
               Contract.Assume((m_asyncResult != null) || (m_discardableAsyncResult != null));
               return (m_asyncResult != null) ? c_DefaultDiscardGroup : m_discardableAsyncResult.DiscardGroup;
            }
         }
         internal DiscardableAsyncResult DiscardableAsyncResult { get { return m_discardableAsyncResult; } }
         internal void SelfComplete(AsyncEnumerator ae) {
            Contract.Requires(ae != null);
            //Contract.Assume(m_discardableAsyncResult != null);
            if ((m_asyncResult != null) || !m_discardableAsyncResult.CanSelfComplete) {
               // We are rejecting/discarding an item that has no way to clean itself up.
               throw new Exception<NoEndMethodCalled>(new NoEndMethodCalled(ae, this.AsyncResult),
                  String.Format(CultureInfo.InvariantCulture,
                     "An asynchronous operation completed with no way to call " +
                     "an EndXxx method potentially leaking resources.\r\n"
                     + "AsyncEnumerator tag={0}, Operation={1}.", ae.Tag, this.ToString()));
            }
            try {
#if !SILVERLIGHT
               Debug.Write("   Self-completing: " + ToString());
#endif
               m_discardableAsyncResult.SelfComplete();
            }
            catch (Exception e) {   /* swallow */
#if !SILVERLIGHT
               Debug.Write(String.Format(CultureInfo.InvariantCulture,
                  "    (swallowed {0}: {1})", e.GetType(), e.Message));
#else
               Exception e2 = e; // To avoid compiler warning of unused variable 'e'
#endif
            }
            Debug.WriteLine(null);
         }

         public override String ToString() {
            //Contract.Assume(m_discardableAsyncResult != null);
            return (m_asyncResult != null) ? m_asyncResult.ToString() : m_discardableAsyncResult.ToString();
         }
      }
   }
}
#endregion


///////////////////////////////////////////////////////////////////////////////


#region Discard Group Support (NoEndMethodCalled ExceptionArgs)
namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// This class is used when throwing an Exception to indicate 
   /// that an operation is being discarded but no EndXxx method 
   /// was specified and so resources are being leaked.
   /// </summary>
   public sealed class NoEndMethodCalled : ExceptionArgs {
      private readonly AsyncEnumerator m_ae;
      private readonly IAsyncResult m_ar;

      /// <summary>Returns the AsyncEnumerator object associated with this exception.</summary>
      public AsyncEnumerator AsyncEnumerator { get { return m_ae; } }

      /// <summary>Returns the IAsyncResult object associated with this exception.</summary>
      public IAsyncResult AsyncResult { get { return m_ar; } }

      internal NoEndMethodCalled(AsyncEnumerator ae, IAsyncResult ar) {
         m_ae = ae;
         m_ar = ar;
      }
   }
}
#endregion


///////////////////////////////////////////////////////////////////////////////


#if !CANCELLATIONTOKEN
#region Cancellation Methods
namespace Wintellect.Threading.AsyncProgModel {
   public partial class AsyncEnumerator {
      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private Timer m_timer;

      private sealed class CancelSentinel {
         private Object m_cancelValue;
         public CancelSentinel(Object cancelValue) {
            m_cancelValue = cancelValue;
         }
         public Object CancelValue { get { return m_cancelValue; } }
      }

      [DebuggerBrowsable(DebuggerBrowsableState.Never)]
      private volatile CancelSentinel m_cancelSentinel = null;

      private void CancelTimer() {
         if (m_timer != null) {  // If timer is still in play, kill it
            Timer timer = Interlocked.Exchange(ref m_timer, null);
            if (timer != null) timer.Dispose();
         }
      }

      /// <summary>
      /// Causes a timer to automatically call Cancel.
      /// </summary>
      /// <param name="timeout">How long to wait before Cancel is called.</param>
      /// <param name="cancelValue">Identifies the cancelValue to be passed to Cancel.</param>
      public void SetCancelTimeout(TimeSpan timeout, Object cancelValue) {
         SetCancelTimeout((Int32)(Int64)timeout.TotalMilliseconds, cancelValue);
      }

      /// <summary>
      /// Causes a timer to automatically call Cancel.
      /// </summary>
      /// <param name="milliseconds">How long to wait before Cancel is called.</param>
      /// <param name="cancelValue">Identifies the cancelValue to be passed to Cancel.</param>
      public void SetCancelTimeout(Int32 milliseconds, Object cancelValue) {
         CancelTimer();
         if (milliseconds != Timeout.Infinite) {
            m_timer = new Timer(TimerExpired, cancelValue, milliseconds, Timeout.Infinite);
         }
      }
      private void TimerExpired(Object cancelValue) { Cancel(cancelValue); }

      /// <summary>
      /// Tells the iterator to Cancel all of its remaining operations.
      /// </summary>
      /// <param name="cancelValue">An arbitrary value that can be examined by the iterator. 
      /// This value is returned by IsCanceled.</param>
      /// <returns>Returns True if the calling thread is the first thread to call Cancel (and sets the cancelValue).</returns>
      public Boolean Cancel(Object cancelValue) {
         CancelTimer();

         CancelSentinel cs = new CancelSentinel(cancelValue);

#pragma warning disable 420   // a reference to a volatile field will not be treated as volatile
         Boolean thisThreadIsCanceling =
            Interlocked.CompareExchange(ref m_cancelSentinel, cs, null) == null;
#pragma warning restore 420

         // If cancel has never been reported, report it; else ignore it
         if (thisThreadIsCanceling) {
            if (m_waitAndInboxCounts.AtomicCanCancel()) // If iterator is suspended now, resume it
               ResumeIterator(ResumeIteratorFlag.CalledFromArbitraryThread);   // Force iterator to resume
         }
         return thisThreadIsCanceling;
      }

      /// <summary>
      /// An iterator calls this to determine if Cancel has been called (possibly by a timer set by a call to SetCancelTimer).
      /// </summary>
      /// <returns>Returns True if Cancel has been called.</returns>
      public Boolean IsCanceled() {
         Object cancelValue;
         return IsCanceled(out cancelValue);
      }

      /// <summary>
      /// An iterator calls this to determine if Cancel has been called (possibly by a timer set by a call to SetCancelTimer).
      /// </summary>
      /// <param name="cancelValue">Returns the cancelValue passed to Cancel.</param>
      /// <returns>Returns True if Cancel has been called.</returns>
      public Boolean IsCanceled(out Object cancelValue) {
         CancelSentinel cs = m_cancelSentinel;
         cancelValue = (cs == null) ? null : cs.CancelValue;
         return (cs != null);
      }
   }
}
#endregion
#endif


///////////////////////////////////////////////////////////////////////////////


#region WaitAndInboxCounts
namespace Wintellect.Threading.AsyncProgModel {
   public partial class AsyncEnumerator {
      /// <summary>
      /// This struct contains a single Int32 member that encapsulates the  
      /// number of items the enumerator is waiting for and the number of 
      /// completed asynchronous operations in the inbox. All members of this type
      /// manipulate the counters atomically.
      /// </summary>
      [DebuggerDisplay("Wait={Wait}, Inbox={Inbox}")]
      [DebuggerStepThrough]
      protected struct WaitAndInboxCounts {
         #region Private Members
         /// <summary>
         /// Sentinel value used to indicate that a thread determined that 
         /// it should call MoveNext again to advance the iterator 
         /// </summary>
         private const UInt16 c_MaxWait = UInt16.MaxValue;

         /// <summary>
         /// High 16 bits=number of inbox items to wait for before calling MoveNext
         /// Low 16 bits=number of items in inbox 
         /// </summary>
         private Int32 m_waitAndInboxCounts;

         /// <summary>
         /// Gets/Sets the number of items the enumerator is waiting for 
         /// </summary>
         private UInt16 Wait {
            get { return unchecked((UInt16)(m_waitAndInboxCounts >> 16)); }
            set { m_waitAndInboxCounts = unchecked((Int32)((value << 16) | Inbox)); }
         }

         /// <summary>
         /// Gets/Sets the number of items in the inbox
         /// </summary>
         private UInt16 Inbox {
            get { return unchecked((UInt16)m_waitAndInboxCounts); }
            set { m_waitAndInboxCounts = unchecked((Int32)((m_waitAndInboxCounts & 0xFFFF0000) | value)); }
         }

         /// <summary>
         /// Constructs a WaitAndInboxCounts instance from an Int32
         /// </summary>
         /// <param name="waic">The Int32 instance.</param>
         private WaitAndInboxCounts(Int32 waic) { m_waitAndInboxCounts = waic; }

         /// <summary>
         /// Converts a WaitAndInboxCounts instance to an Int32
         /// </summary>
         /// <returns>The WaitAndInboxCounts object as an Int32.</returns>
         private Int32 ToInt32() { return m_waitAndInboxCounts; }
         #endregion

         /// <summary>
         /// Initializes the Wait to the sentinel value because we don't want
         /// a thread to MoveNext until the iterator has returned a Wait at least once
         /// </summary>
         internal void Initialize() { Wait = c_MaxWait; }

         /// <summary>
         /// Atomically updates the number of inbox items the enumerator 
         /// wants to wait for and returns the new value.
         /// </summary>
         /// <param name="numberOpsToWaitFor">The number of asynchronous operations that must complete before the enumerator advances.</param>
         /// <returns>Returns true if the calling thread met the requirements necessary to call the enumerator's MoveNext method.</returns>
         internal Boolean AtomicSetWait(UInt16 numberOpsToWaitFor) {
            return InterlockedEx.Morph<Boolean, UInt16>(ref m_waitAndInboxCounts, numberOpsToWaitFor, SetWait);
         }

         [DebuggerStepThrough]
         private static Int32 SetWait(Int32 i, UInt16 numberOpsToWaitFor, out Boolean shouldMoveNext) {
            WaitAndInboxCounts waic = new WaitAndInboxCounts(i);
            waic.Wait = numberOpsToWaitFor;  // Set the number of items to wait for
            shouldMoveNext = (waic.Inbox >= waic.Wait);
            if (shouldMoveNext) {         // Does the inbox contains enough items to MoveNext?
               waic.Inbox -= waic.Wait;   // Subtract the number of items from the inbox
               waic.Wait = c_MaxWait;     // The next wait is indefinite
            }
            return waic.ToInt32();
         }


         /// <summary>
         /// Atomically updates the number of inbox items the enumerator 
         /// wants to wait for and returns the new value. 
         /// </summary>
         /// <returns>Returns true if the calling thread met the requirements necessary to call the enumerator's MoveNext method.</returns>
         internal Boolean AtomicIncrementInbox() {
            return InterlockedEx.Morph<Boolean, Object>(ref m_waitAndInboxCounts, null, IncrementInbox);
         }

         private static Int32 IncrementInbox(Int32 i, Object argument, out Boolean shouldMoveNext) {
            WaitAndInboxCounts waic = new WaitAndInboxCounts(i);
            waic.Inbox++;                 // Add 1 to the inbox count
            shouldMoveNext = (waic.Inbox == waic.Wait);
            if (shouldMoveNext) {         // Does the inbox contain enough items to MoveNext?
               waic.Inbox -= waic.Wait;   // Subtract the number of items from the inbox
               waic.Wait = c_MaxWait;     // The next wait is indefinite
            }
            return waic.ToInt32();
         }

         /// <summary>
         /// Atomically subtracts the number of discarded items from the inbox.
         /// </summary>
         /// <param name="numRemoved">The number of asynchronous operations that were discarded from the inbox.</param>
         internal void AtomicDecrementInbox(Int32 numRemoved) {
            Contract.Requires(numRemoved != Int32.MinValue);
#if PocketPC
            InterlockedEx.Add(ref m_waitAndInboxCounts, -numRemoved);
#else
            Interlocked.Add(ref m_waitAndInboxCounts, -numRemoved);
#endif
         }

         internal Boolean AtomicCanCancel() {
            return InterlockedEx.Morph<Boolean, UInt16>(ref m_waitAndInboxCounts, 0, CanCancel);
         }

         [DebuggerStepThrough]
         private static Int32 CanCancel(Int32 i, UInt16 dummy, out Boolean shouldMoveNext) {
            WaitAndInboxCounts waic = new WaitAndInboxCounts(i);
            shouldMoveNext = (waic.Wait != c_MaxWait);   // if Wait is != c_MaxWait then the iterator is currently suspended (not running)
            if (shouldMoveNext) {      // If the iterator isn't running, we can cancel
               waic.Inbox = 0;         // Make the inbox empty
               waic.Wait = c_MaxWait;  // The next wait is indefinite
            }
            return waic.ToInt32();
         }
      }
   }
}
#endregion


///////////////////////////////////////////////////////////////////////////////


#region AsyncEnumerator<TResult> derived type
namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// A class that can drive an iterator asynchronously allowing for 
   /// scalable and responsive applications and components.
   /// </summary>
   /// <typeparam name="TResult">The type of result that the iterator method will return.</typeparam>
   [DebuggerStepThrough]
   public class AsyncEnumerator<TResult> : AsyncEnumerator {
      #region Static members
      /// <summary>
      /// Returns the AsyncEnumerator object used to obtain this IAsyncResult object.
      /// </summary>
      /// <param name="result">An IAsyncResult object previously returned 
      /// by calling BeginExecute.</param>
      /// <returns>A reference to the AsyncEnumerator object that was used to call BeginExecute.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes")]
      new public static AsyncEnumerator<TResult> FromAsyncResult(IAsyncResult result) {
         Contract.Requires(result != null);
         Contract.Ensures(Contract.Result<AsyncEnumerator<TResult>>() != null);
         AsyncResult ar = (AsyncResult)result;
         AsyncEnumerator<TResult> r = (AsyncEnumerator<TResult>)ar.InitiatingObject;
         Contract.Assume(r != null);
         return r;
      }
      #endregion

      #region Instance fields
      // The iterator's result value
      private TResult m_result = default(TResult);

      /// <summary>
      /// An iterator sets this property to return the value back to code that executed the iterator.
      /// Code that executed the iterator usually gets this value as the return value from Execute or EndExecute. 
      /// </summary>
      public TResult Result {
         get { return m_result; }
         set { m_result = value; }
      }
      #endregion

      #region Constructors
      /// <summary>
      /// Initializes a new instance of the AsyncEnumerator class.
      /// </summary>
      public AsyncEnumerator() { }

      /// <summary>
      /// Initializes a new instance of the AsyncEnumerator class identified with a debug tag.
      /// </summary>
      /// <param name="debugTag">The desired debug tag name for this AsyncEnumerator object.</param>
      public AsyncEnumerator(String debugTag) : base(debugTag) { }
      #endregion

      #region BeginExecute and EndExecute
      /// <summary>
      /// Waits for the pending asynchronous operation to complete.
      /// </summary>
      /// <param name="result">The reference to the pending asynchronous operation to finish.</param>
      /// <returns>The value set in the Result property by the iterator.</returns>
      new public TResult EndExecute(IAsyncResult result) {
         Contract.Requires(result != null);
         try {
            TestForSyncContextDeadlock(result);
            Contract.Assume(m_asyncResult != null);
            // If not done, block; else throw exception if necessary
            return ((AsyncResult<TResult>)m_asyncResult).EndInvoke();
         }
         finally {
            EndExecuteCleanup();
         }
      }

      /// <summary>
      /// Called to construct an AsyncResult object with the specified callback function and state.
      /// </summary>
      /// <param name="callback">An optional asynchronous callback, to be called when the iterator completes.</param>
      /// <param name="state">A user-provided object that distinguishes this particular asynchronous operation from other operations.</param>
      /// <returns>The AsyncResult object.</returns>
      protected sealed override AsyncResult OnConstructAsyncResult(AsyncCallback callback, Object state) {
         return new AsyncResult<TResult>(callback, state, this);
      }

      /// <summary>
      /// Called when the asynchronous operation completes.
      /// </summary>
      protected sealed override void OnCompleteAsyncResult() {
         ((AsyncResult<TResult>)m_asyncResult).SetAsCompleted(m_result, false);
      }
      #endregion
   }
}
#endregion


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// Indicates that a deadlock would occur because a the UI thread is waiting for an AsyncEnumerator 
   /// to complete running but it can't since the UI thread is blocked.
   /// </summary>
#if !SILVERLIGHT
   [Serializable]
#endif
   public sealed class FrozenUIExceptionArgs : ExceptionArgs {
#if !SILVERLIGHT
      [NonSerialized]
#endif
      private readonly AsyncEnumerator m_ae;
      internal FrozenUIExceptionArgs(AsyncEnumerator ae) {
         m_ae = ae;
      }

      /// <summary>
      /// Returns a reference to the AsyncEnumerator that the UI thread is waiting for.
      /// </summary>
      public AsyncEnumerator AsyncEnumerator { get { return m_ae; } }

      /// <summary>Gets a message that describes the current exception.</summary>
      public override string Message {
         get {
            Contract.Assume(m_ae != null);
            return "AsyncEnumerator debug information:\r\n" + m_ae.ToString();
         }
      }
   }
}

//////////////////////////////// End of File //////////////////////////////////