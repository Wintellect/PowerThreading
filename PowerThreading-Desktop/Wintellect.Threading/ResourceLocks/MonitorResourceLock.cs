/******************************************************************************
Module:  MonitorResourceLock.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// A ResourceLock implemented using System.Threading.Monitor
   /// </summary>
   public sealed class MonitorResourceLock : ResourceLock {
      private readonly Object m_lock;

      /// <summary>
      /// Constructs an instance on the MonitorResourceLock.
      /// </summary>
      public MonitorResourceLock() 
         : base(ResourceLockOptions.AcquiringThreadMustRelease | ResourceLockOptions.IsMutualExclusive | ResourceLockOptions.SupportsRecursion) { m_lock = this; }

      /// <summary>
      /// Constructs an instance of the MonitorResourceLock using the specified object as the lock itself.
      /// </summary>
      /// <param name="obj"></param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj")]
      public MonitorResourceLock(Object obj) 
         : base(ResourceLockOptions.AcquiringThreadMustRelease | ResourceLockOptions.IsMutualExclusive | ResourceLockOptions.SupportsRecursion) { m_lock = obj; }

      /// <summary>
      /// Implements the ResourceLock's WaitToWrite behavior.
      /// </summary>
      protected override void OnEnter(Boolean exclusive) {
         Monitor.Enter(m_lock);
      }

      /// <summary>
      /// Implements the ResourceLock's DoneWriting behavior.
      /// </summary>
      protected override void OnLeave(Boolean exclusive) {
         Monitor.Exit(m_lock);
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////}
