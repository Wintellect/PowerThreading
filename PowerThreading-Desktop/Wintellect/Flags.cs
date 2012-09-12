/******************************************************************************
Module:  Flags.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;

///////////////////////////////////////////////////////////////////////////////


#if false
namespace Wintellect {
   internal static class Flags {
      public static Boolean IsSet(Int32 flags, Int32 flagToTest) {
         if (flagToTest == 0) throw new ArgumentOutOfRangeException("flagToTest", "Value must not be 0");
         return (flags & flagToTest) == flagToTest;
      }

      public static Boolean IsClear(Int32 flags, Int32 flagToTest) {
         if (flagToTest == 0) throw new ArgumentOutOfRangeException("flagToTest", "Value must not be 0");
         return !IsSet(flags, flagToTest);
      }

      public static Boolean AnyFlagsSet(Int32 flags, Int32 flagsToTest) {
         return ((flags & flagsToTest) != 0);
      }

      public static Int32 SetFlag(Int32 flags, Int32 flagsToSet) {
         return flags | flagsToSet;
      }
      public static Int32 ClearFlag(Int32 flags, Int32 flagsToClear) {
         return flags & ~flagsToClear;
      }

      public static Boolean IsExactlyOneBitSet(Int32 flags) {
         return ((flags != 0) && ((flags & (flags - 1)) == 0));
      }

      public static Int32 CountOnBits(Int32 flags) {
         Int32 BitsOn = 0;
         while (flags != 0) { BitsOn++; flags = flags & (flags - 1); }
         return (BitsOn);
      }

      public static void ForEachBit(Int32 flags, Predicate<Int32> predicate) {
         if (predicate == null) throw new ArgumentNullException("predicate");
         while (flags != 0) {
            Int32 decimalValue = flags;
            flags &= (flags - 1);
            decimalValue -= flags;
            // For example: 0xFF yields 1 2 4 8 16 32 64 128
            if (!predicate(decimalValue)) break;	// If predicate returns false, stop
         }
      }

      public static void ForEachBit(Int64 flags, Predicate<Int32> predicate) {
         if (predicate == null) throw new ArgumentNullException("predicate");
         while (flags != 0) {
            Int64 decimalValue = flags;
            flags &= (flags - 1);
            decimalValue -= flags;
            if (!predicate((Int32) decimalValue)) break;	// If predicate returns false, stop
         }
      }

      public static void ForEachBit(IntPtr flags, Predicate<Int32> predicate) {
         if (IntPtr.Size == 4) ForEachBit(flags.ToInt32(), predicate);
         else ForEachBit(flags.ToInt64(), predicate);
      }
   }
}
#endif


//////////////////////////////// End of File //////////////////////////////////}
