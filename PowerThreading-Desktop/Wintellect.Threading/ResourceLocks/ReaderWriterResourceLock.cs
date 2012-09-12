/******************************************************************************
Module:  ReaderWriterResourceLock.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/

using System;
using System.Threading;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// A reader/write lock implemented using the .NET Framework's own System.Threading.ReaderWriterLock
   /// </summary>
   public sealed class ReaderWriterResourceLock : ResourceLock {
      private readonly ReaderWriterLock m_lockObj = new ReaderWriterLock();

      /// <summary>
      /// Constructs a ReaderWriterResourceLock object.
      /// </summary>
      public ReaderWriterResourceLock() : base(ResourceLockOptions.AcquiringThreadMustRelease | ResourceLockOptions.SupportsRecursion) { }

      /// <summary>
      /// Implements the ResourceLock's WaitToWrite behavior.
      /// </summary>
      protected override void OnEnter(Boolean exclusive) {
         if (exclusive) m_lockObj.AcquireWriterLock(-1);
         else m_lockObj.AcquireReaderLock(-1);
      }

      /// <summary>
      /// Implements the ResourceLock's DoneWriting behavior.
      /// </summary>
      protected override void OnLeave(Boolean exclusive) {
         if (exclusive) m_lockObj.ReleaseWriterLock();
         else m_lockObj.ReleaseReaderLock();
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////}
