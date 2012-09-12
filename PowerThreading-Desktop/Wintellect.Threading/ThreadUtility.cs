/******************************************************************************
Module:  ThreadUtility.cs
Notices: Copyright (c) 2006-2010 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Wintellect;
using HWND = System.IntPtr;
using System.Diagnostics.Contracts;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading {
   /// <summary>
   /// Flags indicating how you intend to manipulate the thread after you open it.
   /// </summary>
   [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2217:DoNotMarkEnumsWithFlags")]
   [Flags]
   public enum ThreadRights : int {
      /// <summary>
      /// Required to terminate a thread using TerminateThread.
      /// </summary>
      Terminate = 0x0001,

      /// <summary>
      /// Required to suspend or resume a thread (see SuspendThread and ResumeThread).
      /// </summary>
      SuspendResume = 0x0002,

      /// <summary>
      /// Required to read the context of a thread using GetThreadContext.
      /// </summary>
      GetContext = 0x0008,

      /// <summary>
      /// Required to write the context of a thread using SetThreadContext.
      /// </summary>
      SetContext = 0x0010,

      /// <summary>
      /// Required to set certain information in the thread object.
      /// </summary>
      SetInformation = 0x0020,

      /// <summary>
      /// Required to set certain information in the thread object. 
      /// A handle that has the THREAD_SET_INFORMATION access right is 
      /// automatically granted THREAD_SET_LIMITED_INFORMATION.
      /// </summary>
      SetLimitedInformation = 0x0400,

      /// <summary>
      /// Required to read certain information from the thread object, 
      /// such as the exit code (see GetExitCodeThread).
      /// </summary>
      QueryInformation = 0x0040,

      /// <summary>
      /// Required to read certain information from the thread objects (see GetProcessIdOfThread). 
      /// A handle that has the THREAD_QUERY_INFORMATION access right is automatically 
      /// granted THREAD_QUERY_LIMITED_INFORMATION.
      /// </summary>
      QueryLimitedInformation = 0x0800,

      /// <summary>
      /// Required to set the impersonation token for a thread using SetThreadToken.
      /// </summary>
      SetThreadToken = 0x0080,

      /// <summary>
      /// Required to use a thread's security information directly without calling
      /// it by using a communication mechanism that provides impersonation services.
      /// </summary>
      Impersonate = 0x0100,

      /// <summary>
      /// Required for a server thread that impersonates a client.
      /// </summary>
      DirectImpersonation = 0x0200,

      /// <summary>
      /// Required to delete the object.
      /// </summary>
      Delete = 0x00010000,

      /// <summary>
      /// Required to read information in the security descriptor for the object, 
      /// not including the information in the SACL. To read or write the SACL, 
      /// you must request the ACCESS_SYSTEM_SECURITY access right.
      /// </summary>
      ReadPermissions = 0x20000,

      /// <summary>
      /// Required to modify the DACL in the security descriptor for the object.
      /// </summary>
      ChangePermissions = 0x40000,

      /// <summary>
      /// Required to change the owner in the security descriptor for the object.
      /// </summary>
      TakeOwnership = 0x80000,

      /// <summary>
      /// The right to use the object for synchronization. This enables a 
      /// thread to wait until the object is in the signaled state.
      /// </summary>
      Synchronize = 0x100000,

      /// <summary>
      /// 
      /// </summary>
      StandardRightsRequired = 0x000F0000,

      /// <summary>
      /// Same as StandardRightsRequired | Synchronize | 0x3FF
      /// </summary>
      FullControl = StandardRightsRequired | Synchronize | 0x3FF,
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading {
   /// <summary>
   /// This static class contains a bunch of useful thread methods.
   /// </summary>
   public static class ThreadUtility {
      #region Set Name of Finalizer Thread
      /// <summary>
      /// This method sets the name of the Finalizer thread for viewing in the debugger
      /// </summary>
      /// <param name="name">The string to name the Finalizer thread.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "Wintellect.Threading.ThreadUtility+SetNameOfFinalizerThread"), System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.GC.Collect")]
      public static void NameFinalizerThreadForDebugging(String name) {
         Contract.Requires(name != null);
         new SetNameOfFinalizerThread(name);
         GC.Collect();
         GC.WaitForPendingFinalizers();
      }
      private class SetNameOfFinalizerThread {
         private String m_name;
         public SetNameOfFinalizerThread() : this("Finalizer thread") { }
         public SetNameOfFinalizerThread(String name) {
            Contract.Requires(name != null);  m_name = name;
         }
         ~SetNameOfFinalizerThread() { Thread.CurrentThread.Name = m_name; }
         [ContractInvariantMethod]
         void ObjectInvariant() {
            Contract.Invariant(m_name != null);
         }
      }
      #endregion

      /// <summary>
      /// Returns true if the host machine has just one CPU.
      /// </summary>
      public static readonly Boolean IsSingleCpuMachine =
         (Environment.ProcessorCount == 1);

      /// <summary>
      /// Blocks the calling thread for the specified time.
      /// </summary>
      /// <param name="milliseconds">The number of milliseconds that this method should wait before returning.</param>
      /// <param name="computeBound">true if this method should spin in a compute bound loop; false if 
      /// Windows should not schedule for the specified amount of time.</param>
      public static void Block(Int32 milliseconds, Boolean computeBound) {
         if (computeBound) {
            Int64 stop = milliseconds + Environment.TickCount;
            while (Environment.TickCount < stop) ;
         } else { Thread.Sleep(milliseconds); }
      }

      /// <summary>
      /// Returns a ProcessThread object for a specified Win32 thread Id.
      /// </summary>
      /// <param name="threadId">The Win32 thread Id value.</param>
      /// <returns>A ProcessThread object matching the specified thread Id.</returns>
      [ContractVerification(false)]
      public static ProcessThread GetProcessThreadFromWin32ThreadId(Int32 threadId) {
         if (threadId == 0) threadId = ThreadUtility.GetCurrentWin32ThreadId();
         foreach (Process process in Process.GetProcesses()) {
            foreach (ProcessThread processThread in process.Threads) {
               if (processThread.Id == threadId) return processThread;
            }
         }
         throw new InvalidOperationException("No thread matching specified thread Id was found.");
      }

      #region Simple Win32 Thread Wrappers
      /// <summary>
      /// Returns the Win32 thread Id matching the thread that created the specified window handle.
      /// </summary>
      /// <param name="hwnd">Identifies a window handle.</param>
      /// <returns>The thread that created the window.</returns>
      public static Int32 GetWindowThreadId(HWND hwnd) {
         Int32 processId;
         return NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
      }

      /// <summary>
      /// Returns the Win32 process Id containing the thread that created the specified window handle.
      /// </summary>
      /// <param name="hwnd">Identifies a window handle.</param>
      /// <returns>The process owning the thread that created the window.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "hwnd")]
      public static Int32 GetWindowProcessId(HWND hwnd) {
         Int32 processId;
         Int32 threadId = NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
         return processId;
      }

      /// <summary>
      /// Opens a thread in the system identified via its Win32 thread Id.
      /// </summary>
      /// <param name="rights">Indicates how you intend to manipulate the thread.</param>
      /// <param name="inheritHandle">true if the returned handle should be inherited by child processes.</param>
      /// <param name="threadId">The Win32 Id identifying a thread.</param>
      /// <returns>A SafeWaitHandle matching the opened thread. This method throws a WaitHandleCannotBeOpenedException if the thread cannot be opened.</returns>
      public static SafeWaitHandle OpenThread(ThreadRights rights, Boolean inheritHandle, Int32 threadId) {
         SafeWaitHandle thread = NativeMethods.OpenThread(rights, inheritHandle, threadId);
         Contract.Assume(thread != null);
         if (thread.IsInvalid) throw new WaitHandleCannotBeOpenedException();
         return thread;
      }

      /// <summary>
      /// Retrieves the number of the processor the current thread was running on during the call to this function.
      /// </summary>
      /// <returns>The current processor number.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
      public static Int32 GetCurrentProcessorNumber() { return NativeMethods.GetCurrentProcessorNumber(); }


      /// <summary>
      /// Retrieves the Win32 Id of the calling thread.
      /// </summary>
      /// <returns>The Win32 thread Id of the calling thread.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
      public static Int32 GetCurrentWin32ThreadId() { return NativeMethods.GetCurrentWin32ThreadId(); }

      /// <summary>
      /// Retrieves a pseudo handle for the calling thread.
      /// </summary>
      /// <returns>The pseudo handle for the current thread.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
      public static SafeWaitHandle GetCurrentWin32ThreadHandle() { return NativeMethods.GetCurrentWin32ThreadHandle(); }

      /// <summary>
      /// Retrieves a pseudo handle for the calling thread's process.
      /// </summary>
      /// <returns>The pseudo handle for the current process.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1024:UsePropertiesWhereAppropriate")]
      public static SafeWaitHandle GetCurrentWin32ProcessHandle() { return NativeMethods.GetCurrentWin32ProcessHandle(); }

      /// <summary>
      /// Causes the calling thread to yield execution to another thread 
      /// that is ready to run on the current processor. The operating 
      /// system selects the next thread to be executed.
      /// </summary>
      /// <returns>true if the operating system switches execution to another thread; 
      /// false if there are no other threads ready to execute and the OS doesn't switch 
      /// execution to another thread.</returns>
      public static Boolean SwitchToThread() { return NativeMethods.SwitchToThread(); }

      /// <summary>
      /// Tells the I/O Manager to not signal the file/device 
      /// handle when an I/O operation completes.
      /// </summary>
      /// <param name="device">Identifies the file or device that should not be signaled.</param>
      public static void SkipSignalOfDeviceOnIOCompletion(SafeFileHandle device) {
         if (!IsVistaOrLaterOS()) return;
         if (!NativeMethods.SetFileCompletionNotificationModes(device, FileCompletionNotificationNodes.FILE_SKIP_SET_EVENT_ON_HANDLE))
            throw new Win32Exception();
      }

      /// <summary>
      /// Tells the I/O Manager to not queue a completion entry to the specified 
      /// device's I/O completion port if the I/O operation completes synchronously.
      /// </summary>
      /// <param name="device">Identifies the file or device whose 
      /// synchronously-executed operation should not be placed in an 
      /// I/O completion port.</param>
      public static void SkipCompletionPortOnSynchronousIOCompletion(SafeFileHandle device) {
         ValidateVistaOrLaterOS();
         if (!NativeMethods.SetFileCompletionNotificationModes(device, FileCompletionNotificationNodes.FILE_SKIP_COMPLETION_PORT_ON_SUCCESS))
            throw new Win32Exception();
      }

      private enum FileCompletionNotificationNodes : byte {
         FILE_SKIP_COMPLETION_PORT_ON_SUCCESS = 1,
         FILE_SKIP_SET_EVENT_ON_HANDLE = 2
      }

      /// <summary>
      /// Causes the calling thread to allow another thread to run.
      /// </summary>
      public static void StallThread() {
         if (IsSingleCpuMachine) {
            // On single-CPU system, spinning does no good
            SwitchToThread();
         } else {
            // The multi-CPU system might be hyper-threaded, let the other thread run
            Thread.SpinWait(1);
         }
      }

      /// <summary>
      /// Retrieves the cycle time for the specified thread.
      /// </summary>
      /// <param name="threadHandle">Identifies the thread whose cycle time you'd like to obtain.</param>
      /// <returns>The thread's cycle time.</returns>
      [CLSCompliant(false)]
      public static UInt64 QueryThreadCycleTime(SafeWaitHandle threadHandle) {
         ValidateVistaOrLaterOS();
         UInt64 cycleTime;
         if (!NativeMethods.QueryThreadCycleTime(threadHandle, out cycleTime))
            throw new Win32Exception();
         return cycleTime;
      }

      /// <summary>
      /// Retrieves the sum of the cycle time of all threads of the specified process.
      /// </summary>
      /// <param name="processHandle">Identifies the process whose threads' cycles times you'd like to obtain.</param>
      /// <returns>The process' cycle time.</returns>
      [CLSCompliant(false)]
      public static UInt64 QueryProcessCycleTime(SafeWaitHandle processHandle) {
         ValidateVistaOrLaterOS();
         UInt64 cycleTime;
         if (!NativeMethods.QueryProcessCycleTime(processHandle, out cycleTime))
            throw new Win32Exception();
         return cycleTime;
      }

      /// <summary>
      /// Retrieves the cycle time for the idle thread of each processor in the system.
      /// </summary>
      /// <returns>The number of CPU clock cycles used by each idle thread.</returns>
      [CLSCompliant(false)]
      public static UInt64[] QueryIdleProcessorCycleTimes() {
         ValidateVistaOrLaterOS();
         Int32 byteCount = Environment.ProcessorCount;
         Contract.Assume(byteCount > 0);
         UInt64[] cycleTimes = new UInt64[byteCount];
         byteCount *= 8;   // Size of UInt64
         if (!NativeMethods.QueryIdleProcessorCycleTime(ref byteCount, cycleTimes))
            throw new Win32Exception();
         return cycleTimes;
      }
      #endregion

      #region Cancel Synchronous I/O
      /*
		Not cancellable: 
			DeleteTree (use WalkTree), CopyFile (use CopyFileEx), MoveFile(Ex) (use MoveFileWithProgress), ReplaceFile

		Cancellable via callback: 
			WalkTree, CopyFileEx, MoveFileWithProgress

		Cancellable via CancelSynchronousIo: 
			CreateFile, ReadFile(Ex), ReadFileScatter, WriteFile(Ex), WriteFileGather, SetFilePointer(Ex),
			SetEndOfFile, SetFileValidData, FlushFileBuffers, LockFile(Ex), UnlockFile(Ex), 
			FindClose, FindFirstFile(Ex), FindNextFile, FindFirstStreamW, FindNextStreamW,
			CreateHardLink, DeleteFile, GetFileType, GetBinaryType, 
			GetCompressedFileSize, GetFileInformationByHandle, GetFileAttributes(Ex), SetFileAttributes, 
			GetFileSize(Ex), GetFileTime, SetFileTime, SetFileSecurity
			GetFullPathName, GetLongPathName, GetShortPathName, SetFileShortName, 
			GetTempFileName, GetTempPath, SearchPath, 
			GetQueuedCompletionStatus,
			CreateFileMapping, MapViewOfFile(Ex), FlushViewOfFile			
      */

      /// <summary>
      /// Marks pending synchronous I/O operations that are issued by the specified thread as canceled.
      /// </summary>
      /// <param name="thread">Identifies the thread whose synchronous I/O you want to cancel.</param>
      /// <returns>true if an operation is cancelled; false if the thread was not waiting for I/O</returns>
      public static Boolean CancelSynchronousIO(SafeWaitHandle thread) {
         ValidateVistaOrLaterOS();
         if (NativeMethods.CancelSynchronousIO(thread)) return true;
         Int32 error = Marshal.GetLastWin32Error();

         const Int32 ErrorNotFound = 1168;
         if (error == ErrorNotFound) return false; // failed to cancel because thread was not waiting

         throw new Win32Exception(error);
      }
      #endregion

      #region I/O Background Processing Mode
      private static readonly Disposer s_endBackgroundProcessingMode = new Disposer(EndBackgroundProcessingMode);

      /// <summary>
      /// The system lowers the resource scheduling priorities of the thread 
      /// so that it can perform background work without significantly 
      /// affecting activity in the foreground.
      /// </summary>
      /// <returns>An IDisposable object that can be used to end 
      /// background processing mode for the thread.</returns>
      public static IDisposable BeginBackgroundProcessingMode() {
         ValidateVistaOrLaterOS();
         if (NativeMethods.SetThreadPriority(GetCurrentWin32ThreadHandle(), BackgroundProcessingMode.Start))
            return s_endBackgroundProcessingMode;
         throw new Win32Exception();
      }

      /// <summary>
      /// The system restores the resource scheduling priorities of the thread 
      /// as they were before the thread entered background processing mode.
      /// </summary>
      public static void EndBackgroundProcessingMode() {
         ValidateVistaOrLaterOS();
         if (NativeMethods.SetThreadPriority(GetCurrentWin32ThreadHandle(), BackgroundProcessingMode.End))
            return;
         throw new Win32Exception();
      }

      private enum BackgroundProcessingMode {
         Start = 0x10000,
         End = 0x20000
      }
      #endregion

      private static Boolean IsVistaOrLaterOS() {
         OperatingSystem os = Environment.OSVersion;
         Contract.Assume(os != null);
         return (os.Version >= new Version(6, 0));
      }

      private static void ValidateVistaOrLaterOS() {
         if (!IsVistaOrLaterOS())
            throw new NotSupportedException("Requires Windows 6.0 or later");
      }

      private static class NativeMethods {
         [DllImport("User32")]
         internal static extern Int32 GetWindowThreadProcessId(HWND hwnd, out Int32 pdwProcessId);

         [DllImport("Kernel32", SetLastError = true, EntryPoint = "OpenThread")]
         internal static extern SafeWaitHandle OpenThread(ThreadRights dwDesiredAccess,
            [MarshalAs(UnmanagedType.Bool)] Boolean bInheritHandle, Int32 threadId);

         [DllImport("Kernel32", ExactSpelling = true)]
         internal static extern Int32 GetCurrentProcessorNumber();

         [DllImport("Kernel32", EntryPoint = "GetCurrentThreadId", ExactSpelling = true)]
         internal static extern Int32 GetCurrentWin32ThreadId();

         [DllImport("Kernel32", EntryPoint = "GetCurrentThread", ExactSpelling = true)]
         internal static extern SafeWaitHandle GetCurrentWin32ThreadHandle();

         [DllImport("Kernel32", EntryPoint = "GetCurrentProcess", ExactSpelling = true)]
         internal static extern SafeWaitHandle GetCurrentWin32ProcessHandle();

         [DllImport("Kernel32", ExactSpelling = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean SwitchToThread();

         // http://msdn.microsoft.com/en-us/library/aa480216.aspx
         [DllImport("Kernel32", SetLastError = true, EntryPoint = "CancelSynchronousIo")]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean CancelSynchronousIO(SafeWaitHandle hThread);

         [DllImport("Kernel32", ExactSpelling = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean QueryThreadCycleTime(SafeWaitHandle threadHandle, out UInt64 CycleTime);

         [DllImport("Kernel32", ExactSpelling = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean QueryProcessCycleTime(SafeWaitHandle processHandle, out UInt64 CycleTime);

         [DllImport("Kernel32", ExactSpelling = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean QueryIdleProcessorCycleTime(ref Int32 byteCount, UInt64[] CycleTimes);

         [DllImport("Kernel32", ExactSpelling = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean SetFileCompletionNotificationModes(SafeFileHandle FileHandle, FileCompletionNotificationNodes Flags);

         [DllImport("Kernel32", ExactSpelling = true, SetLastError = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         internal static extern Boolean SetThreadPriority(SafeWaitHandle hthread, BackgroundProcessingMode mode);
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////
