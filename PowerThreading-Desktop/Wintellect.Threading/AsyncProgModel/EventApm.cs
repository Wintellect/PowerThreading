/******************************************************************************
Module:  EventApm.cs
Notices: Copyright (c) 2010 by Jeffrey Richter and Wintellect
******************************************************************************/

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;

///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.AsyncProgModel {
   /// <summary>
   /// This class represents an asynchronous operation that will be initiated
   /// by some method and the operations completes by way of raising an event.
   /// </summary>
   /// <typeparam name="TEventArgs">The object passed as the event's second argument. 
   /// This object is usually of an EventArgs-derived type.</typeparam>
   [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Apm"), DebuggerStepThrough]
   public sealed class EventApmOperation<TEventArgs> {
      private AsyncCallback m_callback;
      private Object m_state;

      internal EventApmOperation(AsyncCallback callback, Object state) {
         m_callback = callback;
         m_state = state;
      }

      /// <summary>
      /// The event handler that completes the APM operation when invoked.
      /// </summary>
      /// <param name="sender">The source of the event.</param>
      /// <param name="e">An object (usually derived from EventArgs) that contains the event data.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "e"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "sender")]
      public void EventHandler(Object sender, TEventArgs e) {
         var result = new AsyncResult<TEventArgs>(m_callback, m_state);
         var acea = e as System.ComponentModel.AsyncCompletedEventArgs;
         if ((acea != null) && (acea.Error != null)) result.SetAsCompleted(acea.Error, false);
         else result.SetAsCompleted(e, false);
      }
   }

   /// <summary>
   /// This class converts a raised event to an IAsyncResult-based APM completion.
   /// </summary>
   [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Apm"), DebuggerStepThrough]
   public struct EventApmFactory<TEventArgs> {
      /// <summary>
      /// Prepares to initiate an asynchronous operation by setting the desired callback 
      /// method and state that will be used to completed the operation when the 
      /// event handler method is invoked.
      /// </summary>
      /// <param name="callback">The method to be called when the event is raised.</param>
      /// <param name="state">The state obtained via IAsyncResult's AsyncState property.</param>
      /// <returns>The prepared operation which exposes the event handler method to be registered with the desired event.</returns>
      public EventApmOperation<TEventArgs> PrepareOperation(AsyncCallback callback, Object state) {
         return new EventApmOperation<TEventArgs>(callback, state);
      }

      /// <summary>
      /// Prepares to initiate an asynchronous operation by setting the desired callback 
      /// method that will be used to completed the operation when the 
      /// event handler method is invoked.
      /// </summary>
      /// <param name="callback">The method to be called when the event is raised.</param>
      /// <returns>The prepared operation which exposes the event handler method to be registered with the desired event.</returns>
      public EventApmOperation<TEventArgs> PrepareOperation(AsyncCallback callback) {
         return new EventApmOperation<TEventArgs>(callback, null);
      }

      /// <summary>
      /// Returns the object passed as the second argument to the event handler. 
      /// This is usually an object whose type is derived from System.EventArgs.
      /// Note that you can use any instance of this type to call EndInvoke; you do not 
      /// have to use the same instance that you used to call PrepareOperation.
      /// </summary>
      /// <param name="result">An IAsyncResult that references the pending operation.</param>
      /// <returns>The object passed to the event handler.</returns>
      public TEventArgs EndInvoke(IAsyncResult result) {
         Contract.Requires(result != null);
         return ((AsyncResult<TEventArgs>)result).EndInvoke();
      }

      /// <summary>Indicates whether this instance and a specified object are equal.</summary>
      /// <param name="obj">Another object to compare to.</param>
      /// <returns>true if obj and this instance are the same type and represent the same value; otherwise, false.</returns>
      public override bool Equals(Object obj) {
         if (obj == null) return false;
         return obj.GetType() == this.GetType(); 
      }

      /// <summary>Returns a value indicating whether two instances of EventApmFactory are equal.</summary>
      /// <param name="factory1">A reference to an EventApmFactory object.</param>
      /// <param name="factory2">A reference to an EventApmFactory object.</param>
      /// <returns>true if factory1 and factory2 are equal; otherwise, false.</returns>
      public static Boolean operator==(EventApmFactory<TEventArgs> factory1, EventApmFactory<TEventArgs> factory2) {
         return factory1.Equals(factory2);
      }

      /// <summary>Returns a value indicating whether two instances of EventApmFactory are not equal.</summary>
      /// <param name="factory1">A reference to an EventApmFactory object.</param>
      /// <param name="factory2">A reference to an EventApmFactory object.</param>
      /// <returns>true if factory1 and factory2 are not equal; otherwise, false.</returns>
      public static Boolean operator !=(EventApmFactory<TEventArgs> factory1, EventApmFactory<TEventArgs> factory2) {
         return !factory1.Equals(factory2);
      }

      /// <summary>Returns the hash code for this instance.</summary>
      /// <returns>A 32-bit signed integer that is the hash code for this instance.</returns>
      public override int GetHashCode() { return base.GetHashCode(); }
   }
}


//////////////////////////////// End of File //////////////////////////////////