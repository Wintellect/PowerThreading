#if false
using System;
using System.Runtime.InteropServices;
using System.Runtime.ConstrainedExecution;

namespace PrivilegeClass {
   [Flags]
   public enum TokenAccessLevels {
      AssignPrimary = 0x00000001,
      Duplicate = 0x00000002,
      Impersonate = 0x00000004,
      Query = 0x00000008,
      QuerySource = 0x00000010,
      AdjustPrivileges = 0x00000020,
      AdjustGroups = 0x00000040,
      AdjustDefault = 0x00000080,
      AdjustSessionId = 0x00000100,

      Read = 0x00020000 | Query,

      Write = 0x00020000 | AdjustPrivileges | AdjustGroups | AdjustDefault,

      AllAccess = 0x000F0000 |
          AssignPrimary |
          Duplicate |
          Impersonate |
          Query |
          QuerySource |
          AdjustPrivileges |
          AdjustGroups |
          AdjustDefault |
          AdjustSessionId,

      MaximumAllowed = 0x02000000
   }

   public enum SecurityImpersonationLevel {
      Anonymous = 0,
      Identification = 1,
      Impersonation = 2,
      Delegation = 3,
   }

   public enum TokenType {
      Primary = 1,
      Impersonation = 2,
   }

   internal static class NativeMethods {
      private const uint SE_PRIVILEGE_DISABLED = 0x00000000;
      private const uint SE_PRIVILEGE_ENABLED = 0x00000002;

      [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
      private struct LUID {
         internal uint LowPart;
         internal uint HighPart;
      }

      [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
      private struct LUID_AND_ATTRIBUTES {
         internal LUID Luid;
         internal uint Attributes;
      }

      [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
      private struct TOKEN_PRIVILEGE {
         internal uint PrivilegeCount;
         internal LUID_AND_ATTRIBUTES Privilege;
      }

      private const string ADVAPI32 = "advapi32.dll";
      internal const string KERNEL32 = "kernel32.dll";

      private const int ERROR_SUCCESS = 0x0;
      private const int ERROR_ACCESS_DENIED = 0x5;
      private const int ERROR_NOT_ENOUGH_MEMORY = 0x8;
      private const int ERROR_NO_TOKEN = 0x3f0;
      private const int ERROR_NOT_ALL_ASSIGNED = 0x514;
      private const int ERROR_NO_SUCH_PRIVILEGE = 0x521;
      private const int ERROR_CANT_OPEN_ANONYMOUS = 0x543;

      [DllImport(KERNEL32, ExactSpelling = true, SetLastError = true)]
      [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
      private static extern Boolean CloseHandle(IntPtr handle);

      [DllImport(ADVAPI32, CharSet = CharSet.Unicode, SetLastError = true)]
      [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
      private static extern Boolean AdjustTokenPrivileges(SafeTokenHandle TokenHandle,
          Boolean DisableAllPrivileges, [In] ref TOKEN_PRIVILEGE NewState, UInt32 BufferLength,
          [In, Out] ref TOKEN_PRIVILEGE PreviousState, [In, Out] ref uint ReturnLength);

      [DllImport(ADVAPI32, ExactSpelling = true, SetLastError = true)]
      [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
      private static extern Boolean RevertToSelf();

      [DllImport(ADVAPI32, EntryPoint = "LookupPrivilegeValueW", CharSet = CharSet.Unicode, SetLastError = true)]
      [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
      private static extern Boolean LookupPrivilegeValue(String lpSystemName, String lpName, [In, Out] ref LUID Luid);

      [DllImport(KERNEL32, ExactSpelling = true, SetLastError = true)]
      [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
      internal static extern IntPtr GetCurrentProcess();

      [DllImport(KERNEL32, ExactSpelling = true, SetLastError = true)]
      [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
      internal static extern IntPtr GetCurrentThread();

      [DllImport(ADVAPI32, CharSet = CharSet.Unicode, SetLastError = true)]
      [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
      internal static extern Boolean OpenProcessToken(IntPtr ProcessToken,
         TokenAccessLevels DesiredAccess, [In, Out] ref SafeTokenHandle TokenHandle);

      [DllImport(ADVAPI32, CharSet = CharSet.Unicode, SetLastError = true)]
      [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
      internal static extern Boolean OpenThreadToken(IntPtr ThreadToken, TokenAccessLevels DesiredAccess,
         Boolean OpenAsSelf, [In, Out] ref SafeTokenHandle TokenHandle);

      [DllImport(ADVAPI32, CharSet = CharSet.Unicode, SetLastError = true)]
      [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
      internal static extern Boolean DuplicateTokenEx(SafeTokenHandle ExistingToken, 
         TokenAccessLevels DesiredAccess, IntPtr TokenAttributes, 
         SecurityImpersonationLevel ImpersonationLevel, TokenType TokenType,
         [In, Out] ref SafeTokenHandle NewToken);

      [DllImport(ADVAPI32, CharSet = CharSet.Unicode, SetLastError = true)]
      [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
      internal static extern Boolean SetThreadToken(IntPtr Thread, SafeTokenHandle Token);
   }
}
#endif