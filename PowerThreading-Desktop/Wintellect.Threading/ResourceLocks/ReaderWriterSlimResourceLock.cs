#if false
/******************************************************************************
Module:  ReaderWriterSlimResourceLock.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   // This class is only available when running on .NET 3.5 or later
   /// <summary>
   /// A ResourceLock implemented using System.Threading.ReaderWriterLockSlim
   /// </summary>
   public sealed class ReaderWriterSlimResourceLock : ResourceLock {
      private readonly ReaderWriterLockSlim m_lock;

      /// <summary>
      /// Constructs an instance on the ReaderWriterSlimResourceLock.
      /// </summary>
      public ReaderWriterSlimResourceLock()
         : this(LockRecursionPolicy.NoRecursion) {
      }

      /// <summary>
      /// Constructs an instance on the ReaderWriterSlimResourceLock with the desired recursion policy.
      /// </summary>
      public ReaderWriterSlimResourceLock(LockRecursionPolicy recursionPolicy)
         : base(ResourceLockOptions.AcquiringThreadMustRelease |
         ((recursionPolicy == LockRecursionPolicy.SupportsRecursion) ? ResourceLockOptions.SupportsRecursion : 0)) {
         m_lock = new ReaderWriterLockSlim(recursionPolicy);
      }

      ///<summary>Derived class overrides <c>OnEnter</c> to provide specific lock-acquire semantics.</summary>
      protected override void OnEnter(Boolean exclusive) {
         if (exclusive) m_lock.EnterWriteLock();
         else m_lock.EnterReadLock();
      }

      ///<summary>Derived class overrides <c>OnLeave</c> to provide specific lock-release semantics.</summary>
      protected override void OnLeave(Boolean exclusive) {
         if (exclusive) m_lock.ExitWriteLock();
         else m_lock.ExitReadLock();
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////}
#endif