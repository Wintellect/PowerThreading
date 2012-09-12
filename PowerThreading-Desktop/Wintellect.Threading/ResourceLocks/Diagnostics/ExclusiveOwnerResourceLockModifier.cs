/******************************************************************************
Module:  ExclusiveOwnerResourceLockModifier.cs
Notices: Copyright (c) 2006-2009 by Jeffrey Richter and Wintellect
******************************************************************************/

using System;
using System.Threading;
using System.Collections.Generic;
using Wintellect;
using System.Diagnostics.Contracts;

///////////////////////////////////////////////////////////////////////////////

namespace Wintellect.Threading.ResourceLocks.Diagnostics {
   internal struct ExclusiveOwnerResourceLockHelper {
      private Int32 m_owningThreadId;
      private readonly IDisposable m_afterWaitDisposer;
      private readonly IDisposable m_afterReleaseDisposer;

      public ExclusiveOwnerResourceLockHelper(ResourceLock resLock) {
         Contract.Requires(resLock != null);
         if (!resLock.IsMutualExclusive)
            throw new ArgumentException("resLock must identify a ResourceLock that is really a mutual-exclusive lock");

         if (resLock.SupportsRecursion)
            throw new ArgumentException("resLock must identify a ResourceLock that does not support recursion");

         m_owningThreadId = 0;

         // C# requires that all fields be assigned to before 'this' is used (when newing the Disposer objects)
         m_afterWaitDisposer = null;
         m_afterReleaseDisposer = null;

         m_afterWaitDisposer = new Disposer(AfterWait);
         m_afterReleaseDisposer = new Disposer(AfterRelease);
      }

      // Call this method just before waiting on the lock.
      public IDisposable BeforeWait() {
         Int32 callingThreadId = Thread.CurrentThread.ManagedThreadId;

         // If the calling thread already owns the lock, we have a deadlock
         if (callingThreadId == m_owningThreadId)
            Environment.FailFast("Calling thread already owns this lock");
         return m_afterWaitDisposer;
      }

      // Call this method just after waiting sucessfully on the lock.
      public void AfterWait() {
         m_owningThreadId = Thread.CurrentThread.ManagedThreadId; // The calling thread is the owner
      }

      public IDisposable BeforeRelease() {
         Int32 callingThreadId = Thread.CurrentThread.ManagedThreadId;
         if (m_owningThreadId != callingThreadId)
            throw new InvalidOperationException("Calling thread doesn't own this lock!");
         return m_afterReleaseDisposer;
      }

      public void AfterRelease() {
         m_owningThreadId = 0;      // The lock is becoming unowned
      }
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks.Diagnostics {
   /// <summary>
   /// Modifies a ResourceLock enforcing that whatever thread acquires the lock must also release the lock.
   /// </summary>
   public class ExclusiveOwnerResourceLockModifier: ResourceLockModifier {

      private ExclusiveOwnerResourceLockHelper m_exclusiveOwner;

      /// <summary>
      /// Constructs an ExclusiveOwnerResourceLockModifier over the specified ResourceLock.
      /// </summary>
      /// <param name="resLock"></param>
      public ExclusiveOwnerResourceLockModifier(ResourceLock resLock)
         : base(resLock, ResourceLockOptions.AcquiringThreadMustRelease | ResourceLockOptions.IsMutualExclusive) {
            Contract.Requires(resLock != null);
         m_exclusiveOwner = new ExclusiveOwnerResourceLockHelper(resLock);
      }

      #region Override of ResourceLock Members
      /// <summary>
      /// Implements the ResourceLock's WaitToWrite behavior.
      /// </summary>
      protected override void OnEnter(Boolean exclusive) {
         if (exclusive) {
            using (m_exclusiveOwner.BeforeWait()) {
               base.OnEnter(exclusive);
            }
         } else {
            using (m_exclusiveOwner.BeforeWait()) {
               base.OnEnter(exclusive);
            }
         }
      }

      /// <summary>
      /// Implements the ResourceLock's DoneWriting behavior.
      /// </summary>
      protected override void OnLeave(Boolean exclusive) {
         if (exclusive) {
            using (m_exclusiveOwner.BeforeRelease()) {
               base.OnLeave(exclusive);
            }
         } else {
            using (m_exclusiveOwner.BeforeRelease()) {
               base.OnLeave(exclusive);
            }
         }
      }
      #endregion
   }
}


//////////////////////////////// End of File //////////////////////////////////
