/******************************************************************************
Module:  ResourceLock.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Threading;
//using Wintellect.Threading.ResourceLocks.Diagnostics;

///////////////////////////////////////////////////////////////////////////////

namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// Flags representing features of the ResourceLock.
   /// </summary>
   [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2217:DoNotMarkEnumsWithFlags"), Flags]
   public enum ResourceLockOptions {
      /// <summary>
      /// None.
      /// </summary>
      None = 0x00000000,

      /// <summary>
      /// If specified, then the thread that acquires the lock must also 
      /// release the lock. No other thread can release the lock.
      /// </summary>
      AcquiringThreadMustRelease = 0x00000001,

      /// <summary>
      /// If specified, then this lock supports recursion.
      /// </summary>
      SupportsRecursion = 0x00000002,

      /// <summary>
      /// Indicates that this lock is really a mutual-exclusive lock allowing only one thread to enter into it at a time.
      /// </summary>
      IsMutualExclusive = 0x00000004,

#if DEADLOCK_DETECTION
      /// <summary>
      /// If specified, then deadlock detection does not apply to this kind of lock.
      /// </summary>
      ImmuneFromDeadlockDetection = unchecked((Int32)0x80000000)
#endif
   }
}


///////////////////////////////////////////////////////////////////////////////

#if DEADLOCK_DETECTION
namespace Wintellect.Threading.ResourceLocks {
   internal struct ResourceLockOptionsHelper {
      private readonly ResourceLockOptions m_options;
      public ResourceLockOptionsHelper(ResourceLockOptions options) {
         m_options = options;
      }
      public Boolean IsMutualExclusive {
         get { return (m_options & ResourceLockOptions.IsMutualExclusive) != 0; }
      }

      public Boolean SupportsRecursion {
         get { return (m_options & ResourceLockOptions.SupportsRecursion) != 0; }
      }

      public Boolean AcquiringThreadMustRelease {
         get { return (m_options & ResourceLockOptions.AcquiringThreadMustRelease) != 0; }
      }

      public Boolean ImmuneFromDeadlockDetection {
         get { return (m_options & ResourceLockOptions.ImmuneFromDeadlockDetection) != 0; }
      }
   }
}
#endif

///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   /// <summary>
   /// Indicates the current state of the lock.
   /// </summary>
   public enum ResourceLockState {
      /// <summary>
      /// The lock is current not locked by any thread.
      /// </summary>
      NotLocked = 0,

      /// <summary>
      /// The lock is current locked for shared access.
      /// </summary>
      LockedForSharedAccess = 1,

      /// <summary>
      /// The lock is currently locked for exclusive access.
      /// </summary>
      LockedForExclusiveAccess = 2
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.ResourceLocks {
   ///<summary>A base class allowing all locks to have the same programming model.</summary>
   public abstract partial class ResourceLock : IFormattable, IDisposable {
#if DEADLOCK_DETECTION
      private static Boolean s_PerformDeadlockDetection = false;

      /// <summary>Turns deadlock detection or or off.</summary>
      /// <param name="enable">true to turn on deadlock detection; false to turn it off.</param>
      [Obsolete("NOTE: Deadlock detection contains a bug that occasionally causes it to report deadlock when deadlock does not actually exist.")]
      public static void PerformDeadlockDetection(Boolean enable) { s_PerformDeadlockDetection = enable; }

      /// <summary>Indicates if deadlock detection is currently on or off.</summary>
      public static Boolean IsDeadlockDetectionOn { get { return s_PerformDeadlockDetection; } }

      ///<summary>Indicates whether deadlock detection applies to this lock or not.</summary>
      ///<returns>True if deadlock detection doesn't apply to this lock.</returns>
      public Boolean ImmuneFromDeadlockDetection {
         get { return (m_resourceLockOptions & ResourceLockOptions.ImmuneFromDeadlockDetection) != 0; }
         set {
            if (value) m_resourceLockOptions |= ResourceLockOptions.ImmuneFromDeadlockDetection;
            else m_resourceLockOptions &= ~ResourceLockOptions.ImmuneFromDeadlockDetection;
         }
      }
#endif

      private String m_name;
      private ResourceLockOptions m_resourceLockOptions;

      /// <summary>Initializes a new instance of a reader/writer lock indicating whether the lock is really a mutual-exclusive lock 
      /// and whether the lock requires that any thread that enters it must be the same thread to exit it.</summary>
      /// <param name="resourceLockOptions">true if this lock really only allows one thread at a time into it; otherwise false.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily")]
      protected ResourceLock(ResourceLockOptions resourceLockOptions) {
         m_resourceLockOptions = resourceLockOptions;
         InitConditionalVariableSupport();
      }

      partial void InitConditionalVariableSupport();

      ///<summary>Releases all resources used by the reader/writer lock.</summary>
      public void Dispose() {
         Dispose(true);
         GC.SuppressFinalize(this);
      }

      ///<summary>Releases all resources used by the lock.</summary>
      [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed",
         Justification = "m_doneWritingDisposer and m_doneReadingDisposer don't represent native resources")]
      protected virtual void Dispose(Boolean disposing) {
      }

      /// <summary>Returns options that describe the behavior of this lock.</summary>
      public ResourceLockOptions ResourceLockOptions { get { return m_resourceLockOptions; } }

      // High 16 bits is num writers in lock; low 16-bits is num readers
      private const Int32 c_OneReaderCount = 0x0001;
      private const Int32 c_OneWriterCount = 0x10000;
      private Int32 m_readWriteCounts = 0;

      /// <summary>Returns the number of reader threads currently owning the lock.</summary>
      /// <returns>The number of reader threads in the lock.</returns>
      public Int32 CurrentReaderCount() { return m_readWriteCounts & 0xffff; }

      /// <summary>Returns the number of writer threads currently owning the lock.</summary>
      /// <returns>The number of writer threads in the lock.</returns>
      public Int32 CurrentWriterCount() { return m_readWriteCounts >> 16; }

      /// <summary>Returns true if no thread currently owns the lock.</summary>
      /// <returns>true if no thread currently own the lock.</returns>
      public Boolean CurrentlyFree() { return m_readWriteCounts == 0; }

      ///<summary>Indicates whether the lock treats all requests as mutual-exclusive.</summary>
      ///<returns>True if the lock class allows just one thread at a time.</returns>
      public Boolean IsMutualExclusive {
         get { return (m_resourceLockOptions & ResourceLockOptions.IsMutualExclusive) != 0; }
      }

      ///<summary>Indicates whether the lock supports recursion.</summary>
      ///<returns>True if the lock supports recursion.</returns>
      public Boolean SupportsRecursion {
         get { return (m_resourceLockOptions & ResourceLockOptions.SupportsRecursion) != 0; }
      }

      ///<summary>Indicates whether the thread that acquires the lock must also release the lock.</summary>
      ///<returns>True if the thread that requires the lock must also release it.</returns>
      public Boolean AcquiringThreadMustRelease {
         get { return (m_resourceLockOptions & ResourceLockOptions.AcquiringThreadMustRelease) != 0; }
      }

      /// <summary>
      /// The name associated with this lock for debugging purposes.
      /// </summary>
      public String Name {
         get { return m_name; }
         set {
            if (m_name == null) m_name = value;
            else throw new InvalidOperationException("This property has already been set and cannot be modified.");
         }
      }

      // NOTE: All locks must implement the WaitToWrite/DoneWriting methods
      ///<summary>Allows the calling thread to acquire the lock for writing or reading.</summary>
      /// <param name="exclusive">true if the thread wishes to acquire the lock for exclusive access.</param>
      public void Enter(Boolean exclusive) {
#if DEADLOCK_DETECTION
         if (exclusive) {
            if (AcquiringThreadMustRelease) Thread.BeginCriticalRegion();
            using (s_PerformDeadlockDetection ? DeadlockDetector.BlockForLock(this, true) : null) {
               OnEnter(exclusive);
            }
         } else {
            // When reading, there is no need to call BeginCriticalRegion since resource is not being modified
            using (s_PerformDeadlockDetection ? DeadlockDetector.BlockForLock(this, IsMutualExclusive) : null) {
               OnEnter(exclusive);
            }
         }
#else
         OnEnter(exclusive);
#endif
         Interlocked.Add(ref m_readWriteCounts, exclusive ? c_OneWriterCount : c_OneReaderCount);
      }

      ///<summary>Derived class overrides <c>OnEnter</c> to provide specific lock-acquire semantics.</summary>
      protected abstract void OnEnter(Boolean exclusive);

      ///<summary>Derived class overrides <c>OnLeave</c> to provide specific lock-release semantics.</summary>
      protected abstract void OnLeave(Boolean exclusive);

      ///<summary>Allows the calling thread to release the lock.</summary>
      public void Leave() {
         Contract.Assume(!CurrentlyFree());
         Boolean exclusive = CurrentReaderCount() == 0;
#if DEADLOCK_DETECTION
         if (s_PerformDeadlockDetection) DeadlockDetector.ReleaseLock(this);
#endif
         OnLeave(exclusive);
         if (exclusive) {
            Interlocked.Add(ref m_readWriteCounts, -c_OneWriterCount);
            //if (AcquiringThreadMustRelease) Thread.EndCriticalRegion();
         } else {
            Interlocked.Add(ref m_readWriteCounts, -c_OneReaderCount);
            // When done reading, there is no need to call EndCriticalRegion since resource was not modified
         }
      }

      #region Helper Methods

      ///<summary>If<c>Stress</c> is defined during compilation, calls to this method cause the calling thread to sleep.</summary>
      [Conditional("Stress")]
      protected static void StressPause() { Thread.Sleep(2); }

      ///<summary>Allows calling thread to yield CPU time to another thread.</summary>
      protected static void StallThread() { ThreadUtility.StallThread(); }

      ///<summary>Compares two values for equality and, if they are equal, replaces one of the values.</summary>
      ///<return>Returns true if the value in <paramref name="value"/> was equal the the value of <paramref name="if"/>.</return>
      ///<param name="value">The destination, whose value is compared with <paramref name="if"/> and possibly replaced with <paramref name="then"/>.</param>
      ///<param name="if">The value that is compared to the value at <paramref name="value"/>.</param>
      ///<param name="then">The value that might get placed into <paramref name="value"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      protected static Boolean IfThen(ref Int32 value, Int32 @if, Int32 then) {
         return InterlockedEx.IfThen(ref value, @if, then);
      }

      ///<summary>Compares two values for equality and, if they are equal, replaces one of the values.</summary>
      ///<remarks>The previous value in <paramref name="value"/> is returned in <paramref name="previousValue"/>.</remarks>
      ///<return>Returns true if the value in <paramref name="value"/> was equal the the value of <paramref name="if"/>.</return>
      ///<param name="value">The destination, whose value is compared with <paramref name="if"/> and possibly replaced with <paramref name="then"/>.</param>
      ///<param name="if">The value that is compared to the value at <paramref name="value"/>.</param>
      ///<param name="then">The value that might get placed into <paramref name="value"/>.</param>
      ///<param name="previousValue">The previous value that was in <paramref name="value"/> prior to calling this method.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#"), SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters")]
      protected static Boolean IfThen(ref Int32 value, Int32 @if, Int32 then, out Int32 previousValue) {
         return InterlockedEx.IfThen(ref value, @if, then, out previousValue);
      }

      #endregion

      #region IFormattable Members
      ///<summary>Returns the object's string representation.</summary>
      ///<return>A <c>String</c> containing the object's string representation.</return>
      public String ToString(String format) { return ToString(format, null); }

      ///<summary>Returns the object's string representation.</summary>
      ///<return>A <c>String</c> containing the object's string representation.</return>
      public String ToString(IFormatProvider formatProvider) {
         return ToString(null, formatProvider);
      }

      ///<summary>Returns the object's string representation.</summary>
      ///<param name="format">If <c>null</c> or <c>"extra"</c> is allowed.</param>
      ///<param name="formatProvider">Not used.</param>
      ///<return>A <c>String</c> containing the object's string representation.</return>
      public virtual String ToString(String format, IFormatProvider formatProvider) {
         if (format == null) return ToString();
         if (String.Compare(format, "extra", StringComparison.OrdinalIgnoreCase) == 0)
            return ToString();
         throw new FormatException("Unknown format string: " + format);
      }

      /// <summary>
      /// Returns a System.String that represents the current System.Object.
      /// </summary>
      /// <returns>Returns a System.String that represents the current System.Object.</returns>
      public override string ToString() {
         return m_name ?? base.ToString();
      }

      #endregion

      /// <summary>
      /// Determines whether the specified System.Object is equal to the current System.Object.
      /// </summary>
      /// <param name="obj">The System.Object to compare with the current System.Object.</param>
      /// <returns>true if the specified System.Object is equal to the current System.Object; otherwise, false.</returns>
      public override Boolean Equals(Object obj) {
         ResourceLock other = obj as ResourceLock;
         if (other == null) return false;
         return (GetType() == other.GetType()) && (m_resourceLockOptions == other.m_resourceLockOptions);
      }

      /// <summary>
      /// Serves as a hash function for a particular type.
      /// </summary>
      /// <returns>A hash code for the current System.Object.</returns>
      public override Int32 GetHashCode() {
         return base.GetHashCode();
      }

#if DEBUG
      public void Hammer() {
         for (Int32 n = 0; n < 5; n++) {
            Hammer(10, 100);
            // The lock should settle back to Free here
         }
         Console.WriteLine("done");
         Console.ReadLine();
      }

      private volatile Boolean m_stop = false;
      private readonly Random m_rand = new Random();

      private void Hammer(Int32 exclusive, Int32 shared) {
         Console.WriteLine("Hammering {0} exclusive & {1} shared starting.", exclusive, shared);
         m_stop = false;
         Int32 threads = exclusive + shared;
         List<Thread> l = new List<Thread>();
         Int32 writersInLock = 0, readersInLock = 0;

         for (Int32 n = 0; n < threads; n++) {
            Thread t = new Thread(num => {
               Boolean exclusiveThread = ((Int32)num) < exclusive;
               while (!m_stop) {
                  Enter(exclusiveThread);
                  if (exclusiveThread) {
                     if (Interlocked.Increment(ref writersInLock) != 1 || Thread.VolatileRead(ref readersInLock) != 0) Debugger.Break();
                  } else {
                     if (Interlocked.Increment(ref readersInLock) > shared || Thread.VolatileRead(ref writersInLock) != 0) Debugger.Break();
                  }
                  Console.WriteLine("   ThreadNum={0,3}, Writers={1}, Readers={2}", num, writersInLock, readersInLock);

                  // Body
                  var bodyWork = m_rand.Next(10) * (exclusiveThread ? 100 : 10);
                  for (var end = Environment.TickCount + bodyWork; Environment.TickCount < end; ) ;

                  if (exclusiveThread) Interlocked.Decrement(ref writersInLock);
                  else Interlocked.Decrement(ref readersInLock);
                  Leave();

                  var iterationSleep = m_rand.Next(100) * (exclusiveThread ? 100 : 10);
                  Thread.Sleep(iterationSleep);
               }
            });
            t.Name = n.ToString() + " " + ((n < exclusive) ? " exclusive" : " shared");
            l.Add(t);
            t.Start(n);
         }
         Thread.Sleep(TimeSpan.FromMinutes(5));
         m_stop = true;
         foreach (var t in l) t.Join();
         Console.WriteLine("Hammering {0} exclusive & {1} shared completed.", exclusive, shared);
      }
#endif
   }
}


///////////////////////////////////////////////////////////////////////////////

#if !SILVERLIGHT && false
// This code adds Condition Variable support to all ResourceLock-derived types
namespace Wintellect.Threading.ResourceLocks {
   /// <summary></summary>
   /// <returns></returns>
   public delegate Boolean Condition();

   public partial class ResourceLock {
      private sealed class ResourceLockConditionVariable : ConditionVariable {
         // Refers to the resource lock to wrap
         private ResourceLock m_resourceLock;

         public ResourceLockConditionVariable(ResourceLock resourceLock) {
            m_resourceLock = resourceLock;
         }

         public void CVWait() { Contract.Assume(m_resourceLock != null); base.CVWait(m_resourceLock); }
         public void CVWait(Condition condition) {
            Contract.Requires(condition != null);
            if (condition()) return;
            Contract.Assume(m_resourceLock != null);
            base.CVWait(m_resourceLock);
            while (!condition()) {
               Contract.Assume(m_resourceLock != null);  // TODO: Why?
               base.CVWait(m_resourceLock);
            }
         }

#if Useful  // CVWait is probably all we really need
         public void CVWaitAll(params Condition[] conditions) {
            while (true) {
               Boolean allConditionsTrue = false;
               for (Int32 index = 0; index < conditions.Length; index++) {
                  if (!(allConditionsTrue = conditions[index]())) break;
               }
               if (allConditionsTrue) return;
               base.CVWait(m_resourceLock);
            }
         }
#endif

         public Int32 CVWaitAny(params Condition[] conditions) {
            Contract.Requires(conditions != null);
            while (true) {
               for (Int32 index = 0; index < conditions.Length; index++) {
                  Condition c = conditions[index];
                  Contract.Assume(c != null);
                  if (c()) return index;
               }
               Contract.Assume(m_resourceLock != null);
               base.CVWait(m_resourceLock);
            }
         }
      }

      // Used to add condition variable support to this lock
      private Singleton<ResourceLockConditionVariable> m_conditionVariable = null;
      private void InitConditionalVariableSupport() {
         m_conditionVariable = new Singleton<ResourceLockConditionVariable>(
            SingletonRaceLoser.GC,
            delegate { return new ResourceLockConditionVariable(this); });
      }

      /// <summary></summary>
      public void CVWait() { m_conditionVariable.Value.CVWait(); }

      /// <summary></summary>
      /// <param name="condition"></param>
      public void CVWait(Condition condition) { Contract.Requires(condition != null);  m_conditionVariable.Value.CVWait(condition); }

      /// <summary></summary>
      /// <param name="conditions"></param>
      /// <returns></returns>
      public Int32 CVWaitAny(params Condition[] conditions) {
         Contract.Requires(conditions != null);
         return m_conditionVariable.Value.CVWaitAny(conditions);
      }

      /// <summary></summary>
      public void CVPulseOne() {
         Contract.Assume(m_conditionVariable != null);
         m_conditionVariable.Value.CVPulseOne();
      }

      /// <summary></summary>
      public void CVPulseAll() {
         Contract.Assume(m_conditionVariable != null);
         m_conditionVariable.Value.CVPulseAll();
      }
   }
}
#endif


//////////////////////////////// End of File //////////////////////////////////