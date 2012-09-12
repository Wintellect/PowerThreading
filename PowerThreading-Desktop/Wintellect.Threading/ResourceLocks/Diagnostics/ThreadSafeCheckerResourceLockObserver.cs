/******************************************************************************
Module:  ThreadSafeCheckerResourceLockObserver.cs
Notices: Copyright (c) 2006-2009 by Jeffrey Richter and Wintellect
******************************************************************************/

using System;
using System.Threading;
using System.Diagnostics.Contracts;

///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks.Diagnostics {
   /// <summary>
   /// This class performs various sanity checks on a ResourceLock-derived type 
   /// making sure that the lock is performing correctly 
   /// </summary>
   public sealed class ThreadSafeCheckerResourceLockObserver : ResourceLockObserver {
      /// <summary>Constructs a ThreadSafeCheckerResourceLockObserver wrapping the desired ResourceLock.</summary>
      /// <param name="resLock"></param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily")]
      public ThreadSafeCheckerResourceLockObserver(ResourceLock resLock)
         : base(resLock) {
            Contract.Requires(resLock != null);
      }

      // The high bit is on if a writer is writing, the low 31 bits are for # of readers
      private Int32 m_LockState = 0;

      /// <summary>Performs any desired cleanup for this object.</summary>
      /// <param name="disposing">true if Dispose is being called; false if the object is being finalized.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Wintellect.Threading.ResourceLocks.Diagnostics.ThreadSafeCheckerResourceLockObserver.VerifyNoWriters(System.String)"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Wintellect.Threading.ResourceLocks.Diagnostics.ThreadSafeCheckerResourceLockObserver.VerifyNoReaders(System.String)")]
      protected override void Dispose(Boolean disposing) {
         try {
            // When being finalized or disposed, the lock should be free
            VerifyNoReaders("Lock held by readers while being disposed");
            VerifyNoWriters("Lock held by a writer while being disposed");
         }
         finally {
            base.Dispose(disposing);
         }
      }

      ///<summary>Allows the calling thread to acquire the lock for reading.</summary>
      ///<returns>A object that can be used to release the reader lock.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Wintellect.Threading.ResourceLocks.Diagnostics.ThreadSafeCheckerResourceLockObserver.VerifyNoReaders(System.String)"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Wintellect.Threading.ResourceLocks.Diagnostics.ThreadSafeCheckerResourceLockObserver.VerifyNoWriters(System.String)")]
      protected override void OnEnter(Boolean exclusive) {
         if (exclusive) {
            InnerLock.Enter(exclusive);
            VerifyNoWriters("Writing while already writing!");
            VerifyNoReaders("Writing while already reading!");
            InterlockedEx.BitTestAndSet(ref m_LockState, 31);		// Add the writer
         } else {
            InnerLock.Enter(exclusive);
            VerifyNoWriters("Reading while already writing!");	// Sanity check for no writers
            Interlocked.Increment(ref m_LockState);	// Add a reader
         }
      }

      ///<summary>Allows the calling thread to release the reader lock.</summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Wintellect.Threading.ResourceLocks.Diagnostics.ThreadSafeCheckerResourceLockObserver.VerifySomeReaders(System.String)"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Wintellect.Threading.ResourceLocks.Diagnostics.ThreadSafeCheckerResourceLockObserver.VerifyOneWriter(System.String)"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Wintellect.Threading.ResourceLocks.Diagnostics.ThreadSafeCheckerResourceLockObserver.VerifyNoWriters(System.String)"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Globalization", "CA1303:Do not pass literals as localized parameters", MessageId = "Wintellect.Threading.ResourceLocks.Diagnostics.ThreadSafeCheckerResourceLockObserver.VerifyNoReaders(System.String)")]
      protected override void OnLeave(Boolean write) {
         if (write) {
            VerifyOneWriter("Done writing while not writing!");
            VerifyNoReaders("Done writing while already reading!");
            InterlockedEx.BitTestAndReset(ref m_LockState, 31);	// Remove the writer
         } else {
            VerifySomeReaders("Done reading while not reading!");
            VerifyNoWriters("Done reading while already writing!");
            Interlocked.Decrement(ref m_LockState);	// Subtract a reader
         }
         InnerLock.Leave();
      }

      #region Verify Routines
      private void VerifyNoWriters(String message) {
         // There should be no writers
         if ((m_LockState & 0x80000000) != 0)
            ThrowException(message);
      }
      private void VerifyOneWriter(String message) {
         // There should be one writer
         if ((m_LockState & 0x80000000) == 0)
            ThrowException(message);
      }

      private void VerifyNoReaders(String message) {
         // There should be no readers
         if ((m_LockState & 0x7FFFFFFF) != 0)
            ThrowException(message);
      }

      private void VerifySomeReaders(String message) {
         // There should be some readers
         if ((m_LockState & 0x7FFFFFFF) == 0)
            ThrowException(message);
      }

      private static void ThrowException(String message) {
         throw new InvalidOperationException(message);
      }
      #endregion
   }
}


//////////////////////////////// End of File //////////////////////////////////
