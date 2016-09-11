#if !NET45
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyModel;

namespace Rock.Diagnostics
{
    public class StackTrace
    {
        private readonly StackFrame[] _frames;

        public StackTrace(StackFrame frame)
        {
            _frames = new[] { frame };
        }

        public StackTrace()
            : this(1)
        {
        }

        public StackTrace(int skipFrames)
        {
            var stackTraceLines = Environment.StackTrace.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            var types = GetAllTypes();

            _frames =
                stackTraceLines
                    .Skip(3 + skipFrames)
                    .Select(line => new StackFrame(line, types))
                    .ToArray();
        }

        private static Type[] GetAllTypes()
        {
            return
                DependencyContext.Default.CompileLibraries
                    .Select(compilationLibrary => {
                        try { return Assembly.Load(new AssemblyName(compilationLibrary.Name)); }
                        catch { return null; }
                    })
                    .Where(a => a != null)
                    .SelectMany(a => a.GetTypes())
                    .ToArray();
        }

        public int FrameCount
        {
            get { return _frames.Length; }
        }

        public StackFrame GetFrame(int index)
        {
            return _frames[index];
        }

        public StackFrame[] GetFrames()
        {
            var frames = new StackFrame[_frames.Length];
            _frames.CopyTo(frames, 0);
            return frames;
        }
    }

    public class StackFrame
    {
        /// <summary>
        /// This regex, when successfully matched, will contain three named groups:
        ///   "type":
        ///      The value will be the fully-qualified name of a Type.
        ///   "method":
        ///      The value will be the name of a method.
        ///      If the method represents a constructor, the value will be ".ctor".
        ///   "params":
        ///      The group's captures will contain zero or more items.
        ///      The number of capture items corresponds to the number of parameters of the method.
        ///      Each capture item's value will be the (*not* fully-qualified) name of the parameter's type.
        /// </summary>
        private static readonly Regex _stackFrameRegex = new Regex(@"
\b                      # Start with a word boundry, indicating the beginning of a method signature
(?<type>                # Begin capture group 'type' - this will match a fully-qualified type name
    [^. ]+                  # Find an identifier - the left-most part of the fully qualified type name (probably a namespace)
    (?:                     # Start of non-capture group - this group contains the rest of the fully-qualified type name
        \.                      # Find a dot
        [^. ]+                  # Find an identifier
    )*                      # End of non-capture group; match zero to many of these groups
)                       # End of capture group 'type'
\.                      # Find a dot
(?<method>              # Start of capture group 'method' - this will match the method name
    \.?                     # Optionally find another dot - for constructors ('.ctor')
    [^. ]+                  # Find an identifier
)                       # End of capture group 'method'
\(                      # Find an open parenthesis - this indicates the start of the parameter list
    (?:                     # Begin non-capture group - this group contains the first parameter, if present
        (?<params>              # Start of capture group 'params'
            [^ \t\r\n]+             # Find some non-whitespace - this is the parameter type
        )                       # End of capture group 'params'
        [ \t\r\n]+              # Find some whitespace - this separates the parameter type from its name
        [^ \t\r\n,)]+           # Find some non-whitespace - this is the name of the parameter
    )?                      # End of non-capture group; match zero or one of these 'first parameter' groups
    (?:                     # Start non-capture group - this group contains the second and subsequent parameters, if present
        [ \t\r\n]*              # Optional whitespace before the comma
        ,                       # Find a comma - this separates each parameter
        [ \t\r\n]*              # Optional whitespace after the comma
        (?<params>              # Start of capture group 'params'
            [^ \t\r\n]+             # Find some non-whitespace - this is the parameter type
        )                       # End of capture group 'params'
        [ \t\r\n]+              # Find some whitespace - this separates the parameter type from its name
        [^ \t\r\n,)]+           # Find some non-whitespace - this is the name of the parameter
    )*                      # End of non-capture group - match zero to many of these 'second and subsequent parameters' groups
\)                      # Find a close parenthesis - this is the end of the method signature
(?:                     # Begin non-capture group - this group contains the file info for the stack frame
    [ ]                     # Find a space
    in                      # Find the word 'in'
    [ ]                     # Find a space
    (?<file>                # Begin capture group 'file' - this will match the file where the method was declared
        [^\r\n]+                # Find non-linebreak characters
    )                       # End capture group 'file'
    :line                   # Find the word ':line'
    [ ]                     # Find a space
    (?<line>                # Begin capture group 'line' - this will match the line number in the file where the method was declared
        \d{1,9}                 # Find one to nine digits
    )                       # End capture group 'line'
)?                      # End of non-capture group; match zero or one of these 'file info' groups
", RegexOptions.IgnorePatternWhitespace);

        private readonly Lazy<MethodBase> _method;
        private readonly Lazy<string> _fileName;
        private readonly Lazy<int> _fileLineNumber;

        public StackFrame(string rawStackFrame, IEnumerable<Type> types)
        {
            RawStackFrame = rawStackFrame;

            var match = _stackFrameRegex.Match(RawStackFrame);

            if (match.Success)
            {
                TypeName = match.Groups["type"].Value;
                MethodName = match.Groups["method"].Value;
                ParameterTypeNames = match.Groups["params"].Captures.Cast<Capture>().Select(c => c.Value).ToList().AsReadOnly();
            }
            else
            {
                ParameterTypeNames = new List<string>().AsReadOnly();
            }

            _method = new Lazy<MethodBase>(() => GetMethod(types, match));
            _fileName = new Lazy<string>(() => GetFileName(match));
            _fileLineNumber = new Lazy<int>(() => GetFileLineNumber(match));
        }

        public int GetFileColumnNumber()
        {
            return 0;
        }

        public int GetFileLineNumber()
        {
            return _fileLineNumber.Value;
        }

        public string GetFileName()
        {
            return _fileName.Value;
        }

        public MethodBase GetMethod()
        {
            return _method.Value;
        }

        public string RawStackFrame { get; }
        public string TypeName { get; }
        public string MethodName { get; }
        public IReadOnlyList<string> ParameterTypeNames { get; }

        public MethodBase Method
        {
            get { return _method.Value; }
        }

        public int FileLineNumber
        {
            get { return _fileLineNumber.Value; }
        }

        public string FileName
        {
            get { return _fileName.Value; }
        }

        public override string ToString()
        {
            return RawStackFrame;
        }

        private MethodBase GetMethod(IEnumerable<Type> types, Match match)
        {
            if (match.Success)
            {
                var indexOfLastDot = TypeName.LastIndexOf('.');
                var nestedTypeName =
                    indexOfLastDot != -1
                        ? TypeName.Substring(0, indexOfLastDot) + '+' + TypeName.Substring(indexOfLastDot + 1)
                        : null;

                var declaringType = types.SingleOrDefault(t =>
                    (!t.IsNested && t.FullName == TypeName)
                    || (nestedTypeName != null && t.IsNested && t.FullName == nestedTypeName));

                if (declaringType != null)
                {
                    var methods =
                        MethodName == ".ctor"
                            ? (IEnumerable<MethodBase>)declaringType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                            : declaringType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance);

                    using (var e = methods
                            .Where(m => m.Name == MethodName)
                            .Select(m => new { Method = m, Parameters = m.GetParameters() })
                            .Where(x =>
                                x.Parameters.Length == ParameterTypeNames.Count
                                && x.Parameters.Zip(ParameterTypeNames, (p, n) => p.ParameterType.Name == n).All(y => y))
                            .Select(x => x.Method)
                            .GetEnumerator())
                    {
                        if (e.MoveNext())
                        {
                            var method = e.Current;

                            if (!e.MoveNext())
                            {
                                return method;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private static string GetFileName(Match match)
        {
            if (match.Success)
            {
                var group = match.Groups["file"];

                if (group.Success)
                {
                    return group.Value;
                }
            }

            return null;
        }

        private static int GetFileLineNumber(Match match)
        {
            if (match.Success)
            {
                var group = match.Groups["line"];

                if (group.Success)
                {
                    return int.Parse(group.Value);
                }
            }

            return 0;
        }
    }
}
#endif
