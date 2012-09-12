/******************************************************************************
Module:  Progress.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;
using Wintellect.Threading;
using Wintellect.Threading.ResourceLocks;
using System.Globalization;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading {
   /// <summary>
   /// A thread-safe class for managing the progress of an operation.
   /// </summary>
   public sealed class Progress : IDisposable {
      private SyncContextEventRaiser m_syncContentEventRaiser = new SyncContextEventRaiser();
      private ResourceLock m_lock = new MonitorResourceLock();
      private Int64 m_low = 0, m_current = 0, m_high = 0;
      private Timer m_timer = null;
      private Int64 m_timerUpdateAmount = 0;

      /// <summary>
      /// Constructs a Progress object with a low and high value of zero.
      /// </summary>
      public Progress() : this(0, 0) { }

      /// <summary>
      /// Constructs a Progress object with a low value of zero.
      /// </summary>
      /// <param name="high">The value indicating the completion of the operation.</param>
      public Progress(Int64 high) : this(0, high) { }

      /// <summary>
      /// Constructs a Progress object with the specified low and high values.
      /// </summary>
      /// <param name="low">The value indicating the start of the operation.</param>
      /// <param name="high">The value indicating the completion of the operation.</param>
      public Progress(Int64 low, Int64 high) {
         m_timer = new Timer(TimerProgressUpdate);
         SetValues(low, high, low);
      }

      /// <summary>
      /// Sets all the values associated with maintaining progress of an operation.
      /// </summary>
      /// <param name="low">The value indicating the start of the operation.</param>
      /// <param name="high">The value indicating the completion of the operation.</param>
      /// <param name="current">The value indicating the current completion state of the operation.</param>
      public void Reset(Int64 low, Int64 high, Int64 current) {
         StopTimer();
         SetValues(low, high, current);
      }

      /// <summary>
      /// Call this to indicate that Progress tracking for this operation is complete.
      /// </summary>
      public void Dispose() {
         m_timer.Dispose();
         m_lock.Dispose();
      }

      /// <summary>
      /// Allows Progress to automatically update periodically.
      /// </summary>
      /// <param name="millisecondsBetweenUpdates">Indicates how long to wait before each update to Progress.</param>
      /// <param name="timerUpdateAmount">How much to add to the current status.</param>
      public void SetTimer(Int32 millisecondsBetweenUpdates, Int64 timerUpdateAmount) {
         m_timerUpdateAmount = timerUpdateAmount;
         m_timer.Change(millisecondsBetweenUpdates, millisecondsBetweenUpdates);
      }

      /// <summary>
      /// Stops the timer from continuing to update progress status.
      /// </summary>
      public void StopTimer() {
         SetTimer(Timeout.Infinite, 0);
      }
      private void TimerProgressUpdate(Object state) {
         AddToCurrent(m_timerUpdateAmount);
      }

      /// <summary>
      /// Returns the value indicating the start of the operation.
      /// </summary>
      public Int64 Low { get { return m_low; } }

      /// <summary>
      /// Sets the value indicating the start of the operation.
      /// </summary>
      /// <param name="value">The value indicating the start of the operation.</param>
      public void SetLow(Int64 value) {
         SetValues(value, null, null);
      }

      /// <summary>
      /// Adds the specified value to the value that indicates the start of the operation.
      /// </summary>
      /// <param name="addend">How much to add.</param>
      public void AddToLow(Int64 addend) {
         AddToValues(addend, 0, 0);
      }

      /// <summary>
      /// Returns the value indicating the end of the operation.
      /// </summary>
      public Int64 High { get { return m_high; } }

      /// <summary>
      /// Sets the value indicating the end of the operation.
      /// </summary>
      /// <param name="value">The value indicating the end of the operation.</param>
      public void SetHigh(Int64 value) {
         SetValues(null, value, null);
      }

      /// <summary>
      /// Adds the specified value to the value that indicates the end of the operation.
      /// </summary>
      /// <param name="addend">How much to add.</param>
      public void AddToHigh(Int64 addend) {
         AddToValues(0, addend, 0);
      }


      /// <summary>
      /// Returns the value indicating the current state of the operation.
      /// </summary>
      public Int64 Current { get { return m_current; } }

      /// <summary>
      /// Sets the value indicating the current state of the operation.
      /// </summary>
      /// <param name="value">The value indicating the current state of the operation.</param>
      public void SetCurrent(Int64 value) {
         SetValues(null, null, value);
      }

      /// <summary>
      /// Adds the specified value to the value that indicates the current state of the operation.
      /// </summary>
      /// <param name="addend">How much to add.</param>
      public void AddToCurrent(Int64 addend) {
         AddToValues(0, 0, addend);
      }

      private void AddToValues(Int64 lowAddend, Int64 highAddend, Int64 currentAddend) {
         Boolean reportProgressUpdate;
         m_lock.Enter(true);
         reportProgressUpdate = SetValues(true, m_low + lowAddend, m_high + highAddend, m_current + currentAddend);
         m_lock.Leave();
         ReportProgressUpdate(reportProgressUpdate);
      }

      // A value of null means that the value doesn't change
      private void SetValues(Int64? low, Int64? high, Int64? current) {
         ReportProgressUpdate(SetValues(false, low, high, current));
      }

      // Returns true if low, high, or current has changed
      private Boolean SetValues(Boolean lockAlreadyTaken, Int64? low, Int64? high, Int64? current) {
         if (!lockAlreadyTaken) m_lock.Enter(true);
         try {
            // If any value is null, set it to its current value (assume no change)
            low = low.GetValueOrDefault(m_low);
            high = high.GetValueOrDefault(m_high);
            current = current.GetValueOrDefault(m_current);

            if (low > high) throw new InvalidOperationException("Low can't be greater than high");

            Int64 oldLow = m_low, oldHigh = m_high, oldCurrent = m_current;
            m_low = low.Value;
            m_high = high.Value;

            // Make sure Current stays between Low and High inclusive
            if (current < m_low) current = m_low;
            if (current > m_high) current = m_high;
            m_current = current.Value;

            return (oldLow != m_low) || (oldHigh != m_high) || (oldCurrent != m_current);
         }
         finally {
           if (!lockAlreadyTaken) m_lock.Leave();
         }
      }

      /// <summary>
      /// An event which is raised whenever the operation's low, current, or high value changes.
      /// This event is raised using the SynchronizationContext that was in place on 
      /// the thread that constructed this Progress object.
      /// </summary>
      public event EventHandler<ProgressUpdateEventArgs> ProgressUpdated;
      private void ReportProgressUpdate(Boolean reportProgressUpdate) {
         if (!reportProgressUpdate) return;
         m_syncContentEventRaiser.PostEvent(OnProgressUpdate,
            new ProgressUpdateEventArgs(m_low, m_high, m_current));
      }

      private void OnProgressUpdate(ProgressUpdateEventArgs e) {
         EventHandler<ProgressUpdateEventArgs> t = ProgressUpdated;
         if (t != null) t(this, e);
      }
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading {
   /// <summary>
   /// Indicates the current progress of an operation.
   /// </summary>
   public sealed class ProgressUpdateEventArgs : EventArgs {
      private readonly Int64 m_low, m_high, m_current;
      private readonly Int32 m_percentage;

      internal ProgressUpdateEventArgs(Int64 low, Int64 high, Int64 current) {
         m_low = low;
         m_high = high;
         m_current = current;
         try {
            m_percentage = (Int32)((100 * (m_current - m_low)) / (m_high - m_low));
         }
         catch (DivideByZeroException) { /* m_percentage will be 0 */ }
      }

      /// <summary>
      /// Returns the value representing the start of the operation.
      /// </summary>
      public Int64 Low { get { return m_low; } }

      /// <summary>
      /// Returns the value representing the end of the operation.
      /// </summary>
      public Int64 High { get { return m_high; } }

      /// <summary>
      /// Returns a value representing the current state of the operation.
      /// </summary>
      public Int64 Current { get { return m_current; } }

      /// <summary>
      /// Returns a percentage of how much of the operation has completed thus far.
      /// </summary>
      public Int32 Percentage { get { return m_percentage; } }

      /// <summary>
      /// Returns a string representing the state of this object.
      /// </summary>
      /// <returns>The string representing the state of the object.</returns>
      public override String ToString() {
         return String.Format(CultureInfo.CurrentCulture,
            "Low={0}, High={1}, Current={2}, Percentage={3}%",
            Low, High, Current, Percentage);
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////
