/******************************************************************************
Module:  Disposer.cs
Notices: Copyright (c) 2006-2009 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;
using System.Diagnostics.Contracts;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect {
   /// <summary>
   /// Puts an IDisposable wrapper around a callback method allowing any 
   /// method to be used with the C# using statement. 
   /// </summary>
   public sealed class Disposer : IDisposable {
      /// <summary>
      /// A delegate that matches the signature of IDisposable's Dispose method.
      /// </summary>
      private readonly ThreadStart m_disposeMethod = null;

      /// <summary>
      /// Constructs a Dispose object around the specified method.
      /// </summary>
      /// <param name="disposeMethod">The method that should be called via Dispose.</param>
      public Disposer(ThreadStart disposeMethod) {
         if (disposeMethod == null) throw new ArgumentNullException("disposeMethod");
         m_disposeMethod = disposeMethod;
      }

      /// <summary>
      /// Invokes the desired method via this method.
      /// </summary>
      public void Dispose() { Contract.Assume(m_disposeMethod != null); m_disposeMethod(); }
   }
}


//////////////////////////////// End of File //////////////////////////////////
