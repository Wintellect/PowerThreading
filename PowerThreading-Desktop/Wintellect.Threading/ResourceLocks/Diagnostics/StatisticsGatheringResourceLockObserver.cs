/******************************************************************************
Module:  StatisticsGatheringResourceLockObserver.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
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
   ///<summary>A compositional resource lock providing statics for another lock.</summary>
   public sealed class StatisticsGatheringResourceLockObserver : ResourceLockObserver {

      [AttributeUsage(AttributeTargets.Property)]
      private sealed class StatisticPropertyAttribute : Attribute { }

      private static List<PropertyInfo> InitializeProperties() {
         List<PropertyInfo> props = new List<PropertyInfo>();
         foreach (PropertyInfo pi in typeof(StatisticsGatheringResourceLockObserver).GetProperties()) {
            if (Attribute.IsDefined(pi, typeof(StatisticPropertyAttribute)))
               props.Add(pi);
         }
         return props;
      }
      private static List<PropertyInfo> s_statisticProperties = InitializeProperties();

      private Int64 m_ReadRequests = 0, m_WriteRequests = 0;

      ///<summary>Returns the number of read requests against a lock.</summary>
      ///<return>The number of read requests.</return>
      [StatisticProperty]
      public Int64 ReadRequests { get { return m_ReadRequests; } }

      ///<summary>Returns the number of write requests against a lock.</summary>
      ///<return>The number of write requests.</return>
      [StatisticProperty]
      public Int64 WriteRequests { get { return m_WriteRequests; } }

      private Int64 m_ReadersReading = 0, m_WritersWriting = 0;

      ///<summary>Returns the current number of readers reading.</summary>
      ///<return>The current number of reads.</return>
      [StatisticProperty]
      public Int64 ReadersReading { get { return m_ReadersReading; } }

      ///<summary>Returns the current number of writers writing.</summary>
      ///<return>The current number of writers.</return>
      [StatisticProperty]
      public Int64 WritersWriting { get { return m_WritersWriting; } }


      private Int64 m_ReadersDone = 0, m_WritersDone = 0;

      ///<summary>Returns the number of readers done reading.</summary>
      ///<return>The number of done readers.</return>
      [StatisticProperty]
      public Int64 ReadersDone { get { return m_ReadersDone; } }

      ///<summary>Returns the number of writers done writing.</summary>
      ///<return>The number of done writers.</return>
      [StatisticProperty]
      public Int64 WritersDone { get { return m_WritersDone; } }


      private Int64 m_ReadersWaiting = 0, m_WritersWaiting = 0;

      ///<summary>Returns the current number of readers waiting.</summary>
      ///<return>The number of waiting readers.</return>
      [StatisticProperty]
      public Int64 ReadersWaiting { get { return m_ReadersWaiting; } }

      ///<summary>Returns the current number of writers waiting.</summary>
      ///<return>The number of waiting writers.</return>
      [StatisticProperty]
      public Int64 WritersWaiting { get { return m_WritersWaiting; } }


      private Int64 m_ReaderMaxWaitTime = 0, m_WriterMaxWaitTime = 0;

      ///<summary>Returns the maximum time that a reader waited to acquire the lock.</summary>
      ///<return>Maximum time a reader waited to acquire the lock.</return>
      [StatisticProperty]
      public TimeSpan ReaderMaxWaitTime { get { return TimeSpan.FromMilliseconds(m_ReaderMaxWaitTime); } }

      ///<summary>Returns the maximum time that a writer waited to acquire the lock.</summary>
      ///<return>Maximum time a writer waited to acquire the lock.</return>
      [StatisticProperty]
      public TimeSpan WriterMaxWaitTime { get { return TimeSpan.FromMilliseconds(m_WriterMaxWaitTime); } }


      private Int64 m_ReaderMinHoldTime = Int64.MaxValue, m_ReaderMaxHoldTime = 0;
      private Dictionary<Int32, Int64> m_ReaderStartHoldTime = new Dictionary<Int32, Int64>();

      ///<summary>Returns the minimum time that a reader held the lock.</summary>
      ///<return>Minimum time a reader held the lock.</return>
      [StatisticProperty]
      public TimeSpan ReaderMinHoldTime { get { return TimeSpan.FromMilliseconds(m_ReaderMinHoldTime); } }

      ///<summary>Returns the maximum time that a reader held the lock.</summary>
      ///<return>Maximum time a reader held the lock.</return>
      [StatisticProperty]
      public TimeSpan ReaderMaxHoldTime { get { return TimeSpan.FromMilliseconds(m_ReaderMaxHoldTime); } }


      private Int64 m_WriterMinHoldTime = Int64.MaxValue, m_WriterMaxHoldTime = 0;
      private Int64 m_WriterStartHoldTime;

      ///<summary>Returns the minimum time that a writer held the lock.</summary>
      ///<return>Minimum time a writer held the lock.</return>
      [StatisticProperty]
      public TimeSpan WriterMinHoldTime { get { return TimeSpan.FromMilliseconds(m_WriterMinHoldTime); } }

      ///<summary>Returns the maximum time that a writer held the lock.</summary>
      ///<return>Maximum time a writer held the lock.</return>
      [StatisticProperty]
      public TimeSpan WriterMaxHoldTime { get { return TimeSpan.FromMilliseconds(m_WriterMaxHoldTime); } }

      ///<summary>Initializes a new instance of the <c>StatisticsGatheringResourceLock</c> class that wraps another <c>ResouceLock</c>-derived type.</summary>
      ///<param name="resLock">The <c>ResourceLock</c>-derived type to wrap.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1805:DoNotInitializeUnnecessarily")]
      public StatisticsGatheringResourceLockObserver(ResourceLock resLock)
         : base(resLock) {
         Contract.Requires(resLock != null);
      }

      ///<summary>Returns the object's string representation.</summary>
      ///<param name="format">If <c>null</c> or <c>"extra"</c> is allowed.</param>
      ///<param name="formatProvider">Not used.</param>
      ///<return>A <c>String</c> containing the object's string representation.</return>
      public override String ToString(String format, IFormatProvider formatProvider) {
         StringBuilder sb = new StringBuilder(base.ToString(format, formatProvider));
         if (String.Compare(format, "extra", StringComparison.OrdinalIgnoreCase) == 0) {
            sb.AppendLine();
            foreach (PropertyInfo pi in s_statisticProperties) {
               sb.AppendLine("   " + pi.Name + "=" + pi.GetValue(this, null));
            }
         }
         return sb.ToString();
      }

      ///<summary>Derived class overrides <c>OnEnter</c> to provide specific reader locking semantics.</summary>
      protected override void OnEnter(Boolean exclusive) {
         if (exclusive) {
            Interlocked.Increment(ref m_WriteRequests);
            Interlocked.Increment(ref m_WritersWaiting);
            Int64 startTime = Environment.TickCount;
            InnerLock.Enter(exclusive);

            // Only 1 thread is writing, so no thread safety is required here
            m_WriterMaxWaitTime = Math.Max(m_WriterMaxWaitTime, checked((Int64)(Environment.TickCount - startTime)));
            Interlocked.Decrement(ref m_WritersWaiting);
            Interlocked.Increment(ref m_WritersWriting);
            m_WriterStartHoldTime = Environment.TickCount;
         } else {
            Interlocked.Increment(ref m_ReadRequests);
            Interlocked.Increment(ref m_ReadersWaiting);
            Int64 startTime = Environment.TickCount;
            InnerLock.Enter(exclusive);

            InterlockedEx.Max(ref m_ReaderMaxWaitTime, checked((Int64)(Environment.TickCount - startTime)));
            Interlocked.Decrement(ref m_ReadersWaiting);
            Interlocked.Increment(ref m_ReadersReading);
            Monitor.Enter(m_ReaderStartHoldTime);
            m_ReaderStartHoldTime.Add(Thread.CurrentThread.ManagedThreadId, Environment.TickCount);
            Monitor.Exit(m_ReaderStartHoldTime);
         }
      }

      ///<summary>Derived class overrides <c>OnDoneReading</c> to provide specific reader unlocking semantics.</summary>
      ///<remarks>You do not need to override this method if the specific lock provides mutual-exclusive locking semantics.</remarks>
      protected override void OnLeave(Boolean write) {
         if (write) {
            // Only 1 thread is writing, so no thread safety is required here
            Int64 HoldTime = checked((Int64)(Environment.TickCount - m_WriterStartHoldTime));
            m_WriterMinHoldTime = Math.Min(m_WriterMinHoldTime, HoldTime);
            m_WriterMaxHoldTime = Math.Max(m_WriterMaxHoldTime, HoldTime);
            m_WritersWriting--;
            m_WritersDone++;
            InnerLock.Leave();
         } else {
            Int32 threadId = Thread.CurrentThread.ManagedThreadId;
            Int64 HoldTime = checked((Int64)(Environment.TickCount - m_ReaderStartHoldTime[threadId]));
            Monitor.Enter(m_ReaderStartHoldTime);
            m_ReaderStartHoldTime.Remove(threadId);
            Monitor.Exit(m_ReaderStartHoldTime);

            InterlockedEx.Min(ref m_ReaderMinHoldTime, HoldTime);
            InterlockedEx.Max(ref m_ReaderMaxHoldTime, HoldTime);
            Interlocked.Decrement(ref m_ReadersReading);
            Interlocked.Increment(ref m_ReadersDone);

            InnerLock.Leave();
         }
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////