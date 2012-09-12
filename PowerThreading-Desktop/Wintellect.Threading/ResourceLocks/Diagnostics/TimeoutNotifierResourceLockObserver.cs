/******************************************************************************
Module:  TimeoutNotifierResourceLockObserver.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/

using System;
using System.Threading;
using System.Diagnostics;
using System.Diagnostics.Contracts;

///////////////////////////////////////////////////////////////////////////////

namespace Wintellect.Threading.ResourceLocks.Diagnostics {
   /// <summary>
   /// A ResourceLock-wrapper class that throws an exception if a thread waits 
   /// too long on the inner lock.
   /// </summary>
   [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Notifier")]
   public class TimeoutNotifierResourceLockObserver : ResourceLockObserver {
      private Int64 m_timeout;

      /// <summary>Constructs a TimeoutNotifierResourceLockObserver object.</summary>
      /// <param name="resLock">Indicates the inner ResourceLock.</param>
      /// <param name="timeout">Indicates how long any thread should wait on the inner lock before throwing an exception. This value is in milliseconds.</param>
      public TimeoutNotifierResourceLockObserver(ResourceLock resLock, Int64 timeout)
         : base(resLock) {
            Contract.Requires(resLock != null);
         m_timeout = timeout;
      }

      /// <summary>Constructs a TimeoutNotifierResourceLockObserver object.</summary>
      /// <param name="resLock">Indicates the inner ResourceLock.</param>
      /// <param name="timeout">Indicates how long any thread should wait on the inner lock before throwing an exception.</param>
      public TimeoutNotifierResourceLockObserver(ResourceLock resLock, TimeSpan timeout)
         : this(resLock, (Int64) timeout.TotalMilliseconds) {
         Contract.Requires(resLock != null);
      }

      /// <summary>This method is invoked when a thread has waited too long on a ResourceLock. The default behavior, throws a TimeoutException.</summary>
      /// <param name="stackTrace">The stack trace at the point where the thread waited on the ResourceLock.</param>
      protected virtual void OnTimeout(StackTrace stackTrace) {
         Contract.Requires(stackTrace != null);
         String message = "Timed out while waiting for lock. Stack trace of waiting thread follows:" +
            Environment.NewLine + stackTrace.ToString();
         throw new TimeoutException(message);
      }

      private void OnTimeout(Object stackTrace) {
         Contract.Requires(stackTrace != null);
         OnTimeout((StackTrace)stackTrace);
      }

      /// <summary>Implements the ResourceLock's Enter behavior.</summary>
      protected override void OnEnter(Boolean exclusive) {
         StackTrace st = new System.Diagnostics.StackTrace(0, true);
         using (new Timer(OnTimeout, st, m_timeout, -1)) {
            if (exclusive) InnerLock.Enter(exclusive);
         }
      }

      /// <summary>Implements the ResourceLock's Leave behavior.</summary>
      protected override void OnLeave(Boolean exclusive) {
         InnerLock.Leave();
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////