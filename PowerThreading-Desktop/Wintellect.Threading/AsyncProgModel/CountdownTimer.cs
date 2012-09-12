/******************************************************************************
Module:  CountdownTimer.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/

using System;
using System.Threading;
using System.Diagnostics;
using System.Diagnostics.Contracts;

///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// This class offers a timer that implements the asynchronous programming model (APM).
   /// </summary>
   [DebuggerStepThrough]
   public sealed class CountdownTimer : IDisposable {
      private AsyncResult m_asyncResult;
      private Timer m_timer;

      /// <summary>Constructs a new CountdownTimer object.</summary>
      public CountdownTimer() { m_timer = new Timer(CountdownDone); }

      /// <summary>Initiates an asynchronous countdown timer operation.</summary>
      /// <param name="ms">How many milliseconds the countdown timer should wait before firing.</param>
      /// <param name="callback">An optional asynchronous callback, to be called when the timer fires.</param>
      /// <param name="state">A user-provided object that distinguishes this particular asynchronous operation from other operations.</param>
      /// <returns>An IAsyncResult that references the asynchronous countdown operation.</returns>
      public IAsyncResult BeginCountdown(Int32 ms, AsyncCallback callback, Object state) {
         Interlocked.Exchange(ref m_asyncResult, new AsyncResult(callback, state, this));
         Contract.Assume(m_timer != null);
         m_timer.Change(ms, Timeout.Infinite);
         return m_asyncResult;
      }

      private void CountdownDone(Object state) {
         Contract.Requires(m_asyncResult != null);
         m_asyncResult.SetAsCompleted(null, false);
      }

      /// <summary>Returns the result of the asynchronous countdown operation.</summary>
      /// <param name="result">The reference to the pending asynchronous countdown operation.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
      public void EndCountdown(IAsyncResult result) {
         Contract.Requires(result != null);
         ((AsyncResult) result).EndInvoke();
      }

      /// <summary>Releases all resources used by the countdown timer.</summary>
      public void Dispose() {
         if (m_timer == null) return;
         m_timer.Dispose(); 
         m_timer = null;
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////