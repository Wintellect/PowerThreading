/******************************************************************************
Module:  SyncContextAsyncCallback.cs
Notices: Copyright (c) 2006-2009 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;
using System.Diagnostics;


///////////////////////////////////////////////////////////////////////////////


#if NO
namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// This class invokes an AsyncCallback delegate via a specific SynchronizationContext object.
   /// </summary>
   [DebuggerStepThrough]
   public sealed class SyncContextAsyncCallback {
      // One delegate for ALL instances of this class
      private static readonly SendOrPostCallback s_SendOrPostCallback =
            new SendOrPostCallback(SendOrPostCallback);

      // One SyncContextAsyncCallback object is created 
      // per callback with the following state:
      private SynchronizationContext m_syncContext;
      private Boolean m_send; // versus Post
      private AsyncCallback m_originalCallback;
      private IAsyncResult m_result;

      /// <summary>
      /// Wraps the calling thread's SynchronizationContext object around the specified AsyncCallback.
      /// </summary>
      /// <param name="callback">The method that should be invoked using 
      /// the calling thread's SynchronizationContext.</param>
      /// <param name="send">true if the AsyncCallback should be invoked via send; false if post.</param>
      /// <returns>The wrapped AsyncCallback delegate.</returns>
      public static AsyncCallback Wrap(AsyncCallback callback, Boolean send) {
         // If no sync context, the just call through the original delegate
         SynchronizationContext syncContext = SynchronizationContext.Current;
         if (syncContext == null) return callback;

         // If there is a synchronization context, then call through it
         // NOTE: A delegate object is constructed here
         return new AsyncCallback(
            (new SyncContextAsyncCallback(syncContext, callback, send)).AsyncCallback);
      }

      /// <summary>
      /// Wraps the calling thread's SynchronizationContext object around the specified AsyncCallback.
      /// </summary>
      /// <param name="callback">The method that should be invoked using 
      /// the calling thread's SynchronizationContext.</param>
      /// <returns>The wrapped AsyncCallback delegate.</returns>
      public static AsyncCallback Wrap(AsyncCallback callback) {
         return Wrap(callback, false);  // Default to Posting
      }

      private SyncContextAsyncCallback(SynchronizationContext syncContext, AsyncCallback callback, Boolean send) {
         m_originalCallback = callback;
         m_syncContext = syncContext;
         m_send = send;
      }

      private void AsyncCallback(IAsyncResult result) {
         m_result = result;
         if (m_send) m_syncContext.Send(s_SendOrPostCallback, this);
         else m_syncContext.Post(s_SendOrPostCallback, this);
      }

      private static void SendOrPostCallback(Object state) {
         SyncContextAsyncCallback scac = (SyncContextAsyncCallback)state;
         scac.m_originalCallback(scac.m_result);
      }
   }
}
#endif

//////////////////////////////// End of File //////////////////////////////////
