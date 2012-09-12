/******************************************************************************
Module:  ApmWrap.cs
Notices: Copyright (c) 2010 by Jeffrey Richter and Wintellect
******************************************************************************/

using System;
using System.Threading;
using System.Diagnostics;
using System.Diagnostics.Contracts;


///////////////////////////////////////////////////////////////////////////////

namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// This class contains utility methods to help with the APM.
   /// </summary>
   public static class ApmWrap {
      /// <summary>
      /// Wraps an AsyncCallback with the calling thread's SynchronizationContext so that the
      /// callback method is invoked via posting to the calling thread's SynchronizationContext. 
      /// </summary>
      /// <param name="callback">The callback method to be invoked via the calling thread's SynchronizationContext</param>
      /// <returns>The wrapped callback method</returns>
      public static AsyncCallback SyncContextCallback(AsyncCallback callback) {
         // Capture the calling thread's SynchronizationContext-derived object
         SynchronizationContext sc = SynchronizationContext.Current;
         // If there is no SC, just return what was passed in
         if (sc == null) return callback;
         // Return a delegate that, when invoked, posts to the captured SC a method that
         // calls the original AsyncCallback passing it the IAsyncResult argument
         return asyncResult => sc.Post(result => callback((IAsyncResult)result), asyncResult);
      }
   }
}


namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// This light-weight struct has the ability to associate an arbitrary piece 
   /// of data (of type T) with any IAsyncResult object. When the asynchronous 
   /// operation completes, the associated piece of data can be retrieved to 
   /// complete processing. This struct is typically used when you are 
   /// implementing code that wraps an asynchronous operation and you wish to 
   /// add some context or state of your own to complete the wrapping.
   /// </summary>
   /// <typeparam name="T">The type of the data you wish to embed in 
   /// the IAsyncResult object.</typeparam>
   [DebuggerStepThrough]
   public struct ApmWrap<T> {
      /// <summary>
      /// Returns a value indicating whether this instance and a specified ApmWrap object represent the same value.
      /// </summary>
      /// <param name="value">An ApmWrap object to compare to this instance.</param>
      /// <returns>true if value is equal to this instance; otherwise, false.</returns>
      public Boolean Equals(ApmWrap<T> value) {
         return this.SyncContext.Equals(value.SyncContext);
      }

      /// <summary>
      /// Returns a value indicating whether this instance and a specified ApmWrap object represent the same value.
      /// </summary>
      /// <param name="obj">An ApmWrap object to compare to this instance.</param>
      /// <returns>true if value is equal to this instance; otherwise, false.</returns>
      public override Boolean Equals(Object obj) {
         if (obj is ApmWrap<T>) return Equals((ApmWrap<T>) obj);
         return false;
      }

      /// <summary>
      /// Returns the hash code for this instance.
      /// </summary>
      /// <returns>A 32-bit signed integer hash code.</returns>
      public override Int32 GetHashCode() {
         return base.GetHashCode();
      }

      /// <summary>
      /// /// Returns a value indicating whether two instances of ApmWrap are equal.
      /// </summary>
      /// <param name="obj1">An ApmWrap.</param>
      /// <param name="obj2">An ApmWrap.</param>
      /// <returns>true if obj1 and obj2 are equal; otherwise, false.</returns>
      public static Boolean operator ==(ApmWrap<T> obj1, ApmWrap<T> obj2) {
         return obj1.Equals(obj2);
      }

      /// <summary>
      /// /// Returns a value indicating whether two instances of ApmWrap are not equal.
      /// </summary>
      /// <param name="obj1">An ApmWrap.</param>
      /// <param name="obj2">An ApmWrap.</param>
      /// <returns>true if obj1 and obj2 are not equal; otherwise, true.</returns>
      public static Boolean operator !=(ApmWrap<T> obj1, ApmWrap<T> obj2) {
         return !obj1.Equals(obj2);
      }


      /// <summary>
      /// If non-null when creating an ApmWrap object, the ApmWrap object will 
      /// force the operation to complete using the specified SynchronizationContext. 
      /// /// </summary>
      private SynchronizationContext SyncContext { get; set; }

      /// <summary>
      /// Call this method to create an ApmWrap object around an asynchronous operation.
      /// </summary>
      /// <param name="data">The data to embed in the ApmWrap object.</param>
      /// <param name="callback">The callback method that should be invoked when the operation completes.</param>
      /// <returns>An ApmWrap object's completion method.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1719:ParameterNamesShouldNotMatchMemberNames", MessageId = "1#")]
      public AsyncCallback Callback(T data, AsyncCallback callback) {
         if (callback == null) return null;
         return new ApmWrapper {
            Data = data, AsyncCallback = callback, SyncContext = SyncContext
         }.AsyncCallbackInternal;
      }

      /// <summary>
      /// Call this method to create an ApmWrap object around an asynchronous operation.
      /// </summary>
      /// <param name="data">The data to embed in the ApmWrap object.</param>
      /// <param name="result">The original IAsyncResult object returned from the BeginXxx method.</param>
      /// <returns>An ApmWrap object that contains the originally-returned IAsyncResult object.</returns>
      public IAsyncResult Return(T data, IAsyncResult result) {
         return new ApmWrapper { Data = data, AsyncResult = result };
      }

      /// <summary>
      /// Call this method to unwrap an ApmWrap object to get its embedded data and IAsyncResult.
      /// </summary>
      /// <param name="result">A variable that will receive a reference to the wrapped IAsyncResult object.</param>
      /// <returns>The embedded piece of data passed to the Callback/Return methods.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public T Unwrap(ref IAsyncResult result) {
         Contract.Requires(result != null);
         ApmWrapper apmWrap = (ApmWrapper)result;
         result = apmWrap.AsyncResult;
         return apmWrap.Data;
      }


      [DebuggerStepThrough]
      private sealed class ApmWrapper : IAsyncResult {
         internal T Data { get; set; }
         internal AsyncCallback AsyncCallback { get; set; }
         internal SynchronizationContext SyncContext { get; set; }
         internal IAsyncResult AsyncResult { get; set; }
         internal ApmWrapper() { }

         internal void AsyncCallbackInternal(IAsyncResult result) {
            Contract.Requires(result != null);
            Contract.Requires(AsyncCallback != null);
            this.AsyncResult = result;
            if (SyncContext == null) AsyncCallback(this);
            else SyncContext.Post(PostCallback, this);
         }

         private static void PostCallback(Object state) {
            Contract.Requires(state != null);
            Contract.Requires(((ApmWrapper)state).AsyncCallback != null);
            ApmWrapper apmWrap = (ApmWrapper)state;
            apmWrap.AsyncCallback(apmWrap);
         }

         public Object AsyncState { get { return AsyncResult.AsyncState; } }
         public WaitHandle AsyncWaitHandle { get { return AsyncResult.AsyncWaitHandle; } }
         public Boolean CompletedSynchronously { get { return AsyncResult.CompletedSynchronously; } }
         public Boolean IsCompleted { get { return AsyncResult.IsCompleted; } }
         public override String ToString() { return AsyncResult.ToString(); }
         public override Boolean Equals(object obj) { return AsyncResult.Equals(obj); }
         public override Int32 GetHashCode() { return AsyncResult.GetHashCode(); }
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////
