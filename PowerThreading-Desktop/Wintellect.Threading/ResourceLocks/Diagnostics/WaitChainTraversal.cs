/******************************************************************************
Module:  WaitChainTraversal.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


#if false
using System;
using System.Text;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using System.Runtime.ConstrainedExecution;
using System.ComponentModel;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading {
    public enum WctObjectType : int {
        None = 0,
        CriticalSection = 1,
        SendMessage = 2,
        Mutex = 3,
        Alpc = 4,
        Com = 5,
        ThreadWait = 6,
        ProcessWait = 7,
        Thread = 8,
        ComActivation = 9,
        Unknown = 10
    }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading {
    public enum WctObjectStatus : int {
        None = 0,
        NoAccess = 1,   // Access_denied for this object
        Running = 2,    // Thread status
        Blocked = 3,    // Thread status
        PidOnly = 4,    // Thread status
        PidOnlyRpcss = 5,   // Thread status
        Owned = 6,      // Dispatcher object status
        NotOwned = 7,   // Dispatcher object status
        Abandoned = 8,  // Dispatcher object status
        Unknown = 9,    // All objects
        Error = 10      // All objects
    }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading {
    public enum WctAsyncStatus {
        AccessDenied = 5,       // The caller did not have sufficient privilege to open a target thread. 
        Canceled = 1223,       // The asynchronous session was cancelled by a call to the CloseThreadWaitChainSession function. 
        MoreData = 234,         // The NodeInfoArray buffer is not large enough to contain all the nodes in the wait chain. The NodeCount parameter contains the number of nodes in the chain. The wait chain returned is still valid. 
        ObjectNotFound = 4312,  // The specified thread could not be located. 
        Success = 0,            // The operation completed successfully. 
        TooManyThreads = 565    // The number of nodes exceeds WCT_MAX_NODE_COUNT. The wait chain returned is still valid 
    }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading {
    public sealed class WaitChainNode {
        private WctObjectType m_objectType;
        public WctObjectType ObjectType { get { return m_objectType; } }

        private WctObjectStatus m_objectStatus;
        public WctObjectStatus ObjectStatus { get { return m_objectStatus; } }

        private WctObjectType[] locks = new WctObjectType[] { WctObjectType.CriticalSection, WctObjectType.Mutex };
        private void VerifyLock() {
            if (Array.IndexOf(locks, m_objectType) != -1) return;
            throw new InvalidOperationException("Property not valid for this ObjectType.");
        }
        private void VerifyThread() {
            if (m_objectType == WctObjectType.Thread) return;
            throw new InvalidOperationException("Property not valid for this ObjectType.");
        }

        // The following fields are for a lock object
        private String m_objectName;
        public String ObjectName { get { VerifyLock(); return m_objectName; } }

        private Int64 m_timeout;   // reserved
        public Int64 Timeout { get { VerifyLock(); return m_timeout; } }

        private Boolean m_alertable;  // reserved
        public Boolean Alertable { get { VerifyLock(); return m_alertable; } }

        // The following fields are for a thread object
        private Int32 m_processId;
        public Int32 ProcessId { get { VerifyThread(); return m_processId; } }

        private Int32 m_threadId;   // Can be 0
        public Int32 ThreadId { get { VerifyThread(); return m_threadId; } }

        private Int32 m_waitTime;
        public Int32 WaitTime { get { VerifyThread(); return m_waitTime; } }

        private Int32 m_contextSwitches;
        public Int32 ContextSwitches { get { VerifyThread(); return m_contextSwitches; } }

        internal WaitChainNode(WctObjectType type, WctObjectStatus status) {
            m_objectType = type;
            m_objectStatus = status;
        }

        internal WaitChainNode(WctObjectType type, WctObjectStatus status, String name, Int64 timeout, Boolean alertable)
            : this(type, status) {
            VerifyLock();
            m_objectName = name;
            m_timeout = timeout;
            m_alertable = alertable;
        }

        internal WaitChainNode(WctObjectType type, WctObjectStatus status, Int32 processId, Int32 threadId, Int32 waitTime, Int32 contextSwitches)
            : this(type, status) {
            VerifyThread();
            m_processId = processId;
            m_threadId = threadId;
            m_waitTime = waitTime;
            m_contextSwitches = contextSwitches;
        }

        public override String ToString() {
            StringBuilder sb = new StringBuilder();
            sb.AppendFormat("Type={0}, Status={1}", m_objectType, m_objectStatus);
            switch (m_objectType) {
                case WctObjectType.Thread:
                    sb.AppendFormat(", ProcessId={0}, ThreadId={1}, WaitTime={2}, ContextSwitches={3:N0}",
                       ProcessId, ThreadId, TimeSpan.FromMilliseconds(WaitTime), ContextSwitches);
                    break;

                default: // A synchronization object.
                    if (ObjectName != null) sb.AppendFormat(", Name={0}", ObjectName);
                    break;
            }
            return sb.ToString();
        }
    }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.Threading {
    public sealed class WaitChain : IDisposable {
        private SafeWctHandle m_wct;

        public WaitChain() {
            m_wct = OpenThreadWaitChainSession(WctSession.Synchronous, null);
        }
        public void Dispose() { m_wct.Dispose(); }


        [Flags]
        public enum OutOfProcess : int {
            Flag = 0x00000001, // Follows the wait chain into other processes. Otherwise, the function reports the first thread in a different process but does not retrieve additional information.
            Com = 0x00000002,  // Enumerates all threads of an out-of-proc MTA COM server to find the correct thread identifier.
            CriticalSection = 0x00000004, // Retrieves critical-section information from other processes. 
            All = Flag | Com | CriticalSection
        }

        private static unsafe IList<WaitChainNode> BytesToWaitChainNodeInfoList(Byte[] bytes) {
            List<WaitChainNode> chain = new List<WaitChainNode>();
            for (Int32 index = 0; index < bytes.Length; index += c_SizeOfWaitChainNode) {
                WctObjectType type = (WctObjectType)BitConverter.ToInt32(bytes, index + 0);
                WctObjectStatus status = (WctObjectStatus)BitConverter.ToInt32(bytes, index + 4);

                WaitChainNode wcni;
                switch (type) {
                    case WctObjectType.CriticalSection:
                    case WctObjectType.Mutex:
                        String name = null;
                        fixed (Byte* pb = &bytes[index + 8]) {
                            Char* pc = (Char*)pb;

                            // Find the first 0 character in the name
                            Int32 length = 0;
                            for (; (pc[length] != 0) && (length < c_ObjectNameLength); length++) ;
                            if (length > 0) name = new String(pc, 0, length);
                        }
                        Int64 timeout = BitConverter.ToInt64(bytes, index + 8 + (c_ObjectNameLength * 2));
                        Boolean alertable = BitConverter.ToBoolean(bytes, index + 8 + (c_ObjectNameLength * 2) + 8);
                        wcni = new WaitChainNode(type, status, name, timeout, alertable);
                        break;

                    default:
                        Int32 processId = BitConverter.ToInt32(bytes, index + 8);
                        Int32 threadId = BitConverter.ToInt32(bytes, index + 12);   // Can be 0
                        Int32 waitTime = BitConverter.ToInt32(bytes, index + 16);
                        Int32 contextSwitches = BitConverter.ToInt32(bytes, index + 20);
                        wcni = new WaitChainNode(type, status, processId, threadId, waitTime, contextSwitches);
                        break;
                }
                chain.Add(wcni);
            }
            return chain;
        }

        [SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters")]
        public IList<WaitChainNode> Traverse(Int32 threadId, OutOfProcess flags, out Boolean isCycle) {
            const Int32 c_ErrorIOPending = 997;
            Int32 nodeCount = c_MaxNodeCount;
            Byte[] nodeBytes = new Byte[nodeCount * c_SizeOfWaitChainNode];
            //GCHandle gch = GCHandle.Alloc(nodeBytes, GCHandleType.Pinned);  // TODO: Async - pin bytes
            Boolean ok = GetThreadWaitChain(m_wct, IntPtr.Zero, flags, threadId, ref nodeCount, nodeBytes, out isCycle);
            if (!ok) {
                Int32 error = Marshal.GetLastWin32Error();
                Console.WriteLine("error:{0}", error);
                if (error == c_ErrorIOPending) return new List<WaitChainNode>();
                return new List<WaitChainNode>();
            }
            Array.Resize(ref nodeBytes, nodeCount * c_SizeOfWaitChainNode);
            return BytesToWaitChainNodeInfoList(nodeBytes);
        }

        #region Native Interop Code
        private sealed class SafeWctHandle : SafeHandleZeroOrMinusOneIsInvalid {
            public SafeWctHandle() : base(true) { }
            //public SafeWctHandle(IntPtr handle, Boolean ownsHandle) : base(ownsHandle) { base.SetHandle(handle); }
            protected override Boolean ReleaseHandle() { CloseThreadWaitChainSession(this.handle); return true; } // TODO: Chekc if Win32 prototype changed to return Boolean
        }

        private const Int32 c_ObjectNameLength = 128;
        private const Int32 c_MaxNodeCount = 16;
        // type=4, status=4, name=128 (c_ObjectNameLength) * 2, timeout=8, alertable=4, padding=4
        private const Int32 c_SizeOfWaitChainNode = 4 + 4 + (c_ObjectNameLength * 2) + 8 + 4 + 4;

#pragma warning disable 414
        // The field 'Wintellect.Threading.WaitChain.WctSession.Asynchronous' is assigned but its value is never used
        private enum WctSession : int { Synchronous = 0, Asynchronous = 1 }
#pragma warning restore

        [DllImport("AdvApi32", ExactSpelling = true, SetLastError = true)]
        private static extern SafeWctHandle OpenThreadWaitChainSession(WctSession sessionType, WaitChainCallback callback);

        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [DllImport("AdvApi32", ExactSpelling = true, SetLastError = true)]
        private static extern void CloseThreadWaitChainSession(IntPtr wctHandle);     // TODO: In Wct.h, this returns VOID, not a BOOL

        [DllImport("AdvApi32", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern Boolean GetThreadWaitChain(SafeWctHandle wctHandle, IntPtr context, OutOfProcess flags, Int32 threadId,
           ref Int32 nodeCount, Byte[] nodeBytes, [MarshalAs(UnmanagedType.Bool)] out Boolean isCycle);

        [DllImport("AdvApi32", ExactSpelling = true, SetLastError = true)]
        private static extern void RegisterWaitChainCOMCallback(IntPtr callStateCallback, IntPtr activationStateCallback);

        [DllImport("Kernel32")] // TODO: make private 
        public static extern Int32 GetCurrentThreadId();

        private delegate void WaitChainCallback(IntPtr wctHandle, IntPtr context, WctAsyncStatus status,
           ref Int32 nodeCount, Byte[] nodeBytes, ref Boolean IsCycle);
        #endregion

        #region Static constructor and COM registration code
        static WaitChain() {
            InitCOMAccess();
        }

        private sealed class SafeModuleHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid {
            public SafeModuleHandle() : base(true) { }
            protected override Boolean ReleaseHandle() { return FreeLibrary(this.handle); }

            [DllImport("Kernel32", SetLastError = true, ExactSpelling = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern Boolean FreeLibrary(IntPtr hmodule);
        }

        private static void InitCOMAccess() {
            // Register COM interfaces with WCT. This enables WCT to provide wait information if a thread is blocked on a COM call.
            // Get a handle to OLE32.DLL. You must keep this handle around for the life time for any WCT session.
            SafeModuleHandle hmodule = LoadLibraryW("ole32.dll");

            // Retrieve the function addresses for the COM helper APIs.
            IntPtr CallStateCallback = GetProcAddress(hmodule, "CoGetCallState");
            IntPtr ActivationStateCallback = GetProcAddress(hmodule, "CoGetActivationState");

            // Register these functions with WCT.
            RegisterWaitChainCOMCallback(CallStateCallback, ActivationStateCallback);
        }

        [DllImport("Kernel32", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Unicode)]
        private static extern SafeModuleHandle LoadLibraryW(String dllName);

        [DllImport("Kernel32", SetLastError = true, ExactSpelling = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(SafeModuleHandle hmodule, String functionName);
        #endregion
    }
}

///////////////////////////////////////////////////////////////////////////////


#if AsynchronousWctSupport
private void WaitChainCallbackMethod(IntPtr wctHandle, IntPtr context, WctAsyncStatus status,
   ref Int32 nodeCount, Byte[] nodeBytes, ref Boolean isCycle) {
   if (status != WctAsyncStatus.Success && status != WctAsyncStatus.TooManyThreads) {
      throw new Win32Exception((Int32) status);
   }
   Array.Resize(ref nodeBytes, nodeCount * c_SizeOfWaitChainNode);
   OnTraversed(new WaitChainEventArgs(status, isCycle, BytesToWaitChainNodeInfoList(nodeBytes)));
}

private void OnTraversed(WaitChainEventArgs e) {
   EventHandler<WaitChainEventArgs> t = Traversed;
   if (t != null) t(this, e);
}

public event EventHandler<WaitChainEventArgs> Traversed;
public void Traverse(Int32 threadId, OutOfProcess flags) {
   Boolean isCycle;
   Traverse(threadId, flags, out isCycle);
}

namespace Wintellect.Threading {
   public sealed class WaitChainEventArgs : EventArgs {
      private WctAsyncStatus m_status;
      private IList<WaitChainNode> m_nodes;
      private Boolean m_isCycle;
      internal WaitChainEventArgs(WctAsyncStatus status, Boolean isCycle, IList<WaitChainNode> nodes) {
         m_status = status;
         m_isCycle = isCycle;
         m_nodes = nodes;
      }
      public WctAsyncStatus Status { get { return m_status; } }
      public Boolean IsCycle { get { return m_isCycle; } }
      public IList<WaitChainNode> Nodes { get { return m_nodes; } }
   }
}
#endif
#endif


//////////////////////////////// End of File //////////////////////////////////


#if false
[Flags]
public enum InitOnceFlags {
   CheckOnly = 1,
   Async = 2,
   InitFailed = 4
}

public delegate Boolean InitOnceCallback(InitOnce initOnce, Object parameter, out Object context);

public sealed class InitOnce {
   //private const Int32 c_StaticInit = 0;
   private Int32 m_state = 0;
   public InitOnce() { }

   public Boolean InitOnceExecuteOnce(InitOnceCallback callback, Object parameter, out Object context) {
      return callback(this, parameter, out context);
   }

   public Boolean InitOnceBeginInitialize(InitOnceFlags flags, out Boolean pending, out Object context) {
      context = null;
      pending = true;
      return false;
   }

   public Boolean InitOnceComplete(InitOnceFlags flags, Object context) {
      return false;
   }
}
 Do condition variable stuff?
#endif