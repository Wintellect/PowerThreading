using System;
using System.IO;
using System.Collections.Generic;
using Wintellect.Threading.AsyncProgModel;
using System.Threading;
using System.Diagnostics.Contracts;

namespace Wintellect.IO {
   /// <summary>Provides a set of static methods for manipulating System.IO.Stream.</summary>
   public static class StreamExtensions {
      private static IEnumerator<Int32> CopyStream(AsyncEnumerator<Int64> ae, Stream source, Stream destination, Int32 bufferSize, Action<Int64> reportProgress) {
         Byte[] buffer = new Byte[bufferSize];
         Int64 totalBytesRead = 0;
         while (true) {
            ae.SetOperationTag("Reading from source stream");
            // Read whichever is smaller (number of bytes left to read OR the buffer size)
            source.BeginRead(buffer, 0, buffer.Length, ae.End(), null);
            yield return 1;
            Int32 bytesReadThisTime = source.EndRead(ae.DequeueAsyncResult());
            totalBytesRead += bytesReadThisTime;

            ae.SetOperationTag("Writing to destination stream");
            destination.BeginWrite(buffer, 0, bytesReadThisTime, ae.End(), null);
            yield return 1;
            destination.EndWrite(ae.DequeueAsyncResult());

            if (reportProgress != null) reportProgress(totalBytesRead);
            if (bytesReadThisTime < buffer.Length) break;
         }
         ae.Result = totalBytesRead;
      }

      /// <summary>
      /// Asynchronously copies the contents of the source stream to the destination stream.
      /// </summary>
      /// <param name="source">The stream containing the data to be copied.</param>
      /// <param name="destination">The stream that will receive the copied data.</param>
      /// <param name="bufferSize">The size of the internal buffer that should be used to copy the data in chunks.</param>
      /// <param name="reportProgress">A callback method that is called after each chunk is copied to the destination stream.</param>
      /// <param name="callback">An optional asynchronous callback, to be called when copy completes.</param>
      /// <param name="state">A user-provided object that distinguishes this particular asynchronous operation from other operations.</param>
      /// <returns></returns>
      public static IAsyncResult BeginCopyStream(/* this */ Stream source, Stream destination, Int32 bufferSize, Action<Int64> reportProgress, AsyncCallback callback, Object state) {
         var ae = new AsyncEnumerator<Int64>("CopyStream") { SyncContext = null };
         return ae.BeginExecute(CopyStream(ae, source, destination, bufferSize, reportProgress), callback, state);
      }

      /// <summary>Gets the results of the stream copy operation.</summary>
      /// <param name="source">The stream used to initiate the stream copy operation.</param>
      /// <param name="result">Identifies the asynchronous operation.</param>
      /// <returns>The number of bytes copied to the destination stream.</returns>
      [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "source")]
      public static Int64 EndCopyStream(/* this */ Stream source, IAsyncResult result) {
         Contract.Requires(result != null);
         return AsyncEnumerator<Int64>.FromAsyncResult(result).EndExecute(result);
      }
   }
}