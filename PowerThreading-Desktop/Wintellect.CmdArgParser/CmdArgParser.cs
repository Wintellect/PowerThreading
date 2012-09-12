/******************************************************************************
Module:  CmdArgParser.cs
Notices: Copyright (c) 2011 Jeffrey Richter
******************************************************************************/


using System;
using System.Text;
using System.Reflection;
using System.Runtime.Serialization;
using System.Collections.Generic;
using System.IO;
using Wintellect;
using System.Globalization;
using System.Diagnostics.Contracts;


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.CommandArgumentParser {
   /// <summary>
   /// A class that parses a command-line string into its individual arguments.
   /// </summary>
   public sealed class CmdArgParser {
      private CmdArgParser() { }

      /// <summary>
      /// Returns a string indicating the valid command-line arguments.
      /// </summary>
      /// <param name="optionType">The type adorned with custom 
      /// attributes that indicate the valid command-line arguments.</param>
      /// <returns>A string indicating the valid command-line arguments.</returns>
      public static String Usage(Type optionType) {
         Contract.Requires(optionType != null);
         StringBuilder cmdLine = new StringBuilder();
         cmdLine.AppendFormat("{0} ", Path.GetFileNameWithoutExtension(
            Assembly.GetEntryAssembly().Location));

         const BindingFlags bf = BindingFlags.DeclaredOnly | BindingFlags.IgnoreCase |
            BindingFlags.Instance | BindingFlags.Public;
         Int32 optionsAppended = 0;
         Dictionary<String, String> argAndDesc = new Dictionary<String, String>();
         foreach (MemberInfo mi in optionType.GetMembers(bf)) {
            CmdArgAttribute caa = (CmdArgAttribute)
               Attribute.GetCustomAttribute(mi, typeof(CmdArgAttribute), false);

            // If the member doesn't have a CmdArgAttribute applied to it, try the next member
            if (caa == null) continue;

            // If the attribute indicates the argument is undocumented, try the next member
            if (caa.UndocumentedArg) continue;

            // Append the argument to the string:
            if (optionsAppended > 0) cmdLine.Append(" ");
            optionsAppended++;

            // If no attribute-given name specified, use the member's name
            String argName = (caa.ArgName != null) ? caa.ArgName : mi.Name;
            argAndDesc.Add(argName, caa.Description);

            // If the switch isn't required, it's optional: put it in square brackets
            if (!caa.RequiredArg) cmdLine.Append("[");
            cmdLine.AppendFormat("-{0}", argName);
            if (caa.RequiredValue == CmdArgRequiredValue.No) {
               // This switch must NOT have a value, the switch's type must be Boolean
            } else {
               // This switch MAY have a value, the switch's type doesn't have to be Boolean
               if (caa.RequiredValue == CmdArgRequiredValue.Optional)
                  cmdLine.Append("[");
               cmdLine.Append(":");
               Type switchType = GetFieldOrPropertyMemberType(mi);
               if (typeof(Enum).IsAssignableFrom(switchType)) {
                  Array a = Enum.GetValues(switchType);
                  for (Int32 n = 0; n < a.Length; n++) {
                     cmdLine.AppendFormat("{0}{1}", (n > 0) ? "|" : "", a.GetValue(n));

                     CmdArgEnumValueDescriptionAttribute caevda = (CmdArgEnumValueDescriptionAttribute)
                        Attribute.GetCustomAttribute(switchType.GetField(a.GetValue(n).ToString()),
                        typeof(CmdArgEnumValueDescriptionAttribute), false);

                     argAndDesc.Add(String.Format(CultureInfo.CurrentCulture,
                        "{0}:{1}", argName, a.GetValue(n)),
                        (caevda == null) ? "" : caevda.Description);
                  }
               } else {
                  cmdLine.Append(switchType.Name);
               }
               if (caa.RequiredValue == CmdArgRequiredValue.Optional)
                  cmdLine.Append("]");
            }
            if (!caa.RequiredArg) cmdLine.Append("]");
         }
         cmdLine.AppendLine();
         cmdLine.AppendLine();

         Int32 longestArgName = 0;
         foreach (KeyValuePair<String, String> kv in argAndDesc) {
            longestArgName = Math.Max(longestArgName, kv.Key.Length);
         }
         foreach (KeyValuePair<String, String> kv in argAndDesc) {
            cmdLine.AppendFormat("-{0}  ", kv.Key.PadRight(longestArgName));
            String[] lines = BreakStringIntoLinesOfSpecifiedWidth(kv.Value, /*Console.WindowWidth*/80 - longestArgName - 3 - 1);
            for (Int32 line = 0; line < lines.Length; line++) {
               if (line == 0)
                  cmdLine.AppendFormat(CultureInfo.CurrentCulture, lines[line]);
               else
                  cmdLine.AppendFormat(CultureInfo.CurrentCulture, "{0}{1}",
                     new String(' ', 1 + longestArgName + 2), lines[line]);
               cmdLine.AppendLine();
            }
         }
         return cmdLine.ToString();
      }

      /// <summary>
      /// Parses a set of command-line arguments populating the fields/properties of an ICmdArgs class object.
      /// </summary>
      /// <param name="cmdArgObj">Identifies the object whose fields/properties 
      /// should be set based on the command-line arguments.</param>
      /// <param name="args">Identifies the command-line arguments.</param>
      public static void Parse(ICmdArgs cmdArgObj, String[] args) {
         Contract.Requires((cmdArgObj != null) && (args != null));
         IList<String> requiredSwitchNames = ValidateMembers(cmdArgObj.GetType());

         for (Int32 arg = 0; arg < args.Length; arg++) {
            if ((args[arg][0] == '-') || (args[arg][0] == '/')) {
               // This argument is a switch, process it

               String switchName = null;
               String switchValue = String.Empty;  // Default to what an optional value should be

               Boolean SwitchIncludesAValue = (args[arg].IndexOf(':') != -1);
               if (SwitchIncludesAValue) {
                  // This switch includes a colon followed by a value
                  // Separate the switch name from its value
                  switchName = args[arg].Substring(1, args[arg].IndexOf(':') - 1);
                  switchValue = args[arg].Substring(args[arg].IndexOf(':') + 1);
               } else {
                  // This switch doesn't include a colon followed by a value
                  switchName = args[arg].Substring(1);
               }


               // Lookup the switch member
               CmdArgAttribute caa; // Initialized by LookupMember
               MemberInfo mi = LookupMember(cmdArgObj.GetType(), switchName, SwitchIncludesAValue, out caa);

               if (caa.RequiredValue == CmdArgRequiredValue.No) {
                  // This switch must NOT have a value, the switch's type must be Boolean
                  if (GetFieldOrPropertyMemberType(mi) != typeof(Boolean)) {
                     throw new Exception<CmdArgumentTypeExceptionArgs>(
                        String.Format(CultureInfo.CurrentCulture,
                           "The {0} switch must be of Boolean type.", mi.Name));
                  }
                  // Since the switch is specified, turn this Boolean to true
                  SetFieldOrPropertyValue(mi, cmdArgObj, true);
               } else {
                  // This switch MAY have a value, the switch's type doesn't have to be Boolean
                  Type switchType = GetFieldOrPropertyMemberType(mi);
                  if (typeof(Enum).IsAssignableFrom(switchType)) {
                     try {
                        SetFieldOrPropertyValue(mi, cmdArgObj, Enum.Parse(switchType, switchValue, true));
                     }
                     catch (ArgumentException) {
                        if (!Attribute.IsDefined(switchType, typeof(FlagsAttribute), false)) {
                           throw new Exception<InvalidCmdArgumentExceptionArgs>(
                                     new InvalidCmdArgumentExceptionArgs(switchName),
                                     String.Format(CultureInfo.CurrentCulture,
                                       "The {0} switch requires one of the following values: {1}.",
                                       switchName, String.Join(", ", Enum.GetNames(switchType))));
                        } else {
                           throw new Exception<InvalidCmdArgumentExceptionArgs>(
                                 new InvalidCmdArgumentExceptionArgs(switchName),
                                 String.Format(CultureInfo.CurrentCulture,
                                    "The {0} switch requires a combination of the following values (comma separated): {1}.",
                                    switchName, String.Join(", ", Enum.GetNames(switchType))));
                        }
                     }
                  } else {
                     SetFieldOrPropertyValue(mi, cmdArgObj, 
                        Convert.ChangeType(switchValue, switchType, CultureInfo.InvariantCulture));
                  }

                  if (caa.RequiredArg) {
                     // If we found a required switch, remove it from the list of required 
                     // switches.  This list should be empty when done parsing or some 
                     // required switches weren't specified by the user.
                     requiredSwitchNames.Remove(switchName);
                  }
               }
            } else {
               // This is a free-form command-line argument, append it to a list
               cmdArgObj.ProcessStandAloneArgument(args[arg]);
            }
         }

         // We're done parsing command-line arguments, were any required switch unspecified?
         if (requiredSwitchNames.Count > 0) {
            String[] names = new String[requiredSwitchNames.Count];
            requiredSwitchNames.CopyTo(names, 0);
            throw new Exception<InvalidCmdArgumentExceptionArgs>(
                  new InvalidCmdArgumentExceptionArgs(String.Join(", ", names)),
               String.Format(CultureInfo.CurrentCulture,
                  "The following required switch(es) must be specified: {0}.",
                  String.Join(", ", names)));
         }

         // Let the user's type perform any desired validation
         cmdArgObj.Validate();
      }

      private static Type GetFieldOrPropertyMemberType(MemberInfo mi) {
         if (mi.MemberType == MemberTypes.Field) {
            return ((FieldInfo) mi).FieldType;
         }
         if (mi.MemberType == MemberTypes.Property) {
            return ((PropertyInfo) mi).PropertyType;
         }
         throw new Exception<CmdArgumentTypeExceptionArgs>(
            String.Format(CultureInfo.CurrentCulture, 
               "Member {0} must be a field or property.", mi.Name));
      }


      private static void SetFieldOrPropertyValue(MemberInfo mi, ICmdArgs cmdArgObj, Object value) {
         if (mi.MemberType == MemberTypes.Field) {
            ((FieldInfo) mi).SetValue(cmdArgObj, value);
            return;
         }
         if (mi.MemberType == MemberTypes.Property) {
            ((PropertyInfo) mi).SetValue(cmdArgObj, value, null);
            return;
         }
         throw new Exception<CmdArgumentTypeExceptionArgs>(
                   String.Format(CultureInfo.CurrentCulture, 
                     "Member {0} must be a field or property.", mi.Name));
      }



      private static void ValidateRequiredValue(CmdArgAttribute caa, Boolean switchIncludesAValue, String name) {
         if ((caa.RequiredValue == CmdArgRequiredValue.Yes) && !switchIncludesAValue) {
            throw new Exception<InvalidCmdArgumentExceptionArgs>(
                  new InvalidCmdArgumentExceptionArgs(name),
               String.Format(CultureInfo.CurrentCulture, 
                  "The {0} switch requires an argument and none was specified.", name));
         }
         if ((caa.RequiredValue == CmdArgRequiredValue.No) && switchIncludesAValue) {
            throw new Exception<InvalidCmdArgumentExceptionArgs>(
                  new InvalidCmdArgumentExceptionArgs(name),
               String.Format(CultureInfo.CurrentCulture, 
                  "The {0} switch cannot have an argument and one was specified.", name));
         }
      }


      private static MemberInfo LookupMember(Type optionType, String name, Boolean switchIncludesAValue, out CmdArgAttribute caa) {
         BindingFlags bf = BindingFlags.DeclaredOnly | BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public;
         foreach (MemberInfo mi in optionType.GetMembers(bf)) {
            caa = (CmdArgAttribute) Attribute.GetCustomAttribute(mi, typeof(CmdArgAttribute), false);

            // If the member doesn't have a CmdArgAttribute applied to it, try the next member
            if (caa == null) continue;

            // If no attribute-given name specified, use the member's name
            String memberName = (caa.ArgName != null) ? caa.ArgName : mi.Name;

            // If the switch name matches the member's name, return the matching member
            if (String.Compare(name, memberName, StringComparison.OrdinalIgnoreCase) == 0) {
               ValidateRequiredValue(caa, switchIncludesAValue, name);
               return mi;
            }

            // If no match, try the next member
         }

         // If no members match, we have an invalid argument
         throw new Exception<InvalidCmdArgumentExceptionArgs>(
                   new InvalidCmdArgumentExceptionArgs(name), null);
      }


      private static IList<String> ValidateMembers(Type optionType) {
         List<String> argNames = new List<String>();
         List<String> requiredArgNames = new List<String>();

         const BindingFlags bf = BindingFlags.DeclaredOnly | BindingFlags.IgnoreCase |
            BindingFlags.Instance | BindingFlags.Public;
         foreach (MemberInfo mi in optionType.GetMembers(bf)) {
            CmdArgAttribute caa = (CmdArgAttribute)
               Attribute.GetCustomAttribute(mi, typeof(CmdArgAttribute), false);

            // If the member doesn't have a CmdArgAttribute applied to it, try the next member
            if (caa == null) continue;

            // If no attribute-given name specified, use the member's name
            String argName = (caa.ArgName != null) ? caa.ArgName : mi.Name;

            // If this switch name was already defined, throw an exception
            if (argNames.Contains(argName)) {
               throw new Exception<CmdArgumentTypeExceptionArgs>(
                  String.Format(CultureInfo.CurrentCulture, 
                     "The {0} switch appears more than once in the {1} class.", argName, optionType));
            }

            // The switch name didn't exist, let's add it and try the next one
            argNames.Add(argName);

            // If this switch is required, let's add it to the set of required switches
            if (caa.RequiredArg) requiredArgNames.Add(argName);
         }

         // No switch name conflicts occurred, return the set of required switches
         return requiredArgNames;
      }

      /// <summary>
      /// Breaks a string into lines where no line is more than the specified width.
      /// </summary>
      /// <param name="message">The string to break into lines.</param>
      /// <param name="width">The maximum number of characters per line.</param>
      /// <returns></returns>
      public /*private */static String[] BreakStringIntoLinesOfSpecifiedWidth(String message, Int32 width) {
         StringBuilder text = new StringBuilder(message);
         List<String> Lines = new List<String>();
         Char[] delimiters = { ' ', '\t', '-', '.' };
         while (text.Length > 0) {
            // Remove any delimiters from the start of line
            while (Array.IndexOf<Char>(delimiters, text[0]) == 0)
               text.Remove(0, 1);

            // Grab the maximum # of chars we have take for this line
            String workingLine = text.ToString().Substring(0, Math.Min(width, text.Length));
            Int32 lastIndex = -1;
            if (workingLine.Length >= width) {
               // Look backwards for a breaking char
               lastIndex = workingLine.LastIndexOfAny(delimiters);
            }
            if (lastIndex == -1) lastIndex = width - 1;
            Int32 numCharsToTake = Math.Min(lastIndex + 1, workingLine.Length);
            Lines.Add(workingLine.Substring(0, numCharsToTake));
            text.Remove(0, numCharsToTake);
         }
         return Lines.ToArray();
      }
   }
}


///////////////////////////////////////////////////////////////////////////////


namespace Wintellect.CommandArgumentParser {
   /// <summary>
   /// A type containing fields/properties to be populated from 
   /// command-line arguments must implements this interface.
   /// </summary>
   public interface ICmdArgs {
      /// <summary>
      /// This method is called when a command-line argument fails to parse correctly.
      /// </summary>
      /// <param name="errorInfo">Indicates the command--line argument that failed to parse.</param>
      void Usage(String errorInfo);

      /// <summary>
      /// This method is called after all the command-line arguments have been parsed.
      /// </summary>
      void Validate();

      /// <summary>
      /// This method is called as stand-alone arguments are parsed.
      /// </summary>
      /// <param name="arg">Indicates the value of the stand-alone argument.</param>
      void ProcessStandAloneArgument(String arg);
   }
}


//////////////////////////////// End of File //////////////////////////////////
