/******************************************************************************
Module:  Exception.cs
Notices: Copyright (c) 2006-2009 by Jeffrey Richter and Wintellect
******************************************************************************/


using System;
#if !PocketPC
using System.Runtime.Serialization;
#endif
using System.Security;

#if !v4
using System.Security.Permissions;
using System.Diagnostics.Contracts;
#endif


///////////////////////////////////////////////////////////////////////////////


#region Sample Exception<T> Usage Code
#if false
internal static class SampleUsage {
   private sealed class Simple : ExceptionArgs { }

   private sealed class BadCustomer : ExceptionArgs {
      private String m_customerName;
      public BadCustomer(String customerName) { m_customerName = customerName; }
      public String CustomerName { get { return m_customerName; } }
      public override string Message {
         get {
            return base.Message + 
               ((m_customerName == null) ? null : "Customer name=" + m_customerName);
         }
      }
   }

   private static void SampleThrow() {
      throw new Exception<BadCustomer>(
         new BadCustomer("Jeff"), "Customer not in database");
   }

   internal static void SampleCatch() {
      try {
         SampleThrow();
      }
      catch (Exception<BadCustomer> e) {
         Console.WriteLine(e.Args.CustomerName);
      }
   }
}
#endif
#endregion


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect {
   /// <summary>
   /// A base class that a custom exception would derive from in order to add its own exception arguments.
   /// </summary>
#if !SILVERLIGHT
   [Serializable]
#endif
   public class ExceptionArgs {

      /// <summary>
      /// The string message associated with this exception.
      /// </summary>
      public virtual String Message { get { return null; } }
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect {
   /// <summary>
   /// Represents errors that occur during application execution.
   /// </summary>
   /// <typeparam name="T">The type of exception and any additional arguments associated with it.</typeparam>
#if !SILVERLIGHT && !PocketPC
   [Serializable]
   public sealed class Exception<T> : Exception, ISerializable where T : ExceptionArgs {
#else
public sealed class Exception<T> : Exception where T : ExceptionArgs {
#endif
      // The three public constructors

      /// <summary>
      /// Initializes a new instance of the Exception class
      /// </summary>
      public Exception() : this(null) { }

      /// <summary>
      /// Initializes a new instance of the Exception class with a specified error message.
      /// </summary>
      /// <param name="message">The error message that explains the reason for the exception.</param>
      public Exception(String message)
         : this(message, null) {
      }

      /// <summary>
      /// Initializes a new instance of the Exception class with a specified error message 
      /// and a reference to the inner exception that is the cause of this exception. 
      /// </summary>
      /// <param name="message">The error message that explains the reason for the exception.</param>
      /// <param name="innerException">The exception that is the cause of the current exception, 
      /// or a null reference if no inner exception is specified.</param>
      public Exception(String message, Exception innerException)
         : this(null, message, innerException) {
      }

      private const String c_args = "Args";
      private readonly T m_args;

      /// <summary>
      /// Returns a reference to this exception's additional arguments.
      /// </summary>
      public T Args { get { return m_args; } }

      // The fourth public constructor because there is a field
      /// <summary>
      /// Initializes a new instance of the Exception class with additional arguments, 
      /// a specified error message, and a reference to the inner exception 
      /// that is the cause of this exception. 
      /// </summary>
      /// <param name="args">The exception's additional arguments.</param>
      /// <param name="message">The error message that explains the reason for the exception.</param>
      /// <param name="innerException">The exception that is the cause of the current exception, 
      /// or a null reference if no inner exception is specified.</param>
      public Exception(T args, String message, Exception innerException)
         : base(message, innerException) {
         m_args = args;
      }

      /// <summary>
      /// Initializes a new instance of the Exception class with additional arguments and 
      /// a specified error message. 
      /// </summary>
      /// <param name="args">The exception's additional arguments.</param>
      /// <param name="message">The error message that explains the reason for the exception.</param>
      public Exception(T args, String message)
         : this(args, message, null) {
      }

#if !SILVERLIGHT && !PocketPC
      // Because at least 1 field is defined, 
      // define the special deserialization constructor
      // Since this class is sealed, this constructor is private
      // If this class were not sealed, this constructor should be protected
#if v4
      [SecurityCritical]
#else
      [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
#endif
      private Exception(SerializationInfo info, StreamingContext context)
         : base(info, context) { // Let the base deserialize its fields
         Contract.Requires(info != null);

         // Deserialize each field
         m_args = (T)info.GetValue(c_args, typeof(T));
      }

      // Because at least 1 field is defined, 
      // define the serialization method
      /// <summary>
      /// When overridden in a derived class, sets the SerializationInfo with information about the exception.
      /// </summary>
      /// <param name="info">The SerializationInfo that holds the serialized object data about the exception being thrown.</param>
      /// <param name="context">The StreamingContext that contains contextual information about the source or destination.</param>
#if v4
      [SecurityCritical]
#else
      [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
#endif
      public override void GetObjectData(SerializationInfo info, StreamingContext context) {
         // Serialize each field
         info.AddValue(c_args, m_args);

         // Let the base type serialize its fields
         base.GetObjectData(info, context);
      }
#endif

      /// <summary>
      /// Gets a message that describes the current exception.
      /// </summary>
      public override String Message {
         get {
            return base.Message + ((m_args != null) ? m_args.Message : null);
         }
      }

      /// <summary>
      /// Creates and returns a string representation of the current exception.
      /// </summary>
      /// <returns></returns>
      public override string ToString() {
         return base.ToString();
      }

      /// <summary>
      /// Serves as a hash function for a particular type.
      /// </summary>
      /// <returns>A hash code for the current Object.</returns>
      public override int GetHashCode() {
         return base.GetHashCode();
      }

      /// <summary>
      /// Determines whether the specified Object is equal to the current Object.
      /// </summary>
      /// <param name="obj">The Object to compare with the current Object. </param>
      /// <returns>true if the specified Object is equal to the current Object; otherwise, false.</returns>
      public override Boolean Equals(Object obj) {
         Exception<T> other = obj as Exception<T>;
         if (other == null) return false;
         return Object.Equals(m_args, other.m_args) && base.Equals(obj);
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////