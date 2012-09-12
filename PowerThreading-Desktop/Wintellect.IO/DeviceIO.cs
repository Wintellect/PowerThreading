/******************************************************************************
Module:  DeviceIO.cs
Notices: Copyright (c) 2006-2008 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Wintellect.Threading.AsyncProgModel;
using Wintellect.Threading;
using System.Globalization;
using System.Diagnostics.Contracts;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.IO {
   /// <summary>
   /// A value type representing a single device control code.
   /// </summary>
   public struct DeviceControlCode : IEquatable<DeviceControlCode> {
      private readonly Int32 m_code;

      /// <summary>
      /// Constructs a device control code.
      /// </summary>
      /// <param name="type">Identifies the type of device.</param>
      /// <param name="function">Identifies the device's function.</param>
      /// <param name="method">Identifies the buffering method.</param>
      /// <param name="access">Identifies what access to the device is required.</param>
      public DeviceControlCode(DeviceType type, Int32 function, DeviceMethod method, DeviceAccess access) {
         m_code = (((Int32) type) << 16) | (((Int32) access) << 14) | (function << 2) | (Int32) method;
      }
      internal Int32 Code { get { return m_code; } }

      /// <summary>
      /// Returns the device code number as a string.
      /// </summary>
      /// <returns></returns>
      public override string ToString() { 
         return m_code.ToString(CultureInfo.InvariantCulture); 
      }

      /// <summary>
      /// Returns the hash code for the device code.
      /// </summary>
      /// <returns></returns>
      public override int GetHashCode() { return m_code; }

      /// <summary>
      /// Determines whether the specified Object is equal to the current DeviceControlCode.
      /// </summary>
      /// <param name="obj"></param>
      /// <returns>true if the objects are equal.</returns>
      public override Boolean Equals(object obj) {
         return this == (DeviceControlCode) obj;
      }

      /// <summary>
      /// Determines whether two DeviceControlCode objects have the same value.
      /// </summary>
      /// <param name="code1">The first DeviceControlCode to compare.</param>
      /// <param name="code2">The second DeviceControlCode to compare.</param>
      /// <returns>true if code1 and code2 have the same value; otherwise, false</returns>
      public static Boolean operator ==(DeviceControlCode code1, DeviceControlCode code2) {
         return code1.Code == code2.Code;
      }

      /// <summary>
      /// Determines whether two DeviceControlCode objects have different values.
      /// </summary>
      /// <param name="code1">The first DeviceControlCode to compare.</param>
      /// <param name="code2">The second DeviceControlCode to compare.</param>
      /// <returns>true if code1 and code2 have different values; otherwise, false</returns>
      public static Boolean operator !=(DeviceControlCode code1, DeviceControlCode code2) {
         return code1.Code != code2.Code;
      }

      /// <summary>
      /// Determines whether the specified DeviceControlCode is equal to the current DeviceControlCode.
      /// </summary>
      /// <param name="other">The other DeviceControlCode.</param>
      /// <returns>true if the DeviceControlCode are equal.</returns>
      public Boolean Equals(DeviceControlCode other) {
         return this == other;
      }
   }

   /// <summary>
   /// The various types of devices.
   /// </summary>
   public enum DeviceType {
      /// <summary>
      /// None.
      /// </summary>
      None = 0,

      /// <summary>
      /// The Beep device.
      /// </summary>
      Beep = 0x00000001,

      /// <summary>
      /// The CDRom device.
      /// </summary>
      CDRom = 0x00000002,

      /// <summary>
      /// The CDRom File System device.
      /// </summary>
      CDRomFileSystem = 0x00000003,

      /// <summary>
      /// The Controller device.
      /// </summary>
      Controller = 0x00000004,

      /// <summary>
      /// The DataLink device.
      /// </summary>
      DataLink = 0x00000005,

      /// <summary>
      /// The DFS device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Dfs")]
      Dfs = 0x00000006,

      /// <summary>
      /// The disk device.
      /// </summary>
      Disk = 0x00000007,

      /// <summary>
      ///  The Disk file system device.
      /// </summary>
      DiskFileSystem = 0x00000008,

      /// <summary>
      ///  The file system device.
      /// </summary>
      FileSystem = 0x00000009,

      /// <summary>
      /// The inport port device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Inport")]
      InportPort = 0x0000000a,

      /// <summary>
      ///  The keyboard device.
      /// </summary>
      Keyboard = 0x0000000b,

      /// <summary>
      /// The mailslot device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Mailslot")]
      Mailslot = 0x0000000c,

      /// <summary>
      /// THe MIDI in device.
      /// </summary>
      MidiIn = 0x0000000d,

      /// <summary>
      ///  The MIDI out device.
      /// </summary>
      MidiOut = 0x0000000e,

      /// <summary>
      ///  The mouse device.
      /// </summary>
      Mouse = 0x0000000f,

      /// <summary>
      /// The multi UNC provider device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Multi")]
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Unc")]
      MultiUncProvider = 0x00000010,

      /// <summary>
      ///  The named-pipe device.
      /// </summary>
      NamedPipe = 0x00000011,

      /// <summary>
      /// The network device.
      /// </summary>
      Network = 0x00000012,

      /// <summary>
      /// The network browser device.
      /// </summary>
      NetworkBrowser = 0x00000013,


      /// <summary>
      ///  The network file system device.
      /// </summary>
      NetworkFileSystem = 0x00000014,

      /// <summary>
      /// The null device.
      /// </summary>
      Null = 0x00000015,

      /// <summary>
      /// The parallel port device.
      /// </summary>
      ParallelPort = 0x00000016,

      /// <summary>
      /// The physical net card device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Netcard")]
      PhysicalNetcard = 0x00000017,

      /// <summary>
      /// The printer device.
      /// </summary>
      Printer = 0x00000018,

      /// <summary>
      /// The scanner device.
      /// </summary>
      Scanner = 0x00000019,

      /// <summary>
      /// The serial mouse port device.
      /// </summary>
      SerialMousePort = 0x0000001a,

      /// <summary>
      /// The serial port device.
      /// </summary>
      SerialPort = 0x0000001b,

      /// <summary>
      /// The screen device.
      /// </summary>
      Screen = 0x0000001c,

      /// <summary>
      /// The sound device.
      /// </summary>
      Sound = 0x0000001d,

      /// <summary>
      /// The streams device.
      /// </summary>
      Streams = 0x0000001e,

      /// <summary>
      /// The tape device.
      /// </summary>
      Tape = 0x0000001f,

      /// <summary>
      /// The tape file system device.
      /// </summary>
      TapeFileSystem = 0x00000020,

      /// <summary>
      /// The transport device.
      /// </summary>
      Transport = 0x00000021,

      /// <summary>
      /// The unknown device.
      /// </summary>
      Unknown = 0x00000022,

      /// <summary>
      /// The video device.
      /// </summary>
      Video = 0x00000023,

      /// <summary>
      /// The virtual disk device.
      /// </summary>
      VirtualDisk = 0x00000024,

      /// <summary>
      /// The wave in device.
      /// </summary>
      WaveIn = 0x00000025,

      /// <summary>
      /// The wave out device.
      /// </summary>
      WaveOut = 0x00000026,

      /// <summary>
      /// The port 8042 device.
      /// </summary>
      Port8042 = 0x00000027,

      /// <summary>
      /// The network redirector device.
      /// </summary>
      NetworkRedirector = 0x00000028,

      /// <summary>
      /// The battery device.
      /// </summary>
      Battery = 0x00000029,

      /// <summary>
      /// The bus extender device.
      /// </summary>
      BusExtender = 0x0000002a,

      /// <summary>
      /// The modem device.
      /// </summary>
      Modem = 0x0000002b,

      /// <summary>
      /// The VDM device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Vdm")]
      Vdm = 0x0000002c,

      /// <summary>
      /// The mass storage device.
      /// </summary>
      MassStorage = 0x0000002d,

      /// <summary>
      /// The SMB device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Smb")]
      Smb = 0x0000002e,

      /// <summary>
      /// The KS device.
      /// </summary>
      KS = 0x0000002f,

      /// <summary>
      /// The changer device.
      /// </summary>
      Changer = 0x00000030,

      /// <summary>
      /// The smartcard device.
      /// </summary>
      Smartcard = 0x00000031,

      /// <summary>
      /// The ACPI device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Acpi")]
      Acpi = 0x00000032,

      /// <summary>
      /// The DVD device.
      /// </summary>
      Dvd = 0x00000033,

      /// <summary>
      /// The full screen video device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Fullscreen")]
      FullscreenVideo = 0x00000034,

      /// <summary>
      /// The DFS file system device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Dfs")]
      DfsFileSystem = 0x00000035,

      /// <summary>
      /// The DFS volume device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Dfs")]
      DfsVolume = 0x00000036,

      /// <summary>
      /// The serenum device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Serenum")]
      Serenum = 0x00000037,

      /// <summary>
      /// The terminal server device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Srv")]
      TermSrv = 0x00000038,

      /// <summary>
      /// The KSEC device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Ksec")]
      Ksec = 0x00000039,

      /// <summary>
      /// The FIPS device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Fips")]
      Fips = 0x0000003A,

      /// <summary>
      /// The Infiniband device.
      /// </summary>
      [SuppressMessage("Microsoft.Naming", "CA1704:IdentifiersShouldBeSpelledCorrectly", MessageId = "Infiniband")]
      Infiniband = 0x0000003B
   }

   /// <summary>
   /// Describes how the buffer is used by the device driver.
   /// </summary>
   public enum DeviceMethod {
      /// <summary>
      /// This buffer represents both the input buffer and the output buffer that are specified in calls to DeviceIoControl. 
      /// The driver transfers data out of, and then into, this buffer. 
      /// </summary>
      Buffered = 0,

      /// <summary>
      /// This represents the input buffer that is specified in calls to DeviceIoControl.
      /// </summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1702:CompoundWordsShouldBeCasedCorrectly", MessageId = "InDirect")]
      InDirect = 1,

      /// <summary>
      /// This represents the output buffer that is specified in calls to DeviceIoControl.
      /// </summary>
      OutDirect = 2,

      /// <summary>
      /// The IRP supplies the user-mode virtual addresses of the input and output buffers 
      /// that were specified to DeviceIoControl.
      /// </summary>
      Neither = 3
   }

   /// <summary>
   /// Indicates the type of access that a caller must request when opening the file object 
   /// that represents the device. The I/O manager will create IRPs and call the driver with a 
   /// particular I/O control code only if the caller has requested the specified access rights
   /// </summary>
   public enum DeviceAccess {
      /// <summary>
      /// The I/O manager sends the IRP for any caller that has a handle to the 
      /// file object that represents the target device object. 
      /// </summary>
      Any = 0,

      /// <summary>
      /// The I/O manager sends the IRP for any caller that has a handle to the 
      /// file object that represents the target device object. 
      /// </summary>
      Special = Any,

      /// <summary>
      /// The I/O manager sends the IRP only for a caller with read access rights, 
      /// allowing the underlying device driver to transfer data from the device to system memory. 
      /// </summary>
      Read = 1,   // file & pipe

      /// <summary>
      /// The I/O manager sends the IRP only for a caller with write access rights, 
      /// allowing the underlying device driver to transfer data from system memory to its device. 
      /// </summary>
      Write = 2,   // file & pipe

      /// <summary>
      /// The I/O manager sends the IRP only for a caller with read and write access rights, 
      /// allowing the underlying device driver to transfer data from the device to system memory and
      /// allowing the underlying device driver to transfer data from system memory to its device. 
      /// </summary>
      ReadWrite = Read | Write
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.IO {
   /// <summary>This class allows you to perform low-level device I/O operations with a device driver.</summary>
   public class DeviceIO : IDisposable {
      private SafeFileHandle m_device; // Device driver handle
      private Boolean m_openedAsync;   // True if device open asynchronously

      #region The Construction and Dispose Methods
      /// <summary>
      /// Opens the specified device driver using the desired access and sharing. You can also indicate whether you wish to work with the device synchronously or asynchronously.</summary>
      /// <param name="deviceName">A string identifying the device driver.</param>
      /// <param name="access">What kind of access you wish to have to the device.</param>
      /// <param name="share">Indicates how you are willing to share access ot the device.</param>
      /// <param name="useAsync">If true, allows you to perform asynchronous operations to the device.</param>
      public DeviceIO(String deviceName, FileAccess access, FileShare share, Boolean useAsync) {
         m_device = NativeMethods.CreateFile(deviceName, access, share, IntPtr.Zero, FileMode.Open,
             useAsync ? FileOptions.Asynchronous : FileOptions.None, IntPtr.Zero);
         if (m_device.IsInvalid) throw new Win32Exception();

         if (m_openedAsync = useAsync) {
            ThreadPool.BindHandle(m_device); // Associate this device with the CLR's thread pool
            ThreadUtility.SkipSignalOfDeviceOnIOCompletion(m_device);
         }
      }

      /// <summary>Creates a DeviceIO opjects that allows you to communicate with a device driver that has already been opened.</summary>
      /// <param name="device">Identifies an already-open handle to a device driver.</param>
      /// <param name="openedAsync">Specify true if the device was opened for asynchronous operations.</param>
      public DeviceIO(SafeFileHandle device, Boolean openedAsync) {
         Contract.Requires(device != null);
         if (device.IsInvalid) throw new ArgumentException("Device handle must be valid");
         m_device = device;
         m_openedAsync = openedAsync;
      }

      /// <summary>Releases all resources used by the DeviceIO class.</summary>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1816:CallGCSuppressFinalizeCorrectly")]
      public void Dispose() { Dispose(true); }

      /// <summary>Releases the unmanaged resources used by the DeviceIO class specifying whether to perform a normal dispose operation.</summary>
      /// <param name="disposing">true for a normal dispose operation; false to finalize the handle.</param>
      protected virtual void Dispose(Boolean disposing) {
         if (disposing) m_device.Dispose();
      }
      #endregion


      #region Public Synchronous and APM Control Methods
      /// <summary>Sends a control code to the device driver synchronously.</summary>
      /// <param name="deviceControlCode">The control code to send to the driver.</param>
      public void Control(DeviceControlCode deviceControlCode) {
         Control(deviceControlCode, null);
      }

      /// <summary>Sends a control code to the device driver synchronously along with an input buffer.</summary>
      /// <param name="deviceControlCode">The control code to send to the driver.</param>
      /// <param name="inBuffer">The input buffer to send to the driver.</param>
      public void Control(DeviceControlCode deviceControlCode, Object inBuffer) {
         if (!m_openedAsync) {
            SyncControl(deviceControlCode, inBuffer, null);
         } else {
            EndControl(BeginControl(deviceControlCode, inBuffer, null, null));
         }
      }

      /// <summary>Sends a control code to the device driver asynchronously along with an input buffer.</summary>
      /// <param name="deviceControlCode">The control code to send to the driver.</param>
      /// <param name="inBuffer">The input buffer to send to the driver.</param>
      /// <param name="asyncCallback">The method to be called when the asynchronous operation completes.</param>
      /// <param name="state">A user-provided object that distinguishes this particular asynchronous operation from other operations.</param>
      /// <returns>An IAsyncResult that references the asynchronous operation.</returns>
      public IAsyncResult BeginControl(DeviceControlCode deviceControlCode,
         Object inBuffer, AsyncCallback asyncCallback, Object state) {
         return AsyncControl<Object>(deviceControlCode, inBuffer, null, asyncCallback, state);
      }

      /// <summary>Waits for the pending asynchronous operation to complete.</summary>
      /// <param name="result">The reference to the pending asynchronous request to wait for.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic")]
      public void EndControl(IAsyncResult result) {
         ((DeviceAsyncResult<Object>) result).EndInvoke();
      }
      #endregion


      #region Public Synchronous and APM GetObject Methods
      /// <summary>
      /// Synchronously sends a control code to a device.
      /// </summary>
      /// <typeparam name="TResult">The type of the result.</typeparam>
      /// <param name="deviceControlCode">The control code.</param>
      /// <returns>The result of the operation.</returns>
      [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
      public TResult GetObject<TResult>(DeviceControlCode deviceControlCode) where TResult : new() {
         return GetObject<TResult>(deviceControlCode, null);
      }

      /// <summary>
      /// Synchronously sends a control code and an input buffer to a device.
      /// </summary>
      /// <typeparam name="TResult">The type of the result.</typeparam>
      /// <param name="deviceControlCode">The control code.</param>
      /// <returns>The result of the operation.</returns>
      /// <param name="inBuffer">The input data.</param>
      [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
      public TResult GetObject<TResult>(DeviceControlCode deviceControlCode, Object inBuffer) where TResult : new() {
         if (!m_openedAsync) {
            return (TResult) SyncControl(deviceControlCode, inBuffer, new TResult());
         } else {
            return EndGetObject<TResult>(BeginGetObject<TResult>(deviceControlCode, inBuffer, null, null));
         }
      }

      /// <summary>
      /// Asynchronously sends a control code and an input buffer to a device.
      /// </summary>
      /// <typeparam name="TResult">The type of the result.</typeparam>
      /// <param name="deviceControlCode">The control code.</param>
      /// <param name="inBuffer">The input data.</param>
      /// <param name="asyncCallback">The method that should be called when the operation completes.</param>
      /// <param name="state">Data that should be passed to the callback method.</param>
      /// <returns>An IAsyncResult identifying the started operation.</returns>
      [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
      public IAsyncResult BeginGetObject<TResult>(DeviceControlCode deviceControlCode,
         Object inBuffer, AsyncCallback asyncCallback, Object state) where TResult : new() {
         return AsyncControl(deviceControlCode, inBuffer, new TResult(), asyncCallback, state);
      }

      /// <summary>
      /// Returns the result of a completed asynchronous operation.
      /// </summary>
      /// <typeparam name="TResult">The type of the result.</typeparam>
      /// <param name="result">The IAsyncResult passed into the callback method.</param>
      /// <returns>The result of the operation.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic"), SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
      public TResult EndGetObject<TResult>(IAsyncResult result) where TResult : new() {
         return ((DeviceAsyncResult<TResult>) result).EndInvoke();
      }
      #endregion


      #region Public Synchronous and APM GetArray Methods
      /// <summary>
      /// Synchronously sends a control code and an input buffer to a device.
      /// </summary>
      /// <param name="deviceControlCode">The control code.</param>
      /// <returns>The array of elements returned by the device.</returns>
      /// <param name="inBuffer">The input data.</param>
      /// <typeparam name="TElement">The type of elements that are returned from the device.</typeparam>
      /// <param name="maxElements">The maximum number of elements that you expect the device to return.</param>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
      public TElement[] GetArray<TElement>(DeviceControlCode deviceControlCode,
         Object inBuffer, Int32 maxElements) where TElement : struct {
         if (!m_openedAsync) {
            Int32 bytesReturned;
            TElement[] outBuffer = (TElement[]) SyncControl(deviceControlCode,
               inBuffer, new TElement[maxElements], out bytesReturned);
            Array.Resize<TElement>(ref outBuffer, bytesReturned / Marshal.SizeOf(typeof(TElement)));
            return outBuffer;
         } else {
            return EndGetArray<TElement>(BeginGetArray<TElement>(deviceControlCode, inBuffer, maxElements, null, null));
         }
      }

      /// <summary>
      /// Asynchronously sends a control code and an input buffer to a device.
      /// </summary>
      /// <param name="deviceControlCode">The control code.</param>
      /// <param name="inBuffer">The input data.</param>
      /// <typeparam name="TElement">The type of eleemnts that are returned from teh device.</typeparam>
      /// <param name="maxElements">The maximum number of elements that you expect the device to return.</param>
      /// <param name="asyncCallback">The method that should be called when the operation completes.</param>
      /// <param name="state">Data that should be passed to the callback method.</param>
      /// <returns>An IAsyncResult identifying the started operation.</returns>
      [SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
      public IAsyncResult BeginGetArray<TElement>(DeviceControlCode deviceControlCode,
         Object inBuffer, Int32 maxElements, AsyncCallback asyncCallback, Object state) where TElement : struct {
         return AsyncControl(deviceControlCode, inBuffer, new TElement[maxElements], asyncCallback, state);
      }

      /// <summary>
      /// Returns the result of a completed asynchronous operation.
      /// </summary>
      /// <typeparam name="TElement">The type of elements that are returned from the device.</typeparam>
      /// <param name="result">The IAsyncResult passed into the callback method.</param>
      /// <returns>The array of elements returned by the device.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic"), SuppressMessage("Microsoft.Design", "CA1004:GenericMethodsShouldProvideTypeParameter")]
      public TElement[] EndGetArray<TElement>(IAsyncResult result) where TElement : struct {
         return ((DeviceAsyncResult<TElement[]>) result).EndInvoke();
      }
      #endregion


      #region Private SyncControl, AsyncControl, NativeControl, and DeviceIoControl Methods
      private Object SyncControl(DeviceControlCode deviceControlCode,
         Object inBuffer, Object outBuffer) {
         Int32 bytesReturned;
         return SyncControl(deviceControlCode, inBuffer, outBuffer, out bytesReturned);
      }

      private Object SyncControl(DeviceControlCode deviceControlCode,
         Object inBuffer, Object outBuffer, out Int32 bytesReturned) {

         using (SafePinnedObject inDeviceBuffer = SafePinnedObject.FromObject(inBuffer))
         using (SafePinnedObject outDeviceBuffer = SafePinnedObject.FromObject(outBuffer)) {
            unsafe {
               NativeControl(deviceControlCode, inDeviceBuffer, outDeviceBuffer,
                  out bytesReturned, null);
            }
         }

         // When passed, the argument for outBuffer got boxed so we return a reference
         // to the object that contains the data returned from DeviceIoControl.
         return outBuffer;
      }

      private DeviceAsyncResult<T> AsyncControl<T>(DeviceControlCode deviceControlCode,
         Object inBuffer, T outBuffer, AsyncCallback asyncCallback, Object state) {

         SafePinnedObject inDeviceBuffer = SafePinnedObject.FromObject(inBuffer);
         SafePinnedObject outDeviceBuffer = SafePinnedObject.FromObject(outBuffer);
         DeviceAsyncResult<T> asyncResult = new DeviceAsyncResult<T>(inDeviceBuffer, outDeviceBuffer, asyncCallback, state);
         unsafe {
            Int32 bytesReturned;
            NativeControl(deviceControlCode, inDeviceBuffer, outDeviceBuffer,
               out bytesReturned, asyncResult.GetNativeOverlapped());
         }
         return asyncResult;
      }

      private unsafe void NativeControl(DeviceControlCode deviceControlCode,
         SafePinnedObject inBuffer, SafePinnedObject outBuffer,
         out Int32 bytesReturned, NativeOverlapped* nativeOverlapped) {

         Boolean ok = NativeMethods.DeviceIoControl(m_device, deviceControlCode.Code,
            inBuffer, inBuffer.Size, outBuffer, outBuffer.Size, out bytesReturned, nativeOverlapped);
         if (ok) return;

         Int32 error = Marshal.GetLastWin32Error();
         const Int32 c_ErrorIOPending = 997;
         if (error == c_ErrorIOPending) return;
         throw new InvalidOperationException(
            String.Format(CultureInfo.CurrentCulture, "Control failed (code={0})", error));
      }
      #endregion

      private static class NativeMethods {
         [DllImport("Kernel32", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CreateFileW", ExactSpelling = true)]
         public static extern SafeFileHandle CreateFile(String pFileName, FileAccess dwDesiredAccess,
            FileShare dwShareMode, IntPtr pSecurityAttributes, FileMode dwCreationDisposition,
            FileOptions dwFlagsAndAttributes, IntPtr hTemplateFile);

         [DllImport("Kernel32", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
         [return: MarshalAs(UnmanagedType.Bool)]
         public static unsafe extern Boolean DeviceIoControl(SafeFileHandle device, Int32 controlCode,
            SafePinnedObject inBuffer, Int32 inBufferSize, SafePinnedObject outBuffer, Int32 outBufferSize,
            out Int32 bytesReturned, NativeOverlapped* nativeOverlapped);
      }
   }
}


///////////////////////////////////////////////////////////////////////////////


#if false   
namespace Wintellect.IO {
   public class DeviceIOInfo : IDisposable {
      private SafeFileHandle m_device; // Device driver handle

#region Protected Constructors and Public Dispose
      protected DeviceIOInfo(String deviceName, FileAccess access, FileShare share, Boolean useAsync) {
         m_device = DeviceIO.OpenDevice(deviceName, access, share, useAsync);
      }

      protected DeviceIOInfo(SafeFileHandle device, Boolean useAsync) {
         m_device = device;
         if (useAsync) ThreadPool.BindHandle(m_device);
      }

      public void Dispose() { Dispose(true); }

      protected virtual void Dispose(Boolean disposing) {
         if (disposing) m_device.Dispose();
      }
#endregion


#region Protected Synchronous and APM Control Methods
      protected void Control(DeviceControlCode deviceControlCode) {
         DeviceIO.Control(m_device, deviceControlCode);
      }
      protected void Control(DeviceControlCode deviceControlCode, Object inBuffer) {
         DeviceIO.Control(m_device, deviceControlCode, inBuffer);
      }
      protected IAsyncResult BeginControl(DeviceControlCode deviceControlCode,
         Object inBuffer, AsyncCallback asyncCallback, Object state) {
         return DeviceIO.BeginControl(m_device, deviceControlCode, inBuffer, asyncCallback, state);
      }
      protected void EndControl(IAsyncResult result) {
         DeviceIO.EndControl(result);
      }
#endregion


#region Protected Synchronous and APM GetObject Methods
      protected TResult GetObject<TResult>(DeviceControlCode deviceControlCode) where TResult : new() {
         return DeviceIO.GetObject<TResult>(m_device, deviceControlCode, null);
      }

      protected TResult GetObject<TResult>(DeviceControlCode deviceControlCode, Object inBuffer) where TResult : new() {
         return DeviceIO.GetObject<TResult>(m_device, deviceControlCode, inBuffer);
      }

      protected IAsyncResult BeginGetObject<TResult>(DeviceControlCode deviceControlCode, Object inBuffer,
         AsyncCallback asyncCallback, Object state) where TResult : new() {
         return DeviceIO.BeginGetObject<TResult>(m_device, deviceControlCode, inBuffer, asyncCallback, state);
      }

      protected TResult EndGetObject<TResult>(IAsyncResult ar) where TResult : new() {
         return DeviceIO.EndGetObject<TResult>(ar);
      }
#endregion


#region Protected Synchronous and APM GetArray Methods
      protected TElement[] GetArray<TElement>(DeviceControlCode deviceControlCode, Object inBuffer, Int32 maxElements) where TElement : struct {
         return DeviceIO.GetArray<TElement>(m_device, deviceControlCode, inBuffer, maxElements);
      }

      protected IAsyncResult BeginGetArray<TElement>(DeviceControlCode deviceControlCode, Object inBuffer,
         Int32 maxElements, AsyncCallback asyncCallback, Object state) where TElement : struct {
         return DeviceIO.BeginGetArray<TElement>(m_device, deviceControlCode, inBuffer, maxElements, asyncCallback, state);
      }

      protected TElement[] EndGetArray<TElement>(IAsyncResult ar) where TElement : struct {
         return DeviceIO.EndGetArray<TElement>(ar);
      }
#endregion
   }
}
#endif


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.IO {
   // This class represents a pending DeviceIoControl I/O operation
   internal sealed class DeviceAsyncResult<TResult> : AsyncResult<TResult> {
      // These members encapsulate the pinned input and output memory buffers
      private SafePinnedObject m_inBuffer, m_outBuffer;

      // Constructs an instance specifying the input/output buffers
      public DeviceAsyncResult(SafePinnedObject inBuffer, SafePinnedObject outBuffer, AsyncCallback asyncCallback, Object state)
         : base(asyncCallback, state) {
         m_inBuffer = inBuffer;
         m_outBuffer = outBuffer;
      }

      // Creates and returns a NativeOverlapped structure to be passed to native code
      public unsafe NativeOverlapped* GetNativeOverlapped() {
         // Create a managed Overlapped structure that refers to our IAsyncResult (this)
         Overlapped o = new Overlapped(0, 0, IntPtr.Zero, this);

         // Pack the managed Overlapped structure into a NativeOverlapped structure
         // Pack causes the CLR to ensure that:
         //    1. the input/output objects are pinned
         //    2. the IAsyncResult object (this) isn't GC'd while the I/O is pending
         //    3. the thread pool thread calls CompletionCallback in the invoking thread's AppDomain
         return o.Pack(CompletionCallback, new Object[] { m_inBuffer.Target, m_outBuffer.Target });
      }

      // This method is called by a thread pool thread when native overlapped I/O completes
      private unsafe void CompletionCallback(UInt32 errorCode, UInt32 numBytes, NativeOverlapped* nativeOverlapped) {
         // Release the native OVERLAPPED structure and 
         // let the IAsyncResult object (this) be collectable.
         Overlapped.Free(nativeOverlapped);

         try {
            if (errorCode != 0) {
               // An error occurred, record the Win32 error code
               base.SetAsCompleted(new Win32Exception((Int32) errorCode), false);
            } else {
               // No error occurred, the output buffer contains the result
               TResult result = (TResult) m_outBuffer.Target;

               // If the result is an array of values, resize the array 
               // to the exact size so that the Length property is accurate
               if ((result != null) && result.GetType().IsArray) {

                  // Only resize if the number of elements initialized in the 
                  // array is less than the size of the array itself
                  Type elementType = result.GetType().GetElementType();
                  Int64 numElements = numBytes / Marshal.SizeOf(elementType);
                  Array origArray = (Array) (Object) result;
                  if (numElements < origArray.Length) {
                     // Create a new array whose size equals the number of initialized elements
                     Array newArray = Array.CreateInstance(elementType, numElements);

                     // Copy the initialized elements from the original array to the new array
                     Array.Copy(origArray, newArray, numElements);
                     result = (TResult) (Object) newArray;
                  }
               }

               // Record result and call AsyncCallback method passed to BeginXxx method
               base.SetAsCompleted(result, false);
            }
         }
         finally {
            // Make sure that the input and output buffers are unpinned
            m_inBuffer.Dispose();
            m_outBuffer.Dispose();
            m_inBuffer = m_outBuffer = null;   // Allow early GC
         }
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////