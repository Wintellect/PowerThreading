/******************************************************************************
Module:  Singleton.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading {
   /// <summary>
   /// Indicates whether singleton should be created using a double-check 
   /// locking technique or via an interlocked-compare-exchange technique.
   /// </summary>
   public enum SingletonRaceLoser {
      /// <summary>
      /// If there is a race to create the singleton, the race is 
      /// resolved by blocking all but one thread.
      /// </summary>
      Block = 0,

      /// <summary>
      /// If there is a race to create the singleton, the race is resolved
      /// by having all threads create the singleton but, when done, only one 
      /// thread will win and the losing threads will have their singleton GC'd
      /// </summary>
      GC = 1,
   }

   /// <summary>
   /// This class ensures that only one singleton object is used if mutliple 
   /// threads race to create one simultaneously.
   /// </summary>
   /// <typeparam name="T"></typeparam>
   public sealed class Singleton<T> where T: class {
      private SingletonRaceLoser m_raceLoser = SingletonRaceLoser.Block;

      /// <summary>
      /// A delegate that refers to a method that creates a singleton object.
      /// </summary>
      /// <returns>The singleton object.</returns>
      [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible")]
      public delegate T Creator();
      private Creator m_creator = null;

      private T m_value = null;

      /// <summary>
      /// Constructs a Singleton object that knows how to create a singleton object.
      /// </summary>
      /// <param name="raceLoser">Indicates how to arbitrate a race between multiple thread attempting to create the singleton object.</param>
      /// <param name="creator">Refers to the method that knows how to create the singleton object.</param>
      public Singleton(SingletonRaceLoser raceLoser, Creator creator) {
         m_raceLoser = raceLoser;
         m_creator = creator;
      }

      /// <summary>
      /// Returns the singleton object.
      /// </summary>
      public T Value {
         get {
            if (m_value != null) return m_value;
            Contract.Assume(m_creator != null);
            switch (m_raceLoser) {
               case SingletonRaceLoser.Block:
                  lock (m_creator) {
                     if (m_value == null) m_value = m_creator();
                  }
                  break;

               case SingletonRaceLoser.GC:
                  T val = m_creator();
                  if (Interlocked.CompareExchange(ref m_value, val, null) != null) {
                     IDisposable d = val as IDisposable;
                     if (d != null) d.Dispose();
                  }
                  break;
            }
            return m_value;
         }
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////
