#if !SILVERLIGHT && !PocketPC
using System;
using System.Collections.Generic;
using System.Text;

namespace System.Diagnostics.Contracts {
   [Conditional("CONTRACTS_FULL"), AttributeUsage(AttributeTargets.Property | AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Struct | AttributeTargets.Class | AttributeTargets.Assembly)]
   internal sealed class ContractVerificationAttribute : Attribute {
      // Fields
      private bool _value;

      // Methods
      public ContractVerificationAttribute(bool value) {
         this._value = value;
      }

      // Properties
      public bool Value {
         get {
            return this._value;
         }
      }
   }

   [Conditional("CONTRACTS_FULL"), AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
   internal sealed class ContractInvariantMethodAttribute : Attribute {
   }

 
   internal static class Contract {
      [Conditional("DEBUG"), Conditional("CONTRACTS_FULL")]
      public static void Assert(bool condition) { }

      [Conditional("CONTRACTS_FULL"), Conditional("DEBUG")]
      public static void Assert(bool condition, string userMessage) { }

      [Conditional("CONTRACTS_FULL"), Conditional("DEBUG")]
      public static void Assume(bool condition) { 
         Debug.Assert(condition);
      }

      [Conditional("CONTRACTS_FULL"), Conditional("DEBUG")]
      public static void Assume(bool condition, string userMessage) { 
         Debug.Assert(condition);
}

      [Conditional("CONTRACTS_FULL")]
      public static void EndContractBlock() { }

      [Conditional("CONTRACTS_FULL")]
      public static void Ensures(bool condition) { }

      [Conditional("CONTRACTS_FULL")]
      public static void Ensures(bool condition, string userMessage) { }

      [Conditional("CONTRACTS_FULL")]
      public static void EnsuresOnThrow<TException>(bool condition) where TException : Exception { }

      [Conditional("CONTRACTS_FULL")]
      public static void EnsuresOnThrow<TException>(bool condition, string userMessage) where TException : Exception { }

      public static bool Exists<T>(IEnumerable<T> collection, Predicate<T> predicate) { return true; }

      public static bool Exists(int fromInclusive, int toExclusive, Predicate<int> predicate) { return true; }

      public static bool ForAll<T>(IEnumerable<T> collection, Predicate<T> predicate) { return true; }

      public static bool ForAll(int fromInclusive, int toExclusive, Predicate<int> predicate) { return true; }

      [Conditional("CONTRACTS_FULL")]
      public static void Invariant(bool condition) { }
      
      [Conditional("CONTRACTS_FULL")]
      public static void Invariant(bool condition, string userMessage) {}
      
      public static T OldValue<T>(T value) { return value; }

      [Conditional("CONTRACTS_FULL")]
      public static void Requires(bool condition) {}

      public static void Requires<TException>(bool condition) where TException : Exception {}
      
      [Conditional("CONTRACTS_FULL")]
      public static void Requires(bool condition, string userMessage) {}
      
      public static void Requires<TException>(bool condition, string userMessage) where TException : Exception {}
      
      public static T Result<T>() { return default(T); }
      
      public static T ValueAtReturn<T>(out T value) { value = default(T); return value; }
   }
}
#endif