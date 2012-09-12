/******************************************************************************
Module:  NumaInformation.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.ComponentModel;
using System.Runtime.InteropServices;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading.LogicalProcessor {
   /// <summary>
   /// This class returns NUMA information about the host machine.
   /// </summary>
   public static class NumaInformation {
      /// <summary>
      /// A constant indicating that there is no preferred node.
      /// </summary>
      public const Int32 NoPreferredNode = -1;
      private static readonly Int32 s_highestNode;

      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1065:DoNotRaiseExceptionsInUnexpectedLocations")]
      static NumaInformation() {
         if (!NativeMethods.GetNumaHighestNodeNumber(out s_highestNode))
            throw new Win32Exception();
      }

      /// <summary>
      /// Retrieves the node that currently has the highest number.
      /// </summary>
      public static Int32 HighestNode { get { return s_highestNode; } }

      /// <summary>
      /// Retrieves the node number for the specified processor.
      /// </summary>
      /// <param name="processor">The processor number.</param>
      /// <returns>The node number.</returns>
      public static Int32 GetNodeFromProcessor(Int32 processor) {
         Byte nodeNumber;
         if (NativeMethods.GetNumaProcessorNode((Byte) processor, out nodeNumber)) return nodeNumber;
         throw new Win32Exception();
      }

      /// <summary>
      /// Retrieves the processor mask for the specified node.
      /// </summary>
      /// <param name="node">The node number.</param>
      /// <returns>The processor mask for the node. A processor mask is 
      /// a bit vector in which each bit represents a processor and 
      /// whether it is in the node.</returns>
      [CLSCompliant(false)]
      public static UInt64 GetProcessorsFromNode(Int32 node) {
         UInt64 mask;
         if (NativeMethods.GetNumaNodeProcessorMask((Byte) node, out mask)) return mask;
         throw new Win32Exception();
      }

      /// <summary>
      /// Retrieves the amount of memory available in the specified node.
      /// </summary>
      /// <param name="node">The numa node.</param>
      /// <returns>The amount of available memory for the node, in bytes.</returns>
      [CLSCompliant(false)]
      public static UInt64 GetAvailableMemoryOnNode(Int32 node) {
         UInt64 bytes;
         if (NativeMethods.GetNumaAvailableMemoryNode((Byte) node, out bytes)) return bytes;
         throw new Win32Exception();
      }

      /// <summary>
      /// Retrieves the node number for the specified proximity identifier.
      /// </summary>
      /// <param name="proximityId">The proximity identifier of the node.</param>
      /// <returns>The node number.</returns>
      public static Int32 ProximityNode(Int32 proximityId) {
         Byte nodeNumber;
         if (NativeMethods.GetNumaProximityNode(proximityId, out nodeNumber)) return nodeNumber;
         throw new Win32Exception();
      }

      private static class NativeMethods {
         [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean GetNumaHighestNodeNumber(out Int32 HighestNodeNumber);

         [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean GetNumaProcessorNode(Byte Processor, out Byte NodeNumber);

         [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean GetNumaNodeProcessorMask(Byte Node, out UInt64 ProcessorMask);

         [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean GetNumaAvailableMemoryNode(Byte Node, out UInt64 AvailableBytes);

         [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean GetNumaProximityNode(Int32 ProximityId, out Byte NodeNumber);
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////