/******************************************************************************
Module:  SyncContextEventRaiser.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/

using System;
using System.Threading;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

///////////////////////////////////////////////////////////////////////////////

#if true
namespace Wintellect.Threading {
   /// <summary>
   /// This class raises an event using a specific SynchronizationContext object.
   /// </summary> 
   public sealed class SyncContextEventRaiser {
      // This SynchronizationContext object will help us marshal events to the correct thread:
      // For Windows Forms app, it will marshal events to the GUI thread
      // For other apps, it will do the right thing (which may mean no marshalling at all)
      private SynchronizationContext m_syncContext;

      /// <summary>
      /// Constructs a SyncContextEventRaiser using the current thread's SynchronizationContext.
      /// </summary>
      public SyncContextEventRaiser() : this(null) { }

      /// <summary>
      /// Constructs a SyncContextEventRaiser using the specified SynchronizationContext.
      /// </summary>
      public SyncContextEventRaiser(SynchronizationContext syncContext) {
         m_syncContext = syncContext ?? AsyncOperationManager.SynchronizationContext;
      }

      /// <summary>Represents a callback to a protected virtual method that raises an event.</summary>
      /// <typeparam name="T">The <see cref="T:System.EventArgs"/> type identifying the type of object that gets raised with the event"/></typeparam>
      /// <param name="e">The <see cref="T:System.EventArgs"/> object that should be passed to a protected virtual method that raises the event.</param>
      [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
      public delegate void EventRaiser<T>(T e) where T : EventArgs;

      /// <summary>A method that asynchronously raises an event on the application's desired thread.</summary>
      /// <typeparam name="T">The <see cref="T:System.EventArgs"/> type identifying the type of object that gets raised with the event"/></typeparam>
      /// <param name="callback">The protected virtual method that will raise the event.</param>
      /// <param name="e">The <see cref="T:System.EventArgs"/> object that should be passed to the protected virtual method raising the event.</param>
      public void PostEvent<T>(EventRaiser<T> callback, T e) where T : EventArgs {
         m_syncContext.Post(delegate(Object state) { callback((T)state); }, e);
      }

      /// <summary>A method that synchronously raises an event on the application's desired thread.</summary>
      /// <typeparam name="T">The <see cref="T:System.EventArgs"/> type identifying the type of object that gets raised with the event"/></typeparam>
      /// <param name="callback">The protected virtual method that will raise the event.</param>
      /// <param name="e">The <see cref="T:System.EventArgs"/> object that should be passed to the protected virtual method raising the event.</param>
      public void SendEvent<T>(EventRaiser<T> callback, T e) where T : EventArgs {
         m_syncContext.Send(delegate(Object state) { callback((T)state); }, e);
      }
   }
}
#endif

//////////////////////////////// End of File //////////////////////////////////
