/******************************************************************************
Module:  CmdArgExceptionArgTypes.cs
Notices: Copyright (c) 2010 Jeffrey Richter
******************************************************************************/


using System;
using System.Runtime.Serialization;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.CommandArgumentParser {
   /// <summary>
   /// This represents a CmdArgumentType exception.
   /// </summary>
   [Serializable]
   public sealed class CmdArgumentTypeExceptionArgs: ExceptionArgs {
      internal CmdArgumentTypeExceptionArgs() { }
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.CommandArgumentParser {
   /// <summary>
   /// The exception argument indicating that an invalid command-line argument has been found.
   /// </summary>
   [Serializable]
   public sealed class InvalidCmdArgumentExceptionArgs : ExceptionArgs {
      // Define any private fields
      private readonly String m_InvalidCmdArg;

      // Define additional constructors that set the field
      /// <summary>
      /// Constructs an InvalidCmdArgumentExceptionArgs with the specified invalid command argument.
      /// </summary>
      /// <param name="invalidCmdArg"></param>
      public InvalidCmdArgumentExceptionArgs(String invalidCmdArg) {
         m_InvalidCmdArg = invalidCmdArg;
      }

      /// <summary>
      /// Returns the invalid argument.
      /// </summary>
      public String InvalidCmdArg { get { return m_InvalidCmdArg; } }

      /// <summary>
      /// Returns a string that contains the invalid argument.
      /// </summary>
      /// <returns></returns>
      public override String ToString() {
         if (InvalidCmdArg == null) return null;
         return "Invalid command-line argument: " + InvalidCmdArg;
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////
