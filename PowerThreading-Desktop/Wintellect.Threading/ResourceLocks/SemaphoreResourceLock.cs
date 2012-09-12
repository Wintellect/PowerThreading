/******************************************************************************
Module:  SemaphoreResourceLock.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;
using System.Diagnostics;
using Wintellect.Threading.ResourceLocks.Diagnostics;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>Implements a ResourceLock by way of a Windows Semaphore.</summary>
   public sealed class SemaphoreResourceLock : ResourceLock {
      private readonly Semaphore m_lockObj;

      /// <summary>Constructs a SemaphoreResourceLock.</summary>
      public SemaphoreResourceLock()
         : base(ResourceLockOptions.IsMutualExclusive) {
         m_lockObj = new Semaphore(1, 1);
      }

      /// <summary>
      /// Implements the ResourceLock's WaitToWrite behavior.
      /// </summary>
      protected override void OnEnter(Boolean exclusive) {
         m_lockObj.WaitOne();
      }

      /// <summary>
      /// Implements the ResourceLock's DoneWriting behavior.
      /// </summary>
      protected override void OnLeave(Boolean exclusive) {
         m_lockObj.Release();
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////