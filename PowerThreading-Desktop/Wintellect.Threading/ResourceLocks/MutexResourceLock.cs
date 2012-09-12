/******************************************************************************
Module:  MutexResourceLock.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// This class implements a ResourceLock by way of a Windows Mutex.
   /// </summary>
   public sealed class MutexResourceLock : ResourceLock {
      private readonly Mutex m_lockObj;

      /// <summary>
      /// Constructs a MutexResourceLock.
      /// </summary>
      public MutexResourceLock() : this(false) { }

      /// <summary>
      /// Constructs a MutexResourceLock.
      /// </summary>
      /// <param name="initiallyOwned">true if the calling thread should own the mutex; false if the mutex should be unowned.</param>
      public MutexResourceLock(Boolean initiallyOwned)
         : base(ResourceLockOptions.AcquiringThreadMustRelease | ResourceLockOptions.IsMutualExclusive | ResourceLockOptions.SupportsRecursion) {
         m_lockObj = new Mutex(initiallyOwned);
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
      protected override void OnLeave(Boolean write) {
         m_lockObj.ReleaseMutex();
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////}
