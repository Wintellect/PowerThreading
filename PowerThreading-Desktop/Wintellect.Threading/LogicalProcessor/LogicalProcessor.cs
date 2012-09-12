/******************************************************************************
Module:  LogicalProcessor.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using HWND = System.IntPtr;
using System.Globalization;
using System.Diagnostics.Contracts;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.LogicalProcessor {
   /// <summary>
   /// This class exposes logical processor information about the host machine.
   /// </summary>
   public sealed class LogicalProcessorInformation {
      //private const Int32 c_LtpPCSmt = 1;

      /// <summary>
      /// Retrieves all of the logical processor information about the host machine.
      /// </summary>
      /// <returns>An array of LogicalProcessorInformation objects.</returns>
      [ContractVerification(false)]
      public static unsafe LogicalProcessorInformation[] GetLogicalProcessorInformation() {
         // Get the required buffer size
         Int32 length = 0;
         NativeMethods.GetLogicalProcessorInformation(null, ref length);
         Contract.Assume(length > 0);

         // Allocate the buffer & query the data
         Byte[] bytes = new Byte[length];
         fixed (Byte* pb = &bytes[0]) {
            Boolean ok = NativeMethods.GetLogicalProcessorInformation(pb, ref length);
            if (!ok) throw new Win32Exception();
         }

         List<LogicalProcessorInformation> list = new List<LogicalProcessorInformation>();

         for (Int32 elementIndex = 0; elementIndex < length; elementIndex += (2 * IntPtr.Size) + 16) {
            Int32 index = elementIndex;
            Int64 processorMask;
            if (IntPtr.Size == 4) processorMask = (Int64)BitConverter.ToInt32(bytes, index);
            else processorMask = BitConverter.ToInt64(bytes, index);
            index += IntPtr.Size;

            LogicalProcessorRelationship lpr;
            if (IntPtr.Size == 4) lpr = (LogicalProcessorRelationship)BitConverter.ToInt32(bytes, index);
            else lpr = (LogicalProcessorRelationship)BitConverter.ToInt64(bytes, index);
            index += IntPtr.Size;

            LogicalProcessorInformation lpi = null;
            switch (lpr) {
               case LogicalProcessorRelationship.Cache:
                  CacheLevel level = (CacheLevel)bytes[index++];
                  CacheAssociativity associativity = (CacheAssociativity)bytes[index++];
                  Int16 lineSize = BitConverter.ToInt16(bytes, index); index += 2;
                  Int32 size = BitConverter.ToInt32(bytes, index); index += 4;
                  ProcessorCacheType type = (ProcessorCacheType)BitConverter.ToInt32(bytes, index);
                  lpi = new LogicalProcessorInformation(processorMask, lpr, new CacheDescriptor(level, associativity, lineSize, size, type));
                  break;

               case LogicalProcessorRelationship.NumaNode:
                  lpi = new LogicalProcessorInformation(processorMask, lpr, BitConverter.ToInt32(bytes, index));
                  break;

               case LogicalProcessorRelationship.ProcessorCore:
                  lpi = new LogicalProcessorInformation(processorMask, lpr, bytes[index]);
                  break;

               case LogicalProcessorRelationship.ProcessorPackage:
                  lpi = new LogicalProcessorInformation(processorMask, lpr);
                  break;
            }
            list.Add(lpi);
         }
         return list.ToArray();
      }

      internal LogicalProcessorInformation(Int64 processorMask, LogicalProcessorRelationship relationship) {
         m_processorMask = processorMask;
         m_relationship = relationship;
      }
      internal LogicalProcessorInformation(Int64 processorMask, LogicalProcessorRelationship relationship, CacheDescriptor cacheDescriptor)
         : this(processorMask, relationship) {
         m_cacheDescriptor = cacheDescriptor;
      }
      internal LogicalProcessorInformation(Int64 processorMask, LogicalProcessorRelationship relationship, Int32 numaNode)
         : this(processorMask, relationship) {
         m_numaNode = numaNode;
      }
      internal LogicalProcessorInformation(Int64 processorMask, LogicalProcessorRelationship relationship, Byte processorCore)
         : this(processorMask, relationship) {
         m_processorCore = processorCore;
      }

      private Int64 m_processorMask;  // bit mask

      /// <summary>
      /// Returns a bitmask indicating the logical processors associated with this physical processor.
      /// </summary>
      public Int64 ProcessorMask { get { return m_processorMask; } }

      private LogicalProcessorRelationship m_relationship;

      /// <summary>
      /// Returns the 
      /// </summary>
      public LogicalProcessorRelationship Relationship { get { return m_relationship; } }

      private Byte m_processorCore;
      /// <summary>
      /// This structure contains valid data only if the Relationship member is RelationProcessorCore.
      /// If the value of this member is 1, the logical processors identified by the value of the ProcessorMask member share functional units, as in hyper-threading or SMT. 
      /// Otherwise, the identified logical processors do not share functional units. 
      /// </summary>
      public Byte ProcessorCore {
         get {
            VerifyRelationship(LogicalProcessorRelationship.ProcessorCore);
            return m_processorCore;
         }
      }

      private Int32 m_numaNode;
      /// <summary>
      /// This structure contains valid data only if the Relationship member is RelationNumaNode.
      /// Identifies the NUMA node. The valid values of this parameter are 0 to the highest NUMA node number inclusive. 
      /// A non-NUMA multiprocessor system will report that all processors belong to one NUMA node.
      /// </summary>
      public Int32 NumaNode {
         get {
            VerifyRelationship(LogicalProcessorRelationship.NumaNode);
            return m_numaNode;
         }
      }

      private CacheDescriptor m_cacheDescriptor;
      /// <summary>
      /// This property contains valid data only if the Relationship member is RelationCache.
      /// There is one record returned for each cache reported. Some or all caches may not be reported. 
      /// Therefore, do not assume the absence of any particular caches. Caches are not necessarily shared among logical processors.
      /// </summary>
      public CacheDescriptor CacheDescriptor {
         get {
            VerifyRelationship(LogicalProcessorRelationship.Cache);
            return m_cacheDescriptor;
         }
      }

      private void VerifyRelationship(LogicalProcessorRelationship relationship) {
         if (Relationship == relationship) return;
         throw new InvalidOperationException("Property not valid for this relationship.");
      }

      /// <summary>
      /// Returns a string representing the state of the object.
      /// </summary>
      /// <returns>The string representing the state of the object.</returns>
      public override string ToString() {
         StringBuilder sb = new StringBuilder();
         sb.AppendFormat("Mask={0}", ProcessorMask);
         switch (Relationship) {
            case LogicalProcessorRelationship.ProcessorCore:
               sb.AppendFormat(", ProcessorCore={0}", ProcessorCore);
               break;
            case LogicalProcessorRelationship.NumaNode:
               sb.AppendFormat(", NumaNode={0}", NumaNode);
               break;
            case LogicalProcessorRelationship.Cache:
               sb.AppendFormat(", Cache={0}", CacheDescriptor);
               break;
            case LogicalProcessorRelationship.ProcessorPackage:
               sb.Append(", Package");
               break;
         }
         return sb.ToString();
      }

      private static class NativeMethods {
         [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal unsafe static extern Boolean GetLogicalProcessorInformation(Byte* buffer, ref Int32 returnedLength);
      }
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.LogicalProcessor {
   /// <summary>
   /// Describes the processor's cache characteristics.
   /// </summary>
   public struct CacheDescriptor : IEquatable<CacheDescriptor> {
      internal CacheDescriptor(CacheLevel level, CacheAssociativity associativity, Int16 lineSize, Int32 size, ProcessorCacheType type) {
         m_level = level;
         m_associativity = associativity;
         m_lineSize = lineSize;
         m_size = size;
         m_type = type;
      }

      /// <summary>
      /// Returns a string representing the state of the object.
      /// </summary>
      /// <returns></returns>
      public override String ToString() {
         return String.Format(CultureInfo.InvariantCulture, "Level={0}, Associativity={1}, LineSize={2}, Size={3:N0}, Type={4}",
            Level, Associativity, LineSize, Size, Type);
      }

      private readonly CacheLevel m_level;

      /// <summary>
      /// The cache level. This member can currently be one of the following values; other values may be supported in the future.Value Meaning 1=L1, 2=L2, 3=L3 
      /// </summary>
      public CacheLevel Level { get { return m_level; } }

      private readonly CacheAssociativity m_associativity;

      /// <summary>
      /// The cache associativity. If this member is CACHE_FULLY_ASSOCIATIVE (0xFF), the cache is fully associative.
      /// </summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Associativity")]
      public CacheAssociativity Associativity { get { return m_associativity; } }

      private readonly Int16 m_lineSize;

      /// <summary>The cache line size, in bytes.</summary>
      public Int16 LineSize { get { return m_lineSize; } }

      private readonly Int32 m_size;

      /// <summary>The cache size, in bytes.</summary>
      public Int32 Size { get { return m_size; } }

      private readonly ProcessorCacheType m_type;

      /// <summary>The cache type.</summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods")]
      public ProcessorCacheType Type { get { return m_type; } }

      /// <summary>
      /// Determines whether the specified Object is equal to the current Object. 
      /// </summary>
      /// <param name="obj">The Object to compare with the current Object.</param>
      /// <returns>true if the specified Object is equal to the current Object; otherwise, false.</returns>
      public override Boolean Equals(Object obj) {
         if (obj is CacheDescriptor) return this.Equals((CacheDescriptor)obj);
         return false;
      }
      
      /// <summary>
      /// Serves as a hash function for a particular type.
      /// </summary>
      /// <returns>A hash code for the current Object.</returns>
      public override int GetHashCode() {
         return base.GetHashCode();
      }

      /// <summary>
      /// Determines whether the specified CacheDescriptor is equal to the current CacheDescriptor. 
      /// </summary>
      /// <param name="other">The CacheDescriptor to compare with the current CacheDescriptor.</param>
      /// <returns>true if the specified CacheDescriptor is equal to the current CacheDescriptor; otherwise, false.</returns>
      public Boolean Equals(CacheDescriptor other) {
         return (m_associativity == other.m_associativity) && (m_level == other.m_level) &&
             (m_lineSize == other.m_lineSize) && (m_size == other.m_size) && (m_type == other.m_type);
      }

      /// <summary>
      /// Determines if two CacheDescriptors are equal to each other. 
      /// </summary>
      /// <param name="cd1">The first CacheDescriptor to compare.</param>
      /// <param name="cd2">The second CacheDescriptor to compare.</param>
      /// <returns>true if the two CacheDescriptors are equal; otherwise, false.</returns>
      public static Boolean operator ==(CacheDescriptor cd1, CacheDescriptor cd2) {
         return cd1.Equals(cd2);
      }

      /// <summary>
      /// Determines if two CacheDescriptors are not equal to each other. 
      /// </summary>
      /// <param name="cd1">The first CacheDescriptor to compare.</param>
      /// <param name="cd2">The second CacheDescriptor to compare.</param>
      /// <returns>true if the two CacheDescriptors are not equal; otherwise, false.</returns>
      public static Boolean operator !=(CacheDescriptor cd1, CacheDescriptor cd2) {
         return !cd1.Equals(cd2);
      }
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.LogicalProcessor {
   /// <summary>
   /// Indicates the cache associativity.
   /// </summary>
   [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Associativity"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1028:EnumStorageShouldBeInt32")]
   public enum CacheAssociativity : byte { 
      /// <summary>
      /// The cache is not associative.
      /// </summary>
      None = 0, 

      /// <summary>
      /// The is fully associative.
      /// </summary>
      CacheFullyAssociative = 0xff }
}

///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.LogicalProcessor {
   /// <summary>
   /// Flags indicating what a set of logical processors share with each other.
   /// </summary>
   [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1008:EnumsShouldHaveZeroValue")]
   public enum LogicalProcessorRelationship : int {
      /// <summary>
      /// The specified logical processors share a single processor core. The ProcessorCore member contains additional information.
      /// </summary>
      ProcessorCore = 0,

      /// <summary>
      /// The specified logical processors are part of the same NUMA node. The NumaNode member contains additional information.
      /// </summary>
      NumaNode = 1,

      /// <summary>
      /// The specified logical processors share a cache. The Cache member contains additional information.
      /// </summary>
      Cache = 2,

      /// <summary>
      /// The specified logical processors share a physical package. There is no additional information available.
      /// </summary>
      ProcessorPackage = 3,

      /// <summary>
      /// The specified logical processors share a procesor group. There is no additional information available.
      /// </summary>
      Group = 4
   }
}

///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.LogicalProcessor {
   /// <summary>
   /// Indicates the cache level.
   /// </summary>
   [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1028:EnumStorageShouldBeInt32")]
   public enum CacheLevel : byte { 
      /// <summary>
      /// None.
      /// </summary>
      None = 0, 
      
      /// <summary>
      /// Level 1 cache.
      /// </summary>
      L1 = 1, 
      
      /// <summary>
      /// Level 2 cache.
      /// </summary>
      L2 = 2, 
      
      /// <summary>
      /// Level 3 cache.
      /// </summary>
      L3 = 3 
   }
}

///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.LogicalProcessor {
   /// <summary>
   /// Indicates the processor cache type.
   /// </summary>
   public enum ProcessorCacheType : int {
      /// <summary>The cache is unified.</summary>
      Unified,

      /// <summary>The cache is for processor instructions.</summary>
      Instruction,

      /// <summary>The cache is for data.</summary>
      Data,

      /// <summary>The cache is for traces.</summary>
      Trace
   }
}



//////////////////////////////// End of File //////////////////////////////////