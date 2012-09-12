/******************************************************************************
Module:  NullResourceLock.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// A ResourceLock that actually performs no locking at all.
   /// </summary>
   public sealed class NullResourceLock : ResourceLock {
      /// <summary>
      /// Constructs an instance of the NullResourceLock.
      /// </summary>
      public NullResourceLock() : base(
#if DEADLOCK_DETECTION
         ResourceLockOptions.ImmuneFromDeadlockDetection | 
#endif
         ResourceLockOptions.SupportsRecursion) { }

      /// <summary>
      /// Implements the ResourceLock's WaitToWrite behavior.
      /// </summary>
      protected override void OnEnter(Boolean exclusive) { }

      /// <summary>
      /// Implements the ResourceLock's DoneWriting behavior.
      /// </summary>
      protected override void OnLeave(Boolean exclusive) { }
   }
}


//////////////////////////////// End of File //////////////////////////////////