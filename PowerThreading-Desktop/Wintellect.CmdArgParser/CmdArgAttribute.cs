/******************************************************************************
Module:  CmdArgAttribute.cs
Notices: Copyright (c) 2011 Jeffrey Richter
******************************************************************************/


// TODO: Add support for an option array and for default options (without /Xxx:)
using System;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.CommandArgumentParser {
   /// <summary>
   /// Indicates whether the command-line argument is required, not-required, or is optional.
   /// </summary>
   public enum CmdArgRequiredValue {
      /// <summary>
      /// Indicates that the argument is required.
      /// </summary>
      Yes, 

      /// <summary>
      /// Indicates that the argument is not required.
      /// </summary>
      No, 

      /// <summary>
      /// Indicates that the argument is optional.
      /// </summary>
      Optional
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.CommandArgumentParser {
   /// <summary>
   /// An attribute that can be applied to a field or property indicating 
   /// that the member maps to a command line argument.
   /// </summary>
   [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
   public sealed class CmdArgAttribute : Attribute {
      /// <summary>
      /// The attribute has no mandatory arguments.
      /// </summary>
      public CmdArgAttribute() { }

      private String m_ArgName;
      private Boolean m_RequiredArg;
      private CmdArgRequiredValue m_RequiredValue = CmdArgRequiredValue.No;
      private String m_Description;
      private Boolean m_UndocumentedArg;

      /// <summary>
      /// Identifies the argument name that maps to the associated field or property.
      /// If not specified, the argument name is identical to the field or property name.
      /// </summary>
      public String ArgName {
         get { return m_ArgName; }
         set { m_ArgName = value; }
      }

      /// <summary>
      /// Indicates whether this argument must be specified.
      /// </summary>
      public Boolean RequiredArg {
         get { return m_RequiredArg; }
         set { m_RequiredArg = value; }
      }

      /// <summary>
      /// Indicates whether the command-line argument's value is required, not-required, or is optional.
      /// </summary>
      public CmdArgRequiredValue RequiredValue {
         get { return m_RequiredValue; }
         set { m_RequiredValue = value; }
      }

      /// <summary>
      /// Indicates the Usage text for this command-line argument.
      /// </summary>
      public String Description {
         get { return m_Description; }
         set { m_Description = value; }
      }

      /// <summary>
      /// Indicates whether this argument should appear in the Usage string.
      /// </summary>
      public Boolean UndocumentedArg {
         get { return m_UndocumentedArg; }
         set { m_UndocumentedArg = value; }
      }
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.CommandArgumentParser {
   /// <summary>Attribute describing how the target field maps to a command-line argument.</summary>
   [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
   public sealed class CmdArgEnumValueDescriptionAttribute : Attribute {
      /// <summary>Constructs an attribute using the specified command-line argument description.</summary>
      /// <param name="description"></param>
      public CmdArgEnumValueDescriptionAttribute(String description) {
         m_Description = description;
      }

      private String m_Description = null;

      /// <summary>Returns the description passed in the constructor.</summary>
      public String Description {
         get { return m_Description; }
      }
   }
}


//////////////////////////////// End of File //////////////////////////////////
