/******************************************************************************
Module:  AsyncResult.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.IO;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
#if !PocketPC
using System.Runtime.Serialization;
#endif
#if !SILVERLIGHT && !PocketPC
using System.Runtime.Serialization.Formatters.Binary;
#endif

///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// Represents the status of an asynchronous operation that has no return type.
   /// </summary>
   [DebuggerStepThrough]
   public class AsyncResult : IAsyncResult {
      // Fields set at construction which never change while operation is pending
      private readonly AsyncCallback m_AsyncCallback;
      private readonly Object m_AsyncState;
      private readonly Object m_InitiatingObject = null;

      // Field set at construction which do change after operation completes
      private const Int32 c_StatePending = 0;
      private const Int32 c_StateCompletedSynchronously = 1;
      private const Int32 c_StateCompletedAsynchronously = 2;
      private Int32 m_CompletedState = c_StatePending;

      // Field that may or may not get set depending on usage
      private volatile ManualResetEvent m_AsyncWaitHandle;
      private Int32 m_eventSet = 0; // 0=false, 1= true

      // Fields set when operation completes
      private Exception m_exception;

      // Find method to retain Exception's stack trace when caught and rethrown
      // NOTE: GetMethod returns null if method is not available
      private static readonly MethodInfo s_Exception_InternalPreserveStackTrace =
         typeof(Exception).GetMethod("InternalPreserveStackTrace", BindingFlags.Instance | BindingFlags.NonPublic);

      /// <summary>
      /// Constructs an object that identifies an asynchronous operation.
      /// </summary>
      /// <param name="asyncCallback">The method that should be executed when the operation completes.</param>
      /// <param name="state">The object that can be obtained via the AsyncState property.</param>
      public AsyncResult(AsyncCallback asyncCallback, Object state) {
         m_AsyncCallback = asyncCallback;
         m_AsyncState = state;
      }

      /// <summary>
      /// Constructs an object that identifies an asynchronous operation.
      /// </summary>
      /// <param name="asyncCallback">The method that should be executed when the operation completes.</param>
      /// <param name="state">The object that can be obtained via the AsyncState property.</param>
      /// <param name="initiatingObject">Identifies the object that is initiating the asynchronous operation. This object is obtainable via the InitiatingObject property.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "object")]
      public AsyncResult(AsyncCallback asyncCallback, Object state, Object initiatingObject)
         : this(asyncCallback, state) {
         m_InitiatingObject = initiatingObject;
      }

      /// <summary>
      /// Gets the object passed to the constructor to initiate the asynchronous operation.
      /// </summary>
      public Object InitiatingObject { get { return m_InitiatingObject; } }

#if !SILVERLIGHT && !PocketPC
      private static Exception PreserveExceptionStackTrace(Exception exception) {
         if (exception == null) return null;

         // Try the fast/hacky way first: Call Exception's non-public InternalPreserveStackTrace method to do it
         if (s_Exception_InternalPreserveStackTrace != null) {
            try {
               s_Exception_InternalPreserveStackTrace.Invoke(exception, null);
               return exception;
            }
            catch (MethodAccessException) {
               // Method can't be accessed, try serializing/deserializing the exception
            }
         }

         // The hacky way failed: Serialize and deserialize the exception object
         using (MemoryStream ms = new MemoryStream(1000)) {
            // Using CrossAppDomain causes the Exception to retain its stack
            BinaryFormatter formatter = new BinaryFormatter(null, new StreamingContext(StreamingContextStates.CrossAppDomain));
            formatter.Serialize(ms, exception);

            ms.Seek(0, SeekOrigin.Begin);
            return (Exception)formatter.Deserialize(ms);
         }
      }
#endif

      /// <summary>
      /// Call this method to indicate that the asynchronous operation has completed.
      /// </summary>
      /// <param name="exception">If non-null, this argument identifies the exception that occurring while processing the asynchronous operation.</param>
      /// <param name="completedSynchronously">Indicates whether the operation completed synchronously or asynchronously.</param>
      public void SetAsCompleted(Exception exception, Boolean completedSynchronously) {
         // Passing null for exception means no error occurred; this is the common case
#if !SILVERLIGHT && !PocketPC
         m_exception = PreserveExceptionStackTrace(exception);
#else
         m_exception = exception;
#endif

         // The m_CompletedState field MUST be set prior to calling the callback
         Int32 prevState = Interlocked.Exchange(ref m_CompletedState,
            completedSynchronously ? c_StateCompletedSynchronously : c_StateCompletedAsynchronously);
         if (prevState != c_StatePending)
            throw new InvalidOperationException("You can set a result only once");

         // If the event exists and it hasn't been set yet, set it
         ManualResetEvent mre = m_AsyncWaitHandle; // This is a volatile read
         if ((mre != null) && CallingThreadShouldSetTheEvent())
            mre.Set();

         // If a callback method was set, call it
         if (m_AsyncCallback != null) m_AsyncCallback(this);
      }

      /// <summary>
      /// Frees up resources used by the asynchronous operation represented by the IAsyncResult passed.
      /// If the asynchronous operation failed, this method throws the exception.
      /// </summary>
      public void EndInvoke() {
         // This method assumes that only 1 thread calls EndInvoke for this object

         // If the operation isn't done or if the wait handle was created, wait for it
         if (!IsCompleted || (m_AsyncWaitHandle != null))
            AsyncWaitHandle.WaitOne();

         // If the wait handle was created, close it
#pragma warning disable 420
         ManualResetEvent mre = Interlocked.Exchange(ref m_AsyncWaitHandle, null);
#pragma warning restore 420
         if (mre != null) mre.Close();

         // Operation is done: if an exception occurred, throw it
         if (m_exception != null) throw m_exception;
      }

      #region Implementation of IAsyncResult
      /// <summary>
      /// Gets a user-defined object that qualifies or contains information about an asynchronous operation.
      /// </summary>
      public Object AsyncState { get { return m_AsyncState; } }

      /// <summary>
      /// Gets an indication of whether the asynchronous operation completed synchronously.
      /// </summary>
      public Boolean CompletedSynchronously {
         get {
#if PocketPC || SILVERLIGHT   // No Thread.Volatile methods
            Thread.MemoryBarrier();
            return m_CompletedState == c_StateCompletedSynchronously; 
#else
            return Thread.VolatileRead(ref m_CompletedState) == c_StateCompletedSynchronously; 
#endif
         }
      }

      private Boolean CallingThreadShouldSetTheEvent() { return (Interlocked.Exchange(ref m_eventSet, 1) == 0); }

      /// <summary>
      /// Gets a WaitHandle that is used to wait for an asynchronous operation to complete.
      /// </summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
      public WaitHandle AsyncWaitHandle {
         get {
            Contract.Ensures(Contract.Result<WaitHandle>() != null);
            if (m_AsyncWaitHandle == null) {
               ManualResetEvent mre = new ManualResetEvent(false);
#pragma warning disable 420
               if (Interlocked.CompareExchange(ref m_AsyncWaitHandle, mre, null) != null) {
#pragma warning restore 420
                  Contract.Assume(m_AsyncWaitHandle != null);  // Remove when I.CE gets a post-condition contract
                  // Another thread created this object's event; dispose the event we just created
                  mre.Close();
               } else {
                  // This thread created the event. 
                  // If the operation is complete and no other thread set the event, then this thread should set it
                  if (IsCompleted && CallingThreadShouldSetTheEvent()) {
                     //Contract.Assume(m_AsyncWaitHandle != null);  // Remove when I.CE gets a post-condition contract
                     m_AsyncWaitHandle.Set();
                  }
               }
            }
            Contract.Assume(m_AsyncWaitHandle != null);  // Remove when I.CE gets a post-condition contract
            return m_AsyncWaitHandle;
         }
      }

      /// <summary>
      /// Gets an indication whether the asynchronous operation has completed.
      /// </summary>
      public Boolean IsCompleted {
         get {
#if PocketPC || SILVERLIGHT  // No Thread.Volatile methods
            Thread.MemoryBarrier();
            return m_CompletedState != c_StatePending; 
#else
            return Thread.VolatileRead(ref m_CompletedState) != c_StatePending; 
#endif
         }
      }
      #endregion


      #region Helper Members
      private static readonly AsyncCallback s_AsyncCallbackHelper = AsyncCallbackCompleteOpHelperNoReturnValue;

      /// <summary>
      /// Returns a single static delegate to a static method that will invoke the desired AsyncCallback
      /// </summary>
      /// <returns>The single static delegate.</returns>
      protected static AsyncCallback GetAsyncCallbackHelper() { return s_AsyncCallbackHelper; }

      private static WaitCallback s_WaitCallbackHelper = WaitCallbackCompleteOpHelperNoReturnValue;

      /// <summary>
      /// Returns an IAsyncResult for an operations that was queued to the thread pool.
      /// </summary>
      /// <returns>The IAsyncResult.</returns>
      protected IAsyncResult BeginInvokeOnWorkerThread() {
         ThreadPool.QueueUserWorkItem(s_WaitCallbackHelper, this);
         return this;
      }

      // This static method allows us to have just one static delegate 
      // instead of constructing a delegate per instance of this class
      private static void AsyncCallbackCompleteOpHelperNoReturnValue(IAsyncResult otherAsyncResult) {
         Contract.Requires(otherAsyncResult != null);
         AsyncResult ar = (AsyncResult)otherAsyncResult.AsyncState;
         Contract.Assume(ar != null);
         ar.CompleteOpHelper(otherAsyncResult);
      }

      private static void WaitCallbackCompleteOpHelperNoReturnValue(Object o) {
         Contract.Requires(o != null);
         AsyncResult ar = (AsyncResult)o;
         ar.CompleteOpHelper(null);
      }

      [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
         Justification = "OK because exception will be thrown by EndInvoke.")]
      private void CompleteOpHelper(IAsyncResult ar) {
         Exception exception = null;
         try {
            OnCompleteOperation(ar);
         }
         catch (TargetInvocationException e) {
            exception = e.InnerException;
         }
         catch (Exception e) {
            exception = e;
         }
         finally {
            SetAsCompleted(exception, false);
         }
      }

      /// <summary>
      /// Invokes the callback method when the asynchronous operations completes.
      /// </summary>
      /// <param name="result">The IAsyncResult object identifying the asynchronous operation that has completed.</param>
      protected virtual void OnCompleteOperation(IAsyncResult result) { }
      #endregion
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// Represents the status of an asynchronous operation that has a return type of type <typeparamref name="TResult"/>.
   /// </summary>
   /// <typeparam name="TResult">The type of the return value</typeparam>
   [DebuggerStepThrough]
   public class AsyncResult<TResult> : AsyncResult {
      // Field set when operation completes
      private TResult m_result;

      /// <summary>
      /// Constructs an object that identifies an asynchronous operation.
      /// </summary>
      /// <param name="asyncCallback">The method that should be executed wehen the operation completes.</param>
      /// <param name="state">The object that can be obtained via the AsyncState property.</param>
      public AsyncResult(AsyncCallback asyncCallback, Object state)
         : base(asyncCallback, state) {
      }

      /// <summary>
      /// Constructs an object that identifies an asynchronous operation.
      /// </summary>
      /// <param name="asyncCallback">The method that should be executed wehen the operation completes.</param>
      /// <param name="state">The object that can be obtained via the AsyncState property.</param>
      /// <param name="initiatingObject">Identifies the object that is initiating the asynchronous operation. This object is obtainable via the InitiatingObject property.</param>
      public AsyncResult(AsyncCallback asyncCallback, Object state, Object initiatingObject)
         : base(asyncCallback, state, initiatingObject) {
      }

      /// <summary>
      /// Call this method to indicate that the asynchronous operation has completed.
      /// </summary>
      /// <param name="result">Indicates the value calculated by the asynchronous operation.</param>
      /// <param name="completedSynchronously">Indicates whether the operation completed synchronously or asynchronously.</param>
      public void SetAsCompleted(TResult result, Boolean completedSynchronously) {
         m_result = result;
         base.SetAsCompleted(null, completedSynchronously);
      }

      /// <summary>
      /// Frees up resources used by the asynchronous operation represented by the IAsyncResult passed.
      /// If the asynchronous operation failed, this method throws the exception. If the operation suceeded,
      /// this method returns the value calculated by the asynchronous operation.
      /// </summary>
      /// <returns>The value calculated by the asynchronous operation.</returns>
      public new TResult EndInvoke() {
         base.EndInvoke(); // Wait until operation has completed 
         return m_result;  // Return the result (if above didn't throw)
      }

      #region Helper Members
      private static readonly AsyncCallback s_AsyncCallbackHelper = AsyncCallbackCompleteOpHelperWithReturnValue;

      /// <summary>
      /// Returns a single static delegate to a static method that will invoke the desired AsyncCallback
      /// </summary>
      /// <returns>The single static delegate.</returns>
      [SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes",
         Justification = "OK since member is protected")]
      protected new static AsyncCallback GetAsyncCallbackHelper() { return s_AsyncCallbackHelper; }

      private static void AsyncCallbackCompleteOpHelperWithReturnValue(IAsyncResult otherAsyncResult) {
         Contract.Requires(otherAsyncResult != null);
         Contract.Requires(otherAsyncResult.AsyncState != null);
         AsyncResult<TResult> ar = (AsyncResult<TResult>)otherAsyncResult.AsyncState;
         ar.CompleteOpHelper(otherAsyncResult);
      }

      private static WaitCallback s_WaitCallbackHelper = WaitCallbackCompleteOpHelperWithReturnValue;

      /// <summary>
      /// Returns an IAsyncResult for an operations that was queued to the thread pool.
      /// </summary>
      /// <returns>The IAsyncResult.</returns>
      protected new IAsyncResult BeginInvokeOnWorkerThread() {
         ThreadPool.QueueUserWorkItem(s_WaitCallbackHelper, this);
         return this;
      }
      private static void WaitCallbackCompleteOpHelperWithReturnValue(Object o) {
         Contract.Requires(o != null);
         AsyncResult<TResult> ar = (AsyncResult<TResult>)o;
         ar.CompleteOpHelper(null);
      }

      [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
         Justification = "OK because exception will be thrown by EndInvoke.")]
      private void CompleteOpHelper(IAsyncResult ar) {
         TResult result = default(TResult);
         Exception exception = null;
         try {
            result = OnCompleteOperation(ar);
         }
         catch (Exception e) {
            exception = (e is TargetInvocationException) ? e.InnerException : e;
         }
         if (exception == null) SetAsCompleted(result, false);
         else SetAsCompleted(exception, false);
      }

      /// <summary>
      /// Invokes the callback method when the asynchronous operations completes.
      /// </summary>
      /// <param name="result">The IAsyncResult object identifying the asynchronous operation that has completed.</param>
      /// <returns>The value computed by the asynchronous operation.</returns>
      protected new virtual TResult OnCompleteOperation(IAsyncResult result) {
         return default(TResult);
      }
      #endregion
   }
}


///////////////////////////////////////////////////////////////////////////////

#if false
namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// Represents the status of an asynchronous operation that has a return type of type <typeparamref name="TResult"/>.
   /// </summary>
   /// <typeparam name="TResult">The type of the operations computed value.</typeparam>
   [DebuggerStepThrough]
   public class AsyncResultReflection<TResult> : AsyncResult<TResult> {
      private readonly Object m_target;
      private readonly MethodInfo m_methodInfo;
      private readonly Object[] m_args;

      /// <summary>
      /// Constructs an object that identifies an asynchronous operation.
      /// </summary>
      /// <param name="asyncCallback">The method that should be executed wehen the operation completes.</param>
      /// <param name="state">The object that can be obtained via the AsyncState property.</param>
      /// <param name="target">The object whose instance method should be called. Pass null to invoke a static method.</param>
      /// <param name="methodInfo">Identifies the static or instance method that should be invoked when the asynchronous operation completes.</param>
      /// <param name="args">Identifies the arguments that should be passed to the method when the asynchronous operation completes.</param>
      public AsyncResultReflection(AsyncCallback asyncCallback, Object state, Object target, MethodInfo methodInfo, params Object[] args)
         : base(asyncCallback, state) {
         m_target = target;
         m_methodInfo = methodInfo;
         m_args = args;
         BeginInvokeOnWorkerThread();
      }

      /// <summary>
      /// Invokes the target's method passing it the specified arguments when the asynchronous operations completes.
      /// </summary>
      /// <param name="result">The IAsyncResult object identifying the asynchronous operation that has completed.</param>
      /// <returns>The value computed by the asynchronous operation.</returns>
      protected override TResult OnCompleteOperation(IAsyncResult result) {
         return (TResult)m_methodInfo.Invoke(m_target, m_args);
      }
   }
}
#endif


//////////////////////////////// End of File //////////////////////////////////
