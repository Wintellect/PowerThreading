/******************************************************************************
Module:  ResourceLockDelegator.cs
Notices: Copyright (c) 2006-2009 by Jeffrey Richter and Wintellect
******************************************************************************/

using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Diagnostics.Contracts;

///////////////////////////////////////////////////////////////////////////////

namespace Wintellect.Threading.ResourceLocks.Diagnostics {
   /// <summary>
   /// An abstract class that delegates a lot of ResourceLock behavior to an inner ResourceLock.
   /// </summary>
   public abstract class ResourceLockDelegator : ResourceLock {
      private readonly ResourceLock m_resLock;

      /// <summary>Constructs a ResourceLockDelegator over the specified ResourceLock.</summary>
      /// <param name="resLock">The inner ResourceLock.</param>
      [ContractVerification(false)]
      protected ResourceLockDelegator(ResourceLock resLock)
         : this(resLock, resLock.ResourceLockOptions) {
            Contract.Requires(resLock != null);
            Contract.Ensures(InnerLock != null);
            Contract.Assert(m_resLock != null);
      }

      /// <summary>Constructs a ResourceLockDelegator over the specified ResourceLock.</summary>
      /// <param name="resLock">The inner ResourceLock.</param>
      /// <param name="resourceLockOptions">Indicates the flags to use with this specified lock.</param>
      [ContractVerification(false)]
      protected ResourceLockDelegator(ResourceLock resLock, ResourceLockOptions resourceLockOptions)
         : base(resourceLockOptions) {
         Contract.Requires(resLock != null);
         Contract.Ensures(InnerLock != null);
         m_resLock = resLock;
#if DEADLOCK_DETECTION
         m_resLock.ImmuneFromDeadlockDetection = true;   // The outerlock is used for deadlock detection; not the inner lock
#endif
         Contract.Assert(m_resLock != null);
      }

      /// <summary>Determines whether the specified Object is equal to the current Object.</summary>
      /// <param name="obj">The Object to compare with the current Object.</param>
      /// <returns>true if the specified Object is equal to the current Object; otherwise, false.</returns>
      public override Boolean Equals(Object obj) {
         return (Object.Equals(m_resLock, ((ResourceLockObserver)obj).m_resLock) && base.Equals(obj));
      }

      /// <summary>Serves as a hash function for a particular type.</summary>
      /// <returns>A hash code for the current Object.</returns>
      public override Int32 GetHashCode() { return m_resLock.GetHashCode(); }

      /// <summary>Returns a reference to the inner ResourceLock object.</summary>
      protected ResourceLock InnerLock { get { return m_resLock; } }

#if false
      [ContractInvariantMethod]
      private void ObjectInvariant() {
         Contract.Invariant(InnerLock != null);
      }
#endif

      /// <summary>Allows the object to clean itself up.</summary>
      /// <param name="disposing">true if Dispose is being called; false if the object is being finalized.</param>
      protected override void Dispose(Boolean disposing) {
         try {
            if (disposing) m_resLock.Dispose();
         }
         finally {
            base.Dispose(disposing);
         }
      }

      /// <summary>Implements the ResourceLock's Enter behavior.</summary>
      protected override void OnEnter(Boolean exclusive) {
         m_resLock.Enter(exclusive);
      }

      /// <summary>Implements the ResourceLock's Leave behavior.</summary>
      protected override void OnLeave(Boolean exclusive) {
         m_resLock.Leave();
      }
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks.Diagnostics {
   /// <summary>
   /// An abstract class that all ResourceLockObserver classes derive from.
   /// </summary>
   public abstract class ResourceLockObserver : ResourceLockDelegator {
      /// <summary>
      /// Constructs a ResourceLockObserver.
      /// </summary>
      /// <param name="resLock"></param>
      protected ResourceLockObserver(ResourceLock resLock)
         : base(resLock) {
            Contract.Requires(resLock != null);
      }
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks.Diagnostics {
   /// <summary>An abstract class that all ResourceLockModifier classes derive from.</summary>
   public abstract class ResourceLockModifier : ResourceLockDelegator {
      /// <summary>Constructs a ResourceLockModifier object.</summary>
      /// <param name="resLock">Identifies the inner ResourceLock object.</param>
      /// <param name="resourceLockOptions">Identifies the flags associted with the innter ResourceLock object.</param>
      protected ResourceLockModifier(ResourceLock resLock, ResourceLockOptions resourceLockOptions)
         : base(resLock, resourceLockOptions) {
            Contract.Requires(resLock != null);
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////