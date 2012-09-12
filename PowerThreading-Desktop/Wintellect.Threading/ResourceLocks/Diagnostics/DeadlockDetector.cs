/******************************************************************************
Module:  DeadlockDetector.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Remoting.Messaging;
using Wintellect;
using System.Globalization;
using System.Diagnostics.CodeAnalysis;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks.Diagnostics {
    internal class ThreadBlockInfo {
        private Int32 m_blockedThreadId;    // ID of thread being blocked
        private Int32 m_blockingThreadId;   // ID of thread keeping m_blockedThreadId blocked (if known)
        private Object m_blockObject;       // Object that m_blockedThreadId is blocked on
        private ResourceLockOptionsHelper m_lockFlags; // Flags if m_blockObject is a lock

        public ThreadBlockInfo(Object blockObject, Int32 blockingThreadId) {
            m_blockedThreadId = Thread.CurrentThread.ManagedThreadId;
            m_blockingThreadId = blockingThreadId;
            m_blockObject = blockObject;
            m_lockFlags = new ResourceLockOptionsHelper(ResourceLockOptions.None);
        }

        public ThreadBlockInfo(Object lockObject, ResourceLockOptions lockFlags)
            : this(lockObject, 0) {
            m_lockFlags = new ResourceLockOptionsHelper(lockFlags);
        }

        public ThreadBlockInfo(ResourceLock lockObject, Boolean acquiringExclusively)
            :
             this(lockObject,
                 (lockObject.AcquiringThreadMustRelease ? ResourceLockOptions.AcquiringThreadMustRelease : 0) |
                 (lockObject.SupportsRecursion ? ResourceLockOptions.SupportsRecursion : 0) |
                 (acquiringExclusively ? ResourceLockOptions.IsMutualExclusive : 0)) { }

        public Int32 BlockedThreadId { get { return m_blockedThreadId; } }
        public Int32 BlockingThreadId { get { return m_blockingThreadId; } }
        public Object BlockObject { get { return m_blockObject; } }

        public Boolean AcquiringThreadMustRelease { get { return m_lockFlags.AcquiringThreadMustRelease; } }
        public Boolean SupportsRecursion { get { return m_lockFlags.SupportsRecursion; } }
        public Boolean AcquiredExclusively { get { return m_lockFlags.IsMutualExclusive; } }

        public override string ToString() {
            return String.Format(CultureInfo.InvariantCulture, "BlockedThreadId={0}, BlockObj={1}({2})",
                m_blockedThreadId, m_blockObject, m_blockObject.GetType());
        }
    }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks.Diagnostics {
    /// <summary>
    /// Contains information about a waiting thread.
    /// </summary>
    public class WaitChainInfo {
        private Int32 m_blockedThreadId;    // ID of thread being blocked
        private Int32 m_blockingThreadId;   // ID of thread keeping m_blockedThreadId blocked (if known)
        private Object m_blockObject;       // Object that m_blockedThreadId is blocked on

        [SuppressMessage("Microsoft.Naming", "CA1720:AvoidTypeNamesInParameters")]
        internal WaitChainInfo(Int32 blockedThreadId, Int32 blockingThreadId, Object blockObject) {
            m_blockedThreadId = blockedThreadId;
            m_blockingThreadId = blockingThreadId;
            m_blockObject = blockObject;
        }

        /// <summary>Returns the ID of the blocked thread.</summary>
        public Int32 BlockedThreadId { get { return m_blockedThreadId; } }

        /// <summary>Returns the ID of the blocking thread.</summary>
        public Int32 BlockingThreadId { get { return m_blockingThreadId; } }

        /// <summary>Returns the object that the thread is blocked on.</summary>
        public Object BlockObject { get { return m_blockObject; } }

        /// <summary>
        /// Returns a System.String that represents the current System.Object.
        /// </summary>
        /// <returns>Returns a System.String that represents the current System.Object.</returns>
        public override string ToString() {
            return String.Format(CultureInfo.InvariantCulture,
               "BlockedThreadId={0}, BlockingThreadId={1}, BlockingObj={2}({3})",
                m_blockedThreadId, m_blockingThreadId, m_blockObject, m_blockObject.GetType());
        }
    }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks.Diagnostics {
    /// <summary>Keeps track of what threads are waiting for in order to detect deadlock.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable")]
    public sealed class DeadlockDetector :
#if !SILVERLIGHT
 MarshalByRefObject
#else
      Object
#endif
 {
        // Private object used internally for thread synchronization (we avoid a ResourceLock to prevent recursion)
        private Object m_syncLock = new Object();

        // Set of threads currently in a wait and what they are blocking for
        private List<ThreadBlockInfo> m_BlockedThreads = new List<ThreadBlockInfo>();

        // Set of locks currently owned by threads
        private List<ThreadBlockInfo> m_AcquiredLocks = new List<ThreadBlockInfo>();

        private Disposer m_UnblockDueToLockAcquireDisposer = new Disposer(delegate { Unblock(true); });
        private Disposer m_UnblockDueToThreadReleaseDisposer = new Disposer(delegate { Unblock(false); });

        private const String c_DeadlockDetectorCallContextName = "DeadlockDetector";
        private static DeadlockDetector s_deadlockDetector = InitializeDeadlockDetector();

        private static DeadlockDetector InitializeDeadlockDetector() {
            // Try to find a DeadlockDetector object from CallContext
            DeadlockDetector deadlockDetector = (DeadlockDetector)
               CallContext.LogicalGetData(c_DeadlockDetectorCallContextName);

            // If we find one, then use the one that was created in another AppDomain
            if (deadlockDetector != null) return deadlockDetector;

            // We couldn't get one out of CallContext; let's create a new DeadlockDetector
            deadlockDetector = new DeadlockDetector();
            CallContext.LogicalSetData(c_DeadlockDetectorCallContextName, deadlockDetector);
            return deadlockDetector;
        }

        private DeadlockDetector() { }

#if false
      public static void ForceClear() {
         s_deadlockDetector.ForceClearI();
      }

      private void ForceClearI() {
         Monitor.Enter(m_syncLock);
         m_BlockedThreads.Clear();
         m_AcquiredLocks.Clear();
         Monitor.Exit(m_syncLock);
      }
#endif

        /// <summary>Tells the DeadlockDetector that a thread is about to block waiting for a lock.</summary>
        /// <param name="lockObject">The lock that the thread is about to block on.</param>
        /// <param name="acquiringExclusively">true if the thread is acquiring the lock exclusively.</param>
        /// <returns>An object that tells the DeadlockDetector that the thread is no longer blocked.</returns>
        public static IDisposable BlockForLock(ResourceLock lockObject, Boolean acquiringExclusively) {
            return s_deadlockDetector.BlockForLockI(lockObject, acquiringExclusively);
        }

        /// <summary>Tells the DeadlockDetector that a thread is about to block waiting for a lock.</summary>
        /// <param name="lockObject">The lock that the thread is about to block on.</param>
        /// <param name="resourceLockOptions">Flags representing the behavior of the lock.</param>
        /// <returns>An object that tells the DeadlockDetector that the thread is no longer blocked.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1720:AvoidTypeNamesInParameters")]
        public static IDisposable BlockForLock(Object lockObject, ResourceLockOptions resourceLockOptions) {
            return s_deadlockDetector.BlockForLockI(lockObject, resourceLockOptions);
        }

        /// <summary>Tells the DeadlockDetector that a thread is about to block waiting for another thread to terminate.</summary>
        /// <param name="thread">The thread that the calling thread wants to wait for.</param>
        /// <param name="milliseconds">The amount of time the calling thread is willing to wait.</param>
        /// <returns>true if the thread has terminated; false if the thread has not terminated after the amount of time specified by the milliseconds parameter has elapsed.</returns>
        public static Boolean BlockForJoin(Thread thread, Int32 milliseconds) {
            if (thread == null) throw new ArgumentNullException("thread");
            using (BlockTemporary(thread, thread.ManagedThreadId)) {
                return thread.Join(milliseconds);
            }
        }

        /// <summary></summary>
        /// <param name="blockObject"></param>
        /// <param name="blockingThreadId"></param>
        /// <returns>An object that tells the DeadlockDetector that the thread is no longer blocked.</returns>
        [SuppressMessage("Microsoft.Naming", "CA1720:AvoidTypeNamesInParameters")]
        public static IDisposable BlockTemporary(Object blockObject, Int32 blockingThreadId) {
            return s_deadlockDetector.BlockTemporaryI(blockObject, blockingThreadId);
        }

        /// <summary></summary>
        /// <param name="targetThreadId">The ID of thread to sart with while building the wait chain.</param>
        /// <returns>The collection of WaitChainInfo objects making up the wait chain.</returns>
        public static IList<WaitChainInfo> GetWaitChain(Int32 targetThreadId) {
            return s_deadlockDetector.GetWaitChainI(targetThreadId);
        }

        /// <summary>Tells the DeadlockDetector that a thread is no longer blocked.</summary>
        /// <param name="lockAcquired">true if the thread acquired the lock it was waiting for.</param>
        public static void Unblock(Boolean lockAcquired) {
            s_deadlockDetector.UnblockI(lockAcquired);
        }

        /// <summary>Tells the DeadlockDetector that a thread is releasing a lock that it currently owns.</summary>
        /// <param name="lockObject">The lock that is being released.</param>
        [SuppressMessage("Microsoft.Naming", "CA1720:AvoidTypeNamesInParameters")]
        public static void ReleaseLock(Object lockObject) {
            s_deadlockDetector.ReleaseLockI(lockObject);
        }

        // Called by a thread when it is about to wait on a lock
        private IDisposable BlockForLockI(ResourceLock lockObject, Boolean acquiringExclusively) {
            // Some ResourceLock-derived types are ignored (like NullResourceLock)
            if (lockObject.ImmuneFromDeadlockDetection) return null;

            // Create record indicating what the calling thread is waiting on and add to waiting threads list
            BlockThreadAndCheckDeadlock(new ThreadBlockInfo(lockObject, acquiringExclusively));
            return m_UnblockDueToLockAcquireDisposer;
        }

        // Called by a thread when it is about to wait on a lock
        private IDisposable BlockForLockI(Object lockObject, ResourceLockOptions resourceLockOptions) {
            // Create record indicating what the calling thread is waiting on and add to waiting threads list
            BlockThreadAndCheckDeadlock(new ThreadBlockInfo(lockObject, resourceLockOptions));
            return m_UnblockDueToLockAcquireDisposer;
        }

        // Called by a thread when it is about to block temporarily without acquiring a lock
        private IDisposable BlockTemporaryI(Object blockObject, Int32 blockingThreadId) {
            // Create record indicating what the calling thread is waiting on and add to waiting threads list
            BlockThreadAndCheckDeadlock(new ThreadBlockInfo(blockObject, blockingThreadId));
            return m_UnblockDueToThreadReleaseDisposer;
        }

        private void BlockThreadAndCheckDeadlock(ThreadBlockInfo tli) {
            try {
                Monitor.Enter(m_syncLock);
                m_BlockedThreads.Add(tli);
                Monitor.Exit(m_syncLock);

                // Check for deadlock (wait chain result is thrown away)
                GetWaitChainI(Thread.CurrentThread.ManagedThreadId);
            }
            catch {
                // If anything goes wrong (most-likely a DeadlockException),
                // cleanup by indicating that this thread did not block because it 
                // will not acquire the lock
                UnblockI(false);
                throw;
            }
        }

        // Called by any thread to check the wait chain and deadlock state of another thread (or itself)
        private IList<WaitChainInfo> GetWaitChainI(Int32 targetThreadId) {
            // Initialize the wait chain result
            List<WaitChainInfo> waitChain = new List<WaitChainInfo>();

            lock (m_syncLock) {
                // Walk the wait chain
                for (Int32 blockedThreadId = targetThreadId; true; ) {
                    // Determine if the thread in question is blocked and what it is blocked on.
                    ThreadBlockInfo blockedThreadInfo = GetBlockedThreadInfo(blockedThreadId);

                    // If the thread is not in a wait state, just return
                    if (blockedThreadInfo == null) return waitChain;

                    // Do we know what thread this thread is blocked on?
                    Int32 blockingThreadId = blockedThreadInfo.BlockingThreadId;

                    if (blockingThreadId == 0) {
                        // We don't know, try to detect it
                        ThreadBlockInfo blockingThreadInfo = GetBlockingThreadInfo(
                           blockedThreadInfo.BlockObject, blockedThreadInfo.AcquiredExclusively);

                        if (blockingThreadInfo != null) blockingThreadId = blockingThreadInfo.BlockedThreadId;
                    }

                    // Add the thread that is currently blocked to the wait chain
                    waitChain.Add(new WaitChainInfo(blockedThreadId, blockingThreadId, blockedThreadInfo.BlockObject));


                    // If no thread is blocking the blocked thread, there is no deadlock
                    if (blockingThreadId == 0) return waitChain;

                    // If the owning thread is the original target thread, then we have a deadlock situation
                    if (blockingThreadId == targetThreadId)
                        throw new Exception<DeadlockExceptionArgs>(
                                  new DeadlockExceptionArgs(waitChain),
                                  "Deadlock");

                    // Traverse what this thread is blocked on...
                    blockedThreadId = blockingThreadId;
                }
            }
        }

        private ThreadBlockInfo GetBlockedThreadInfo(Int32 threadId) {
            return m_BlockedThreads.Find(delegate(ThreadBlockInfo tli) {
                return (tli.BlockedThreadId == threadId);
            });
        }

        private ThreadBlockInfo GetBlockingThreadInfo(Object blockingLockObject, Boolean acquiredExclusively) {
            Int32 callingThreadId = Thread.CurrentThread.ManagedThreadId;

            // Find the thread that currently owns (has acquired) the passed-in lock
            return m_AcquiredLocks.Find(delegate(ThreadBlockInfo tli) {
                // If the acquired lock doesn't match the blocking lock, then the lock is not blocking
                if (tli.BlockObject != blockingLockObject) return false;

                // If the owned lock supports recursion and if the lock was acquired exclusively 
                // and the calling thread is acquiring exclusively and the calling thread 
                // currently owns this recursive lock, then this lock is not blocking
                if (tli.SupportsRecursion &&
                   tli.AcquiredExclusively && acquiredExclusively &&
                   (tli.BlockedThreadId == callingThreadId)) return false;

                // If the owned lock supports recursion and if the lock was acquired shared 
                // and the calling thread is acquiring shared and the calling thread 
                // currently owns this recursive lock, then this lock is not blocking
                if (tli.SupportsRecursion &&
                   !tli.AcquiredExclusively && !acquiredExclusively &&
                   (tli.BlockedThreadId == callingThreadId)) return false;

                // If the owned lock was acquired for exclusive access, then the lock is blocking
                if (tli.AcquiredExclusively) return true;

                // NOTE: The owned lock was acquired for shared access
                // If caller wants exclusive access, then we have a match else no match
                return acquiredExclusively ? true : false;
            });
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "WaitForLock"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA2204:Literals should be spelled correctly", MessageId = "WaitSucceeded")]
        private void UnblockI(Boolean lockAcquired) {
            // The calling thread is no longer waiting on this lock; it owns it.
            lock (m_syncLock) {
                Int32 threadId = Thread.CurrentThread.ManagedThreadId;

                // Find the record indicating the lock that the calling thread was waiting on
                Int32 index = m_BlockedThreads.FindIndex(
                   delegate(ThreadBlockInfo tli) { return tli.BlockedThreadId == threadId; });
                if (index == -1)
                    throw new InvalidOperationException("This thread must call WaitForLock prior to calling WaitSucceeded");

                // If wait succeeded, add this lock to the set of acquired locks
                if (lockAcquired) m_AcquiredLocks.Add(m_BlockedThreads[index]);

                // This thread is no longer waiting on this lock (it succeeded or failed to get it)
                m_BlockedThreads.RemoveAt(index);
            }
        }

        private void ReleaseLockI(Object lockObject) {
            // Some ResourceLock-derived types are ignored (like NullResourceLock)
            ResourceLock rl = lockObject as ResourceLock;
            if ((rl != null) && rl.ImmuneFromDeadlockDetection) return;

            Int32 threadId = Thread.CurrentThread.ManagedThreadId;

            // The calling thread no longer owns this lock.
            lock (m_syncLock) {
                // Find the record representing the acquired lock being released
                Int32 index = m_AcquiredLocks.FindIndex(delegate(ThreadBlockInfo tli) {
                    // If the lock doesn't have to be released by the owning thread, we have a match
                    if (!tli.AcquiringThreadMustRelease) return true;

                    // Lock must be released by owning thread; we have a match if owning thread Id matches releasing thread Id
                    return (tli.BlockedThreadId == threadId);
                });

                // If not found, then we have an error
                if (index == -1)
                    throw new InvalidOperationException("This thread is trying to release a lock that is not owned");

                // Sanity check: Is the lock is a thread-owned lock, ensure that the thread releasing it is the owner
                if (m_AcquiredLocks[index].AcquiringThreadMustRelease && (m_AcquiredLocks[index].BlockedThreadId != Thread.CurrentThread.ManagedThreadId))
                    throw new InvalidOperationException("This thread is trying to release a thread-owned lock that is owned by another thread");

                m_AcquiredLocks.RemoveAt(index);
            }
        }
    }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks.Diagnostics {
    /// <summary>Indicates that a deadlock has been detected.</summary>
#if !SILVERLIGHT
    [Serializable]
#endif
    public sealed class DeadlockExceptionArgs : ExceptionArgs {
        private IList<WaitChainInfo> m_waitChain;

        /// <summary>Returns the collection of WaitChainInfo objects that make up the wait chain.</summary>
        public IList<WaitChainInfo> WaitChain { get { return m_waitChain; } }

        internal DeadlockExceptionArgs(IList<WaitChainInfo> waitChain) {
            m_waitChain = waitChain;
        }

        /// <summary>
        /// Returns a System.String that represents the current System.Object.
        /// </summary>
        /// <returns>Returns a System.String that represents the current System.Object.</returns>
        public override string ToString() {
            if (m_waitChain == null) return null;

            // Append the wait chain text to the message
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("   Wait Chain:");
            foreach (WaitChainInfo wci in m_waitChain)
                sb.AppendLine("     " + wci.ToString());
            return sb.ToString();
        }
    }
}


//////////////////////////////// End of File //////////////////////////////////}
