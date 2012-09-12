#if false
/******************************************************************************
Module:  ConditionVariable.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;
using System.Diagnostics.Contracts;


///////////////////////////////////////////////////////////////////////////////


// This class allows a ResourceLock to be used with a Condition Variable
namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// Adds condition variable support to a ResourceLock class.
   /// </summary>
   public class ConditionVariable : IDisposable {
      // Contains the number of threads that are paused on the condition variable
      private Int32 m_numPausedThreads = 0;

      // Used to wake up threads paused on the condition variable
//      private Semaphore m_semaphore = new Semaphore(0, Int32.MaxValue);

      /// <summary>
      /// Releases all resources used by the ConditionVariable.
      /// </summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1816:CallGCSuppressFinalizeCorrectly")]
      public void Dispose() { Dispose(true); }

      /// <summary>
      /// Releases all resources associated with the ConditionVariable
      /// </summary>
      /// <param name="disposing"></param>
      protected virtual void Dispose(Boolean disposing) { /* if (disposing) ((IDisposable) m_semaphore).Dispose();*/ }

      /// <summary>
      /// Constructs a ConditionVariable object.
      /// </summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily")]
      public ConditionVariable() {
      }

      /// <summary>
      /// Causes the calling thread to enter a condition variable wait using the specified ResourceLock.
      /// </summary>
      /// <param name="resourceLock">A reference to the ResourceLock object that will be 
      /// temporarily released while waiting for the condition to change.</param>
      public void CVWait(ResourceLock resourceLock) {
         Contract.Requires(resourceLock != null);
         // If lock is held by a writer, reacquire it for writing
         Boolean writing = resourceLock.CurrentWriterCount() > 0;
         CVWait(resourceLock, writing);
      }

      /// <summary>
      /// Causes the calling thread to enter a condition variable wait using the specified ResourceLock.
      /// </summary>
      /// <param name="resourceLock">A reference to the ResourceLock object that will be 
      /// temporarily released while waiting for the condition to change.</param>
      /// <param name="reacquireForWriting">true if the ResourceLock should be reacquired for writing when the condition changes; 
      /// false if the lock should be reacquired for reading when the condition changes.</param>
      public void CVWait(ResourceLock resourceLock, Boolean reacquireForWriting) {
         Contract.Requires(resourceLock != null);
         // We can't wait on a lock that is free; the lock must currently be held
         if (resourceLock.CurrentlyFree())
            throw new InvalidOperationException("Can't wait on free lock.");

         // Indicate that this thread is going to pause
         // This value is "decremented" in Unpause
         Interlocked.Increment(ref m_numPausedThreads);
         //Console.WriteLine("{0}: Inc to {1}", Thread.CurrentThread.ManagedThreadId, m_numPausedThreads);

         AutoResetEvent are = new AutoResetEvent(false);
         m_waitingThreads.AddLast(are);


         // Find out if the lock is held by readers or a writer
         //Boolean reading = resourceLock.CurrentReaderCount() > 0;

         // Release the lock held by this thread
         resourceLock.Leave();

         // Make this thread paused until unpaused
         are.WaitOne(); //m_semaphore.WaitOne();

         // Make this thread regain the lock it used to hold
         resourceLock.Enter(reacquireForWriting);
      }

      System.Collections.Generic.LinkedList<AutoResetEvent> m_waitingThreads = new System.Collections.Generic.LinkedList<AutoResetEvent>();

      /// <summary>
      /// Wakes a single thread that is currently inside a call to CVWait.
      /// </summary>
      public void CVPulseOne() { CVPulse(true); }

      /// <summary>
      /// Wakes all threads that are currently inside a call to CVWait.
      /// </summary>
      public void CVPulseAll() { CVPulse(false); }

      private void CVPulse(Boolean justOne) {
         Int32 count = 0;
         if (justOne) {
            // If count of paused threads is great than zero, unpause one of them
            if (InterlockedEx.DecrementIfGreaterThan(ref m_numPausedThreads, 0) > 0)
               count = 1;
         } else {
            // Get count of paused threads and set to 0
            count = Interlocked.Exchange(ref m_numPausedThreads, 0);
         }
         //Console.WriteLine("{0}: releasing {1}", Thread.CurrentThread.ManagedThreadId, count);
         for (Int32 n = 0; n < count; n++) {
            m_waitingThreads.First.Value.Set();
            m_waitingThreads.RemoveFirst();
         }
         //if (count == 0) return; // No threads were paused, return
         //m_semaphore.Release(count); // Unpause 1 or all threads
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////
#endif