/******************************************************************************
Module:  InterlockedEx.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Threading;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading {
   ///<summary>Provides atomic operations for variables that are shared by multiple threads.</summary>
   [SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix"), DebuggerStepThrough, CLSCompliant(false)]
   public static class InterlockedEx {
      #region Generic Morph and Morpher
      /// <summary>Identifies a method that morphs the Int32 startValue into a new value, returning it.</summary>
      /// <typeparam name="TResult">The return type returned by the Morph method.</typeparam>
      /// <typeparam name="TArgument">The argument type passed to the Morph method.</typeparam>
      /// <param name="startValue">The initial Int32 value.</param>
      /// <param name="argument">The argument passed to the method.</param>
      /// <param name="morphResult">The value returned from Morph when the morpher callback method is successful.</param>
      /// <returns>The value that the morpher method desires to set the <paramref name="startValue"/> to.</returns>
      [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Morpher")]
      public delegate Int32 Morpher<TResult, TArgument>(Int32 startValue, TArgument argument, out TResult morphResult);

      /// <summary>Atomically modifies an Int32 value using an algorithm identified by <paramref name="morpher"/>.</summary>
      /// <typeparam name="TResult">The type of the return value.</typeparam>
      /// <typeparam name="TArgument">The type of the argument passed to the <paramref name="morpher"/> callback method.</typeparam>
      /// <param name="target">A reference to the Int32 value that is to be modified atomically.</param>
      /// <param name="argument">A value of type <typeparamref name="TArgument"/> that will be passed on to the <paramref name="morpher"/> callback method.</param>
      /// <param name="morpher">The algorithm that modifies the Int32 returning a new Int32 value and another return value to be returned to the caller.</param>
      /// <returns>The desired Int32 value.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "morpher"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static TResult Morph<TResult, TArgument>(ref Int32 target, TArgument argument, Morpher<TResult, TArgument> morpher) {
         Contract.Requires(morpher != null);
         TResult morphResult;
         Int32 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, morpher(i, argument, out morphResult), i);
         } while (i != j);
         return morphResult;
      }
      #endregion

      #region Convenience Wrappers
      ///<summary>Compares two values for equality and, if they are equal, replaces one of the values.</summary>
      ///<return>Returns true if the value in <paramref name="value"/> was equal the the value of <paramref name="if"/>.</return>
      ///<param name="value">The destination, whose value is compared with <paramref name="if"/> and possibly replaced with <paramref name="then"/>.</param>
      ///<param name="if">The value that is compared to the value at <paramref name="value"/>.</param>
      ///<param name="then">The value that might get placed into <paramref name="value"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Boolean IfThen(ref Int32 value, Int32 @if, Int32 then) {
         return (Interlocked.CompareExchange(ref value, then, @if) == @if);
      }

#if !SILVERLIGHT && !PocketPC
      ///<summary>Compares two values for equality and, if they are equal, replaces one of the values.</summary>
      ///<return>Returns true if the value in <paramref name="value"/> was equal the the value of <paramref name="if"/>.</return>
      ///<param name="value">The destination, whose value is compared with <paramref name="if"/> and possibly replaced with <paramref name="then"/>.</param>
      ///<param name="if">The value that is compared to the value at <paramref name="value"/>.</param>
      ///<param name="then">The value that might get placed into <paramref name="value"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Boolean IfThen(ref UInt32 value, UInt32 @if, UInt32 then) {
         return (InterlockedEx.CompareExchange(ref value, then, @if) == @if);
      }
#endif

      ///<summary>Compares two values for equality and, if they are equal, replaces one of the values.</summary>
      ///<remarks>The previous value in <paramref name="value"/> is returned in <paramref name="previousValue"/>.</remarks>
      ///<return>Returns true if the value in <paramref name="value"/> was equal the the value of <paramref name="if"/>.</return>
      ///<param name="value">The destination, whose value is compared with <paramref name="if"/> and possibly replaced with <paramref name="then"/>.</param>
      ///<param name="if">The value that is compared to the value at <paramref name="value"/>.</param>
      ///<param name="then">The value that might get placed into <paramref name="value"/>.</param>
      ///<param name="previousValue">The previous value that was in <paramref name="value"/> prior to calling this method.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#"), SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters")]
      public static Boolean IfThen(ref Int32 value, Int32 @if, Int32 then, out Int32 previousValue) {
         previousValue = Interlocked.CompareExchange(ref value, then, @if);
         return (previousValue == @if);
      }

#if !SILVERLIGHT && !PocketPC
      ///<summary>Compares two values for equality and, if they are equal, replaces one of the values.</summary>
      ///<remarks>The previous value in <paramref name="value"/> is returned in <paramref name="previousValue"/>.</remarks>
      ///<return>Returns true if the value in <paramref name="value"/> was equal the the value of <paramref name="if"/>.</return>
      ///<param name="value">The destination, whose value is compared with <paramref name="if"/> and possibly replaced with <paramref name="then"/>.</param>
      ///<param name="if">The value that is compared to the value at <paramref name="value"/>.</param>
      ///<param name="then">The value that might get placed into <paramref name="value"/>.</param>
      ///<param name="previousValue">The previous value that was in <paramref name="value"/> prior to calling this method.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#"), SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters")]
      public static Boolean IfThen(ref UInt32 value, UInt32 @if, UInt32 then, out UInt32 previousValue) {
         previousValue = InterlockedEx.CompareExchange(ref value, then, @if);
         return (previousValue == @if);
      }
#endif

      ///<summary>Compares two values for equality and, if they are equal, replaces one of the values.</summary>
      ///<return>Returns true if the value in <paramref name="value"/> was equal the the value of <paramref name="if"/>.</return>
      ///<typeparam name="T">The type to be used for value, if, and then. This type must be a reference type.</typeparam>
      ///<param name="value">The destination, whose value is compared with <paramref name="if"/> and possibly replaced with <paramref name="then"/>.</param>
      ///<param name="if">The value that is compared to the value at <paramref name="value"/>.</param>
      ///<param name="then">The value that might get placed into <paramref name="value"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Boolean IfThen<T>(ref T value, T @if, T then) where T : class {
         return (Interlocked.CompareExchange(ref value, then, @if) == @if);
      }

      ///<summary>Compares two values for equality and, if they are equal, replaces one of the values.</summary>
      ///<remarks>The previous value in <paramref name="value"/> is returned in <paramref name="previousValue"/>.</remarks>
      ///<return>Returns true if the value in <paramref name="value"/> was equal the the value of <paramref name="if"/>.</return>
      ///<typeparam name="T">The type to be used for value, if, and then. This type must be a reference type.</typeparam>
      ///<param name="value">The destination, whose value is compared with <paramref name="if"/> and possibly replaced with <paramref name="then"/>.</param>
      ///<param name="if">The value that is compared to the value at <paramref name="value"/>.</param>
      ///<param name="then">The value that might get placed into <paramref name="value"/>.</param>
      ///<param name="previousValue">The previous value that was in <paramref name="value"/> prior to calling this method.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#"), SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters")]
      public static Boolean IfThen<T>(ref T value, T @if, T then, out T previousValue) where T : class {
         previousValue = Interlocked.CompareExchange(ref value, then, @if);
         return (previousValue == @if);
      }

#if !SILVERLIGHT && !PocketPC
      /// <summary>Compares two 32-bit unsigned integers for equality and, if they are equal, replaces one of the values.</summary>
      /// <remarks>If comparand and the value in location are equal, then value is stored in location. Otherwise, no operation is performed. The compare and exchange operations are performed as an atomic operation. The return value of CompareExchange is the original value in location, whether or not the exchange takes place.</remarks>
      /// <param name="location">The destination, whose value is compared with comparand and possibly replaced.</param>
      /// <param name="value">The value that replaces the destination value if the comparison results in equality.</param>
      /// <param name="comparand">The value that is compared to the value at <paramref name="location"/>.</param>
      /// <returns>The original value in location.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static UInt32 CompareExchange(ref UInt32 location, UInt32 value, UInt32 comparand) {
         unsafe {
            fixed (UInt32* p = &location) {
               Int32* p2 = (Int32*)p;
               return (UInt32)Interlocked.CompareExchange(ref *p2, (Int32)value, (Int32)comparand);
            }
         }
      }
#endif

#if !SILVERLIGHT && !PocketPC
      /// <summary>Sets a 32-bit unsigned integer to a specified value and returns the original value, as an atomic operation.</summary>
      /// <param name="location">The variable to set to the specified value.</param>
      /// <param name="value">The value to which the <paramref name="location"/> parameter is set.</param>
      /// <returns>The original value of <paramref name="location"/>.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static UInt32 Exchange(ref UInt32 location, UInt32 value) {
         unsafe {
            fixed (UInt32* p = &location) {
               Int32* p2 = (Int32*)p;
               return (UInt32)Interlocked.Exchange(ref *p2, (Int32)value);
            }
         }
      }
#endif
      #endregion

      #region Mathematic Operations
#if !SILVERLIGHT && !PocketPC
      /// <summary>Adds a 32-bit signed integer to a 32-bit unsigned integer and replaces the first integer with the sum, as an atomic operation.</summary>
      /// <param name="location">A variable containing the first value to be added. The sum of the two values is stored in <paramref name="location"/>.</param>
      /// <param name="value">The value to be added to the integer at <paramref name="location"/>.</param>
      /// <returns>The new value stored at <paramref name="location"/>.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static UInt32 Add(ref UInt32 location, Int32 value) {
         unsafe {
            fixed (UInt32* p = &location) {
               Int32* p2 = (Int32*)p;
               return (UInt32)Interlocked.Add(ref *p2, value);
            }
         }
      }
#endif

#if PocketPC
      /// <summary>Adds a 32-bit signed integer to a 32-bit signed integer and replaces the first integer with the sum, as an atomic operation.</summary>
      /// <param name="target">A variable containing the first value to be added. The sum of the two values is stored in <paramref name="target"/>.</param>
      /// <param name="value">The value to be added to the integer at <paramref name="target"/>.</param>
      /// <returns>The new value stored at <paramref name="target"/>.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 Add(ref Int32 target, Int32 value) {
         Int32 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, i + value, i);
         } while (i != j);
         return j;
      }
#endif

#if !SILVERLIGHT && !PocketPC
      /// <summary>Increments a specified variable and stores the result, as an atomic operation.</summary>
      /// <param name="location">The variable whose value is to be incremented.</param>
      /// <returns>The incremented value.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static UInt32 Increment(ref UInt32 location) { return Add(ref location, 1); }


      /// <summary>Decrements a specified variable and stores the result, as an atomic operation.</summary>
      /// <param name="location">The variable whose value is to be decremented.</param>
      /// <returns>The decremented value.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static UInt32 Decrement(ref UInt32 location) { return Add(ref location, -1); }
#endif

      ///<summary>Increases a value to a new value if the new value is larger.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the value that might be increased to a new maximum.</param>
      ///<param name="value">The value that if larger than <paramref name="target"/> will be placed in <paramref name="target"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 Max(ref Int32 target, Int32 value) {
         Int32 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, Math.Max(i, value), i);
         } while (i != j);
         return j;
      }

      ///<summary>Increases a value to a new value if the new value is larger.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the value that might be increased to a new maximum.</param>
      ///<param name="value">The value that if larger than <paramref name="target"/> will be placed in <paramref name="target"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int64 Max(ref Int64 target, Int64 value)
      {
          Int64 i, j = target;
          do
          {
              i = j;
              j = Interlocked.CompareExchange(ref target, Math.Max(i, value), i);
          } while (i != j);
          return j;
      }

      ///<summary>Decreases a value to a new value if the new value is smaller.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the value that might be decreased to a new minimum.</param>
      ///<param name="value">The value that if smaller than <paramref name="target"/> will be placed in <paramref name="target"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 Min(ref Int32 target, Int32 value) {
         Int32 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, Math.Min(i, value), i);
         } while (i != j);
         return j;
      }

      ///<summary>Decreases a value to a new value if the new value is smaller.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the value that might be decreased to a new minimum.</param>
      ///<param name="value">The value that if smaller than <paramref name="target"/> will be placed in <paramref name="target"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int64 Min(ref Int64 target, Int64 value) {
         Int64 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, Math.Min(i, value), i);
         } while (i != j);
         return j;
      }
      
      ///<summary>Decrements a value by 1 if the value is greater than the specified value (usually 0).</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the value that might be decremented.</param>
      ///<param name="lowValue">The value that target must be greater than in order for the decrement to occur.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 DecrementIfGreaterThan(ref Int32 target, Int32 lowValue) {
         Int32 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, (i > lowValue) ? (i - 1) : i, i);
         } while (i != j);
         return j;
      }


      /// <summary>Decrements a value by 1 if the value is greater than 0.</summary>
      /// <param name="target">A variable containing the value that might be decremented.</param>
      /// <param name="value">The value to add to target before calculating the modulo specified in <paramref name="modulo"/>.</param>
      /// <param name="modulo">The value to use for the modulo operation.</param>
      /// <returns>Returns the previous value of <paramref name="target"/>.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 AddModulo(ref Int32 target, Int32 value, Int32 modulo) {
         Contract.Requires(modulo != 0);
         Int32 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, (i + value) % modulo, i);
         } while (i != j);
         return j;
      }
      #endregion

      #region Bit Operations
      ///<summary>Bitwise ANDs two 32-bit integers and replaces the first integer with the ANDed value, as an atomic operation.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the first value to be ANDed. The bitwise AND of the two values is stored in <paramref name="target"/>.</param>
      ///<param name="with">The value to AND with <paramref name="target"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 And(ref Int32 target, Int32 with) {
         Int32 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, i & with, i);
         } while (i != j);
         return j;
      }

      ///<summary>Bitwise ORs two 32-bit integers and replaces the first integer with the ORed value, as an atomic operation.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the first value to be ORed. The bitwise OR of the two values is stored in <paramref name="target"/>.</param>
      ///<param name="with">The value to OR with <paramref name="target"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 Or(ref Int32 target, Int32 with) {
         Int32 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, i | with, i);
         } while (i != j);
         return j;
      }

#if !PocketPC
      ///<summary>Bitwise ORs two 64-bit signed integers and replaces the first integer with the ORed value, as an atomic operation.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the first value to be ORed. The bitwise OR of the two values is stored in <paramref name="target"/>.</param>
      ///<param name="with">The value to OR with <paramref name="target"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int64 Or(ref Int64 target, Int64 with) {
         Int64 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, i | with, i);
         } while (i != j);
         return j;
      }

      ///<summary>Bitwise XORs two 32-bit integers and replaces the first integer with the XORed value, as an atomic operation.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the first value to be XORed. The bitwise XOR of the two values is stored in <paramref name="target"/>.</param>
      ///<param name="with">The value to XOR with <paramref name="target"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 Xor(ref Int32 target, Int32 with) {
         Int32 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, i ^ with, i);
         } while (i != j);
         return j;
      }

      ///<summary>Turns a bit on and returns whether or not it was on.</summary>
      ///<return>Returns whether the bit was on prior to calling this method.</return>
      ///<param name="target">A variable containing the value that is to have a bit turned on.</param>
      ///<param name="bitNumber">The bit (0-31) in <paramref name="target"/> that should be turned on.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Boolean BitTestAndSet(ref Int32 target, Int32 bitNumber) {
         Int32 tBit = unchecked((Int32)(1u << bitNumber));
         // Turn the bit on and return if it was on
         return (Or(ref target, tBit) & tBit) != 0;
      }

      ///<summary>Turns a bit off and returns whether or not it was on.</summary>
      ///<return>Returns whether the bit was on prior to calling this method.</return>
      ///<param name="target">A variable containing the value that is to have a bit turned off.</param>
      ///<param name="bitNumber">The bit (0-31) in <paramref name="target"/> that should be turned off.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Boolean BitTestAndReset(ref Int32 target, Int32 bitNumber) {
         Int32 tBit = unchecked((Int32)(1u << bitNumber));
         // Turn the bit off and return if it was on
         return (And(ref target, ~tBit) & tBit) != 0;
      }

      ///<summary>Flips an on bit off or and off bit on.</summary>
      ///<return>Returns whether the bit was on prior to calling this method.</return>
      ///<param name="target">A variable containing the value that is to have a bit flipped.</param>
      ///<param name="bitNumber">The bit (0-31) in <paramref name="target"/> that should be flipped.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Boolean BitTestAndCompliment(ref Int32 target, Int32 bitNumber) {
         Int32 tBit = unchecked((Int32)(1u << bitNumber));
         // Toggle the bit and return if it was on
         return (Xor(ref target, tBit) & tBit) != 0;
      }
#endif
      #endregion

      #region Masked Bit Operations
      ///<summary>Bitwise ANDs two 32-bit integers with a mask replacing the first integer with the ANDed value, as an atomic operation.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the first value to be ANDed. The bitwise AND of the two values is stored in <paramref name="target"/>.</param>
      ///<param name="with">The value to AND with <paramref name="target"/>.</param>
      ///<param name="mask">The value to AND with <paramref name="target"/> prior to ANDing with <paramref name="with"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 MaskedAnd(ref Int32 target, Int32 with, Int32 mask) {
         Int32 i, j = target;
         do {
            i = j & mask;  // Mask off the bits we're not interested in
            j = Interlocked.CompareExchange(ref target, i & with, i);
         } while (i != j);
         return j;
      }

      ///<summary>Bitwise ORs two 32-bit integers with a mask replacing the first integer with the ORed value, as an atomic operation.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the first value to be ORed. The bitwise OR of the two values is stored in <paramref name="target"/>.</param>
      ///<param name="with">The value to OR with <paramref name="target"/>.</param>
      ///<param name="mask">The value to AND with <paramref name="target"/> prior to ORing with <paramref name="with"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 MaskedOr(ref Int32 target, Int32 with, Int32 mask) {
         Int32 i, j = target;
         do {
            i = j & mask;  // Mask off the bits we're not interested in
            j = Interlocked.CompareExchange(ref target, i | with, i);
         } while (i != j);
         return j;
      }

      ///<summary>Bitwise XORs two 32-bit integers with a mask replacing the first integer with the XORed value, as an atomic operation.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the first value to be XORed. The bitwise XOR of the two values is stored in <paramref name="target"/>.</param>
      ///<param name="with">The value to XOR with <paramref name="target"/>.</param>
      ///<param name="mask">The value to AND with <paramref name="target"/> prior to XORing with <paramref name="with"/>.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 MaskedXor(ref Int32 target, Int32 with, Int32 mask) {
         Int32 i, j = target;
         do {
            i = j & mask;  // Mask off the bits we're not interested in
            j = Interlocked.CompareExchange(ref target, i ^ with, i);
         } while (i != j);
         return j;
      }

      ///<summary>Sets a variable to a specified value as an atomic operation.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the value to be replaced.</param>
      ///<param name="mask">The bits to leave unaffected in <paramref name="target"/> prior to ORing with <paramref name="value"/>.</param>
      ///<param name="value">The value to replace <paramref name="target"/> with.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 MaskedExchange(ref Int32 target, Int32 mask, Int32 value) {
         Int32 i, j = target;
         do {
            i = j;
            j = Interlocked.CompareExchange(ref target, (i & ~mask) | value, j);
         } while (i != j);
         return j;
      }

      ///<summary>Adds two integers and replaces the first integer with the sum, as an atomic operation.</summary>
      ///<return>Returns the previous value of <paramref name="target"/>.</return>
      ///<param name="target">A variable containing the value to be replaced.</param>
      ///<param name="value">The value to add to <paramref name="target"/>.</param>
      ///<param name="mask">The bits in <paramref name="target"/> that should not be affected by adding.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1045:DoNotPassTypesByReference", MessageId = "0#")]
      public static Int32 MaskedAdd(ref Int32 target, Int32 value, Int32 mask) {
         Int32 i, j = target;
         do {
            i = j & mask;  // Mask off the bits we're not interested in
            j = Interlocked.CompareExchange(ref target, i + value, i);
         } while (i != j);
         return j;
      }
      #endregion
   }
}


//////////////////////////////// End of File //////////////////////////////////