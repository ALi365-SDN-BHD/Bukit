// ----------------------------------------------------------------------------------
// This file was automatically generated - 11/17/2025 08:03:34 by Scriban.DelegateCodeGen
// DOT NOT EDIT THIS FILE MANUALLY
// ----------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Reflection;
using Scriban.Functions;
using Scriban.Helpers;
using Scriban.Parsing;
using Scriban.Syntax;

namespace Scriban.Runtime
{
#if SCRIBAN_PUBLIC
    public
#else
    internal
#endif
    abstract partial class DynamicCustomFunction
    {


        private static MethodInfo M(Delegate d) => d.Method;

        static DynamicCustomFunction()
        {
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, object, bool>)ArrayFunctions.Contains), method => new Functionbool_IEnumerable_object(method));
            BuiltinFunctionDelegates.Add(M((Func<object, bool>)MathFunctions.IsNumber), method => new Functionbool_object(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, bool>)StringFunctions.Contains), method => new Functionbool_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, bool>)StringFunctions.EndsWith), method => new Functionbool_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, bool>)StringFunctions.EqualsIgnoreCase), method => new Functionbool_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, bool>)StringFunctions.StartsWith), method => new Functionbool_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, bool>)StringFunctions.Empty), method => new Functionbool_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, bool>)StringFunctions.Whitespace), method => new Functionbool_string(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, IEnumerable, object, object[], bool>)ArrayFunctions.Any), method => new Functionbool_TemplateContext_SourceSpan_IEnumerable_object_objectArray(method));
            BuiltinFunctionDelegates.Add(M((Func<DateTime>)DateTimeFunctions.Now), method => new FunctionDateTime(method));
            BuiltinFunctionDelegates.Add(M((Func<DateTime, double, DateTime>)DateTimeFunctions.AddDays), method => new FunctionDateTime_DateTime_double(method));
            BuiltinFunctionDelegates.Add(M((Func<DateTime, double, DateTime>)DateTimeFunctions.AddHours), method => new FunctionDateTime_DateTime_double(method));
            BuiltinFunctionDelegates.Add(M((Func<DateTime, double, DateTime>)DateTimeFunctions.AddMinutes), method => new FunctionDateTime_DateTime_double(method));
            BuiltinFunctionDelegates.Add(M((Func<DateTime, double, DateTime>)DateTimeFunctions.AddSeconds), method => new FunctionDateTime_DateTime_double(method));
            BuiltinFunctionDelegates.Add(M((Func<DateTime, double, DateTime>)DateTimeFunctions.AddMilliseconds), method => new FunctionDateTime_DateTime_double(method));
            BuiltinFunctionDelegates.Add(M((Func<DateTime, int, DateTime>)DateTimeFunctions.AddMonths), method => new FunctionDateTime_DateTime_int(method));
            BuiltinFunctionDelegates.Add(M((Func<DateTime, int, DateTime>)DateTimeFunctions.AddYears), method => new FunctionDateTime_DateTime_int(method));
            BuiltinFunctionDelegates.Add(M((Func<double, int, double>)MathFunctions.Round), method => new Functiondouble_double_int___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<double, double>)MathFunctions.Ceil), method => new Functiondouble_double(method));
            BuiltinFunctionDelegates.Add(M((Func<double, double>)MathFunctions.Floor), method => new Functiondouble_double(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, IEnumerable, IEnumerable>)ArrayFunctions.AddRange), method => new FunctionIEnumerable_IEnumerable_IEnumerable(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, IEnumerable, IEnumerable>)ArrayFunctions.Concat), method => new FunctionIEnumerable_IEnumerable_IEnumerable(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, int, object, IEnumerable>)ArrayFunctions.InsertAt), method => new FunctionIEnumerable_IEnumerable_int_object(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, int, IEnumerable>)ArrayFunctions.Limit), method => new FunctionIEnumerable_IEnumerable_int(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, int, IEnumerable>)ArrayFunctions.Offset), method => new FunctionIEnumerable_IEnumerable_int(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, object, IEnumerable>)ArrayFunctions.Add), method => new FunctionIEnumerable_IEnumerable_object(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, IEnumerable>)ArrayFunctions.Compact), method => new FunctionIEnumerable_IEnumerable(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, IEnumerable>)ArrayFunctions.Reverse), method => new FunctionIEnumerable_IEnumerable(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, IEnumerable>)ArrayFunctions.Uniq), method => new FunctionIEnumerable_IEnumerable(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, IEnumerable>)StringFunctions.Split), method => new FunctionIEnumerable_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, object, string, IEnumerable>)ArrayFunctions.Sort), method => new FunctionIEnumerable_TemplateContext_SourceSpan_object_string___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, object, string, IEnumerable>)ArrayFunctions.Map), method => new FunctionIEnumerable_TemplateContext_SourceSpan_object_string(method));
            BuiltinFunctionDelegates.Add(M((Func<IList, int, IList>)ArrayFunctions.RemoveAt), method => new FunctionIList_IList_int(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, int>)ArrayFunctions.Size), method => new Functionint_IEnumerable(method));
            BuiltinFunctionDelegates.Add(M((Func<object, int>)ObjectFunctions.Size), method => new Functionint_object(method));
            BuiltinFunctionDelegates.Add(M((Func<string, int>)StringFunctions.Size), method => new Functionint_string(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, object>)ArrayFunctions.First), method => new Functionobject_IEnumerable(method));
            BuiltinFunctionDelegates.Add(M((Func<IEnumerable, object>)ArrayFunctions.Last), method => new Functionobject_IEnumerable(method));
            BuiltinFunctionDelegates.Add(M((Func<object, object, object>)ObjectFunctions.Default), method => new Functionobject_object_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, double, object, object>)MathFunctions.DividedBy), method => new Functionobject_TemplateContext_SourceSpan_double_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, IList, object, object>)ArrayFunctions.Cycle), method => new Functionobject_TemplateContext_SourceSpan_IList_object___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, int, int, object>)MathFunctions.Random), method => new Functionobject_TemplateContext_SourceSpan_int_int(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, object, object, object>)MathFunctions.Minus), method => new Functionobject_TemplateContext_SourceSpan_object_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, object, object, object>)MathFunctions.Modulo), method => new Functionobject_TemplateContext_SourceSpan_object_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, object, object, object>)MathFunctions.Plus), method => new Functionobject_TemplateContext_SourceSpan_object_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, object, object, object>)MathFunctions.Times), method => new Functionobject_TemplateContext_SourceSpan_object_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, object, object>)MathFunctions.Abs), method => new Functionobject_TemplateContext_SourceSpan_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, object, object>)ObjectFunctions.Eval), method => new Functionobject_TemplateContext_SourceSpan_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, object, object>)ObjectFunctions.EvalTemplate), method => new Functionobject_TemplateContext_SourceSpan_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, string, object>)StringFunctions.ToInt), method => new Functionobject_TemplateContext_string(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, string, object>)StringFunctions.ToLong), method => new Functionobject_TemplateContext_string(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, string, object>)StringFunctions.ToFloat), method => new Functionobject_TemplateContext_string(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, string, object>)StringFunctions.ToDouble), method => new Functionobject_TemplateContext_string(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, object, ScriptArray>)ObjectFunctions.Keys), method => new FunctionScriptArray_TemplateContext_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, object, ScriptArray>)ObjectFunctions.Values), method => new FunctionScriptArray_TemplateContext_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, string, string, string, ScriptArray>)RegexFunctions.Match), method => new FunctionScriptArray_TemplateContext_string_string_string___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, string, string, string, ScriptArray>)RegexFunctions.Matches), method => new FunctionScriptArray_TemplateContext_string_string_string___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, string, string, string, ScriptArray>)RegexFunctions.Split), method => new FunctionScriptArray_TemplateContext_string_string_string___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, IEnumerable, object, ScriptRange>)ArrayFunctions.Each), method => new FunctionScriptRange_TemplateContext_SourceSpan_IEnumerable_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, IEnumerable, object, ScriptRange>)ArrayFunctions.Filter), method => new FunctionScriptRange_TemplateContext_SourceSpan_IEnumerable_object(method));
            BuiltinFunctionDelegates.Add(M((Func<string>)MathFunctions.Uuid), method => new Functionstring(method));
            BuiltinFunctionDelegates.Add(M((Func<int, string, string, string>)StringFunctions.Pluralize), method => new Functionstring_int_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<object, string>)ObjectFunctions.Typeof), method => new Functionstring_object(method));
            BuiltinFunctionDelegates.Add(M((Func<string, int, int, string>)StringFunctions.Slice1), method => new Functionstring_string_int_int___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<string, int, string, string>)StringFunctions.Truncate), method => new Functionstring_string_int_string___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<string, int, string, string>)StringFunctions.Truncatewords), method => new Functionstring_string_int_string___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<string, int, string>)StringFunctions.PadLeft), method => new Functionstring_string_int(method));
            BuiltinFunctionDelegates.Add(M((Func<string, int, string>)StringFunctions.PadRight), method => new Functionstring_string_int(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, string, bool, string>)StringFunctions.ReplaceFirst), method => new Functionstring_string_string_string_bool___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, string, string>)StringFunctions.Replace), method => new Functionstring_string_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, string>)StringFunctions.Append), method => new Functionstring_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, string>)StringFunctions.Prepend), method => new Functionstring_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, string>)StringFunctions.Remove), method => new Functionstring_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, string>)StringFunctions.RemoveFirst), method => new Functionstring_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, string>)StringFunctions.RemoveLast), method => new Functionstring_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, string>)StringFunctions.HmacSha1), method => new Functionstring_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, string>)StringFunctions.HmacSha256), method => new Functionstring_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string, string>)StringFunctions.HmacSha512), method => new Functionstring_string_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)HtmlFunctions.Escape), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)HtmlFunctions.NewlineToBr), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)HtmlFunctions.UrlEncode), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)HtmlFunctions.UrlEscape), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)RegexFunctions.Escape), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)RegexFunctions.Unescape), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Escape), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Capitalize), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Capitalizewords), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Downcase), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Handleize), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Literal), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.LStrip), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.RStrip), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Strip), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.StripNewlines), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Upcase), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Md5), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Sha1), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Sha256), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Sha512), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Base64Encode), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<string, string>)StringFunctions.Base64Decode), method => new Functionstring_string(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, object, string>)ObjectFunctions.Kind), method => new Functionstring_TemplateContext_object(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, IEnumerable, string, object, string>)ArrayFunctions.Join), method => new Functionstring_TemplateContext_SourceSpan_IEnumerable_string_object___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, object, string, string, string>)MathFunctions.Format), method => new Functionstring_TemplateContext_SourceSpan_object_string_string___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, SourceSpan, object, string, string, string>)ObjectFunctions.Format), method => new Functionstring_TemplateContext_SourceSpan_object_string_string___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, string, string, string, string, string, string>)DateTimeFunctions.ParseToString), method => new Functionstring_TemplateContext_string_string___Opt_string___Opt_string___Opt_string___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, string, string, string, string, string>)RegexFunctions.Replace), method => new Functionstring_TemplateContext_string_string_string_string___Opt(method));
            BuiltinFunctionDelegates.Add(M((Func<TemplateContext, string, string>)HtmlFunctions.Strip), method => new Functionstring_TemplateContext_string(method));
            BuiltinFunctionDelegates.Add(M((Func<double, TimeSpan>)TimeSpanFunctions.FromDays), method => new FunctionTimeSpan_double(method));
            BuiltinFunctionDelegates.Add(M((Func<double, TimeSpan>)TimeSpanFunctions.FromHours), method => new FunctionTimeSpan_double(method));
            BuiltinFunctionDelegates.Add(M((Func<double, TimeSpan>)TimeSpanFunctions.FromMinutes), method => new FunctionTimeSpan_double(method));
            BuiltinFunctionDelegates.Add(M((Func<double, TimeSpan>)TimeSpanFunctions.FromSeconds), method => new FunctionTimeSpan_double(method));
            BuiltinFunctionDelegates.Add(M((Func<double, TimeSpan>)TimeSpanFunctions.FromMilliseconds), method => new FunctionTimeSpan_double(method));
            BuiltinFunctionDelegates.Add(M((Func<string, TimeSpan>)TimeSpanFunctions.Parse), method => new FunctionTimeSpan_string(method));

        }

        /// <summary>
        /// Optimized custom function for: bool (IEnumerable, object)
        /// </summary>
        private partial class Functionbool_IEnumerable_object : DynamicCustomFunction
        {
            private delegate bool InternalDelegate(IEnumerable arg0, object arg1);

            private readonly InternalDelegate _delegate;

            public Functionbool_IEnumerable_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IEnumerable)arguments[0];
                var arg1 = (object)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: bool (object)
        /// </summary>
        private partial class Functionbool_object : DynamicCustomFunction
        {
            private delegate bool InternalDelegate(object arg0);

            private readonly InternalDelegate _delegate;

            public Functionbool_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (object)arguments[0];

                return _delegate(arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: bool (string, string)
        /// </summary>
        private partial class Functionbool_string_string : DynamicCustomFunction
        {
            private delegate bool InternalDelegate(string arg0, string arg1);

            private readonly InternalDelegate _delegate;

            public Functionbool_string_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];
                var arg1 = (string)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: bool (string)
        /// </summary>
        private partial class Functionbool_string : DynamicCustomFunction
        {
            private delegate bool InternalDelegate(string arg0);

            private readonly InternalDelegate _delegate;

            public Functionbool_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];

                return _delegate(arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: bool (TemplateContext, SourceSpan, IEnumerable, object, Object[])
        /// </summary>
        private partial class Functionbool_TemplateContext_SourceSpan_IEnumerable_object_objectArray : DynamicCustomFunction
        {
            private delegate bool InternalDelegate(TemplateContext arg0, SourceSpan arg1, IEnumerable arg2, object arg3, Object[] arg4);

            private readonly InternalDelegate _delegate;

            public Functionbool_TemplateContext_SourceSpan_IEnumerable_object_objectArray(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IEnumerable)arguments[0];
                var arg1 = (object)arguments[1];
                var arg2 = ((ScriptArray)arguments[2]).ToArray();

                return _delegate(context, callerContext.Span, arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Optimized custom function for: DateTime ()
        /// </summary>
        private partial class FunctionDateTime : DynamicCustomFunction
        {
            private delegate DateTime InternalDelegate();

            private readonly InternalDelegate _delegate;

            public FunctionDateTime(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {

                return _delegate();
            }
        }

        /// <summary>
        /// Optimized custom function for: DateTime (DateTime, double)
        /// </summary>
        private partial class FunctionDateTime_DateTime_double : DynamicCustomFunction
        {
            private delegate DateTime InternalDelegate(DateTime arg0, double arg1);

            private readonly InternalDelegate _delegate;

            public FunctionDateTime_DateTime_double(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (DateTime)arguments[0];
                var arg1 = (double)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: DateTime (DateTime, int)
        /// </summary>
        private partial class FunctionDateTime_DateTime_int : DynamicCustomFunction
        {
            private delegate DateTime InternalDelegate(DateTime arg0, int arg1);

            private readonly InternalDelegate _delegate;

            public FunctionDateTime_DateTime_int(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (DateTime)arguments[0];
                var arg1 = (int)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: double (double, int = ...)
        /// </summary>
        private partial class Functiondouble_double_int___Opt : DynamicCustomFunction
        {
            private delegate double InternalDelegate(double arg0, int arg1);

            private readonly InternalDelegate _delegate;

            public Functiondouble_double_int___Opt(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (double)arguments[0];
                var arg1 = (int)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: double (double)
        /// </summary>
        private partial class Functiondouble_double : DynamicCustomFunction
        {
            private delegate double InternalDelegate(double arg0);

            private readonly InternalDelegate _delegate;

            public Functiondouble_double(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (double)arguments[0];

                return _delegate(arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: IEnumerable (IEnumerable, IEnumerable)
        /// </summary>
        private partial class FunctionIEnumerable_IEnumerable_IEnumerable : DynamicCustomFunction
        {
            private delegate IEnumerable InternalDelegate(IEnumerable arg0, IEnumerable arg1);

            private readonly InternalDelegate _delegate;

            public FunctionIEnumerable_IEnumerable_IEnumerable(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IEnumerable)arguments[0];
                var arg1 = (IEnumerable)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: IEnumerable (IEnumerable, int, object)
        /// </summary>
        private partial class FunctionIEnumerable_IEnumerable_int_object : DynamicCustomFunction
        {
            private delegate IEnumerable InternalDelegate(IEnumerable arg0, int arg1, object arg2);

            private readonly InternalDelegate _delegate;

            public FunctionIEnumerable_IEnumerable_int_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IEnumerable)arguments[0];
                var arg1 = (int)arguments[1];
                var arg2 = (object)arguments[2];

                return _delegate(arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Optimized custom function for: IEnumerable (IEnumerable, int)
        /// </summary>
        private partial class FunctionIEnumerable_IEnumerable_int : DynamicCustomFunction
        {
            private delegate IEnumerable InternalDelegate(IEnumerable arg0, int arg1);

            private readonly InternalDelegate _delegate;

            public FunctionIEnumerable_IEnumerable_int(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IEnumerable)arguments[0];
                var arg1 = (int)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: IEnumerable (IEnumerable, object)
        /// </summary>
        private partial class FunctionIEnumerable_IEnumerable_object : DynamicCustomFunction
        {
            private delegate IEnumerable InternalDelegate(IEnumerable arg0, object arg1);

            private readonly InternalDelegate _delegate;

            public FunctionIEnumerable_IEnumerable_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IEnumerable)arguments[0];
                var arg1 = (object)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: IEnumerable (IEnumerable)
        /// </summary>
        private partial class FunctionIEnumerable_IEnumerable : DynamicCustomFunction
        {
            private delegate IEnumerable InternalDelegate(IEnumerable arg0);

            private readonly InternalDelegate _delegate;

            public FunctionIEnumerable_IEnumerable(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IEnumerable)arguments[0];

                return _delegate(arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: IEnumerable (string, string)
        /// </summary>
        private partial class FunctionIEnumerable_string_string : DynamicCustomFunction
        {
            private delegate IEnumerable InternalDelegate(string arg0, string arg1);

            private readonly InternalDelegate _delegate;

            public FunctionIEnumerable_string_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];
                var arg1 = (string)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: IEnumerable (TemplateContext, SourceSpan, object, string = ...)
        /// </summary>
        private partial class FunctionIEnumerable_TemplateContext_SourceSpan_object_string___Opt : DynamicCustomFunction
        {
            private delegate IEnumerable InternalDelegate(TemplateContext arg0, SourceSpan arg1, object arg2, string arg3);

            private readonly InternalDelegate _delegate;

            public FunctionIEnumerable_TemplateContext_SourceSpan_object_string___Opt(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (object)arguments[0];
                var arg1 = (string)arguments[1];

                return _delegate(context, callerContext.Span, arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: IEnumerable (TemplateContext, SourceSpan, object, string)
        /// </summary>
        private partial class FunctionIEnumerable_TemplateContext_SourceSpan_object_string : DynamicCustomFunction
        {
            private delegate IEnumerable InternalDelegate(TemplateContext arg0, SourceSpan arg1, object arg2, string arg3);

            private readonly InternalDelegate _delegate;

            public FunctionIEnumerable_TemplateContext_SourceSpan_object_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (object)arguments[0];
                var arg1 = (string)arguments[1];

                return _delegate(context, callerContext.Span, arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: IList (IList, int)
        /// </summary>
        private partial class FunctionIList_IList_int : DynamicCustomFunction
        {
            private delegate IList InternalDelegate(IList arg0, int arg1);

            private readonly InternalDelegate _delegate;

            public FunctionIList_IList_int(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IList)arguments[0];
                var arg1 = (int)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: int (IEnumerable)
        /// </summary>
        private partial class Functionint_IEnumerable : DynamicCustomFunction
        {
            private delegate int InternalDelegate(IEnumerable arg0);

            private readonly InternalDelegate _delegate;

            public Functionint_IEnumerable(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IEnumerable)arguments[0];

                return _delegate(arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: int (object)
        /// </summary>
        private partial class Functionint_object : DynamicCustomFunction
        {
            private delegate int InternalDelegate(object arg0);

            private readonly InternalDelegate _delegate;

            public Functionint_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (object)arguments[0];

                return _delegate(arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: int (string)
        /// </summary>
        private partial class Functionint_string : DynamicCustomFunction
        {
            private delegate int InternalDelegate(string arg0);

            private readonly InternalDelegate _delegate;

            public Functionint_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];

                return _delegate(arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: object (IEnumerable)
        /// </summary>
        private partial class Functionobject_IEnumerable : DynamicCustomFunction
        {
            private delegate object InternalDelegate(IEnumerable arg0);

            private readonly InternalDelegate _delegate;

            public Functionobject_IEnumerable(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IEnumerable)arguments[0];

                return _delegate(arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: object (object, object)
        /// </summary>
        private partial class Functionobject_object_object : DynamicCustomFunction
        {
            private delegate object InternalDelegate(object arg0, object arg1);

            private readonly InternalDelegate _delegate;

            public Functionobject_object_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (object)arguments[0];
                var arg1 = (object)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: object (TemplateContext, SourceSpan, double, object)
        /// </summary>
        private partial class Functionobject_TemplateContext_SourceSpan_double_object : DynamicCustomFunction
        {
            private delegate object InternalDelegate(TemplateContext arg0, SourceSpan arg1, double arg2, object arg3);

            private readonly InternalDelegate _delegate;

            public Functionobject_TemplateContext_SourceSpan_double_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (double)arguments[0];
                var arg1 = (object)arguments[1];

                return _delegate(context, callerContext.Span, arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: object (TemplateContext, SourceSpan, IList, object = ...)
        /// </summary>
        private partial class Functionobject_TemplateContext_SourceSpan_IList_object___Opt : DynamicCustomFunction
        {
            private delegate object InternalDelegate(TemplateContext arg0, SourceSpan arg1, IList arg2, object arg3);

            private readonly InternalDelegate _delegate;

            public Functionobject_TemplateContext_SourceSpan_IList_object___Opt(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IList)arguments[0];
                var arg1 = (object)arguments[1];

                return _delegate(context, callerContext.Span, arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: object (TemplateContext, SourceSpan, int, int)
        /// </summary>
        private partial class Functionobject_TemplateContext_SourceSpan_int_int : DynamicCustomFunction
        {
            private delegate object InternalDelegate(TemplateContext arg0, SourceSpan arg1, int arg2, int arg3);

            private readonly InternalDelegate _delegate;

            public Functionobject_TemplateContext_SourceSpan_int_int(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (int)arguments[0];
                var arg1 = (int)arguments[1];

                return _delegate(context, callerContext.Span, arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: object (TemplateContext, SourceSpan, object, object)
        /// </summary>
        private partial class Functionobject_TemplateContext_SourceSpan_object_object : DynamicCustomFunction
        {
            private delegate object InternalDelegate(TemplateContext arg0, SourceSpan arg1, object arg2, object arg3);

            private readonly InternalDelegate _delegate;

            public Functionobject_TemplateContext_SourceSpan_object_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (object)arguments[0];
                var arg1 = (object)arguments[1];

                return _delegate(context, callerContext.Span, arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: object (TemplateContext, SourceSpan, object)
        /// </summary>
        private partial class Functionobject_TemplateContext_SourceSpan_object : DynamicCustomFunction
        {
            private delegate object InternalDelegate(TemplateContext arg0, SourceSpan arg1, object arg2);

            private readonly InternalDelegate _delegate;

            public Functionobject_TemplateContext_SourceSpan_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (object)arguments[0];

                return _delegate(context, callerContext.Span, arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: object (TemplateContext, string)
        /// </summary>
        private partial class Functionobject_TemplateContext_string : DynamicCustomFunction
        {
            private delegate object InternalDelegate(TemplateContext arg0, string arg1);

            private readonly InternalDelegate _delegate;

            public Functionobject_TemplateContext_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];

                return _delegate(context, arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: ScriptArray (TemplateContext, object)
        /// </summary>
        private partial class FunctionScriptArray_TemplateContext_object : DynamicCustomFunction
        {
            private delegate ScriptArray InternalDelegate(TemplateContext arg0, object arg1);

            private readonly InternalDelegate _delegate;

            public FunctionScriptArray_TemplateContext_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (object)arguments[0];

                return _delegate(context, arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: ScriptArray (TemplateContext, string, string, string = ...)
        /// </summary>
        private partial class FunctionScriptArray_TemplateContext_string_string_string___Opt : DynamicCustomFunction
        {
            private delegate ScriptArray InternalDelegate(TemplateContext arg0, string arg1, string arg2, string arg3);

            private readonly InternalDelegate _delegate;

            public FunctionScriptArray_TemplateContext_string_string_string___Opt(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];
                var arg1 = (string)arguments[1];
                var arg2 = (string)arguments[2];

                return _delegate(context, arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Optimized custom function for: ScriptRange (TemplateContext, SourceSpan, IEnumerable, object)
        /// </summary>
        private partial class FunctionScriptRange_TemplateContext_SourceSpan_IEnumerable_object : DynamicCustomFunction
        {
            private delegate ScriptRange InternalDelegate(TemplateContext arg0, SourceSpan arg1, IEnumerable arg2, object arg3);

            private readonly InternalDelegate _delegate;

            public FunctionScriptRange_TemplateContext_SourceSpan_IEnumerable_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IEnumerable)arguments[0];
                var arg1 = (object)arguments[1];

                return _delegate(context, callerContext.Span, arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: string ()
        /// </summary>
        private partial class Functionstring : DynamicCustomFunction
        {
            private delegate string InternalDelegate();

            private readonly InternalDelegate _delegate;

            public Functionstring(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {

                return _delegate();
            }
        }

        /// <summary>
        /// Optimized custom function for: string (int, string, string)
        /// </summary>
        private partial class Functionstring_int_string_string : DynamicCustomFunction
        {
            private delegate string InternalDelegate(int arg0, string arg1, string arg2);

            private readonly InternalDelegate _delegate;

            public Functionstring_int_string_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (int)arguments[0];
                var arg1 = (string)arguments[1];
                var arg2 = (string)arguments[2];

                return _delegate(arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (object)
        /// </summary>
        private partial class Functionstring_object : DynamicCustomFunction
        {
            private delegate string InternalDelegate(object arg0);

            private readonly InternalDelegate _delegate;

            public Functionstring_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (object)arguments[0];

                return _delegate(arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (string, int, int = ...)
        /// </summary>
        private partial class Functionstring_string_int_int___Opt : DynamicCustomFunction
        {
            private delegate string InternalDelegate(string arg0, int arg1, int arg2);

            private readonly InternalDelegate _delegate;

            public Functionstring_string_int_int___Opt(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];
                var arg1 = (int)arguments[1];
                var arg2 = (int)arguments[2];

                return _delegate(arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (string, int, string = ...)
        /// </summary>
        private partial class Functionstring_string_int_string___Opt : DynamicCustomFunction
        {
            private delegate string InternalDelegate(string arg0, int arg1, string arg2);

            private readonly InternalDelegate _delegate;

            public Functionstring_string_int_string___Opt(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];
                var arg1 = (int)arguments[1];
                var arg2 = (string)arguments[2];

                return _delegate(arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (string, int)
        /// </summary>
        private partial class Functionstring_string_int : DynamicCustomFunction
        {
            private delegate string InternalDelegate(string arg0, int arg1);

            private readonly InternalDelegate _delegate;

            public Functionstring_string_int(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];
                var arg1 = (int)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (string, string, string, bool = ...)
        /// </summary>
        private partial class Functionstring_string_string_string_bool___Opt : DynamicCustomFunction
        {
            private delegate string InternalDelegate(string arg0, string arg1, string arg2, bool arg3);

            private readonly InternalDelegate _delegate;

            public Functionstring_string_string_string_bool___Opt(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];
                var arg1 = (string)arguments[1];
                var arg2 = (string)arguments[2];
                var arg3 = (bool)arguments[3];

                return _delegate(arg0, arg1, arg2, arg3);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (string, string, string)
        /// </summary>
        private partial class Functionstring_string_string_string : DynamicCustomFunction
        {
            private delegate string InternalDelegate(string arg0, string arg1, string arg2);

            private readonly InternalDelegate _delegate;

            public Functionstring_string_string_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];
                var arg1 = (string)arguments[1];
                var arg2 = (string)arguments[2];

                return _delegate(arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (string, string)
        /// </summary>
        private partial class Functionstring_string_string : DynamicCustomFunction
        {
            private delegate string InternalDelegate(string arg0, string arg1);

            private readonly InternalDelegate _delegate;

            public Functionstring_string_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];
                var arg1 = (string)arguments[1];

                return _delegate(arg0, arg1);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (string)
        /// </summary>
        private partial class Functionstring_string : DynamicCustomFunction
        {
            private delegate string InternalDelegate(string arg0);

            private readonly InternalDelegate _delegate;

            public Functionstring_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];

                return _delegate(arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (TemplateContext, object)
        /// </summary>
        private partial class Functionstring_TemplateContext_object : DynamicCustomFunction
        {
            private delegate string InternalDelegate(TemplateContext arg0, object arg1);

            private readonly InternalDelegate _delegate;

            public Functionstring_TemplateContext_object(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (object)arguments[0];

                return _delegate(context, arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (TemplateContext, SourceSpan, IEnumerable, string, object = ...)
        /// </summary>
        private partial class Functionstring_TemplateContext_SourceSpan_IEnumerable_string_object___Opt : DynamicCustomFunction
        {
            private delegate string InternalDelegate(TemplateContext arg0, SourceSpan arg1, IEnumerable arg2, string arg3, object arg4);

            private readonly InternalDelegate _delegate;

            public Functionstring_TemplateContext_SourceSpan_IEnumerable_string_object___Opt(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (IEnumerable)arguments[0];
                var arg1 = (string)arguments[1];
                var arg2 = (object)arguments[2];

                return _delegate(context, callerContext.Span, arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (TemplateContext, SourceSpan, object, string, string = ...)
        /// </summary>
        private partial class Functionstring_TemplateContext_SourceSpan_object_string_string___Opt : DynamicCustomFunction
        {
            private delegate string InternalDelegate(TemplateContext arg0, SourceSpan arg1, object arg2, string arg3, string arg4);

            private readonly InternalDelegate _delegate;

            public Functionstring_TemplateContext_SourceSpan_object_string_string___Opt(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (object)arguments[0];
                var arg1 = (string)arguments[1];
                var arg2 = (string)arguments[2];

                return _delegate(context, callerContext.Span, arg0, arg1, arg2);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (TemplateContext, string, string = ..., string = ..., string = ..., string = ...)
        /// </summary>
        private partial class Functionstring_TemplateContext_string_string___Opt_string___Opt_string___Opt_string___Opt : DynamicCustomFunction
        {
            private delegate string InternalDelegate(TemplateContext arg0, string arg1, string arg2, string arg3, string arg4, string arg5);

            private readonly InternalDelegate _delegate;

            public Functionstring_TemplateContext_string_string___Opt_string___Opt_string___Opt_string___Opt(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];
                var arg1 = (string)arguments[1];
                var arg2 = (string)arguments[2];
                var arg3 = (string)arguments[3];
                var arg4 = (string)arguments[4];

                return _delegate(context, arg0, arg1, arg2, arg3, arg4);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (TemplateContext, string, string, string, string = ...)
        /// </summary>
        private partial class Functionstring_TemplateContext_string_string_string_string___Opt : DynamicCustomFunction
        {
            private delegate string InternalDelegate(TemplateContext arg0, string arg1, string arg2, string arg3, string arg4);

            private readonly InternalDelegate _delegate;

            public Functionstring_TemplateContext_string_string_string_string___Opt(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];
                var arg1 = (string)arguments[1];
                var arg2 = (string)arguments[2];
                var arg3 = (string)arguments[3];

                return _delegate(context, arg0, arg1, arg2, arg3);
            }
        }

        /// <summary>
        /// Optimized custom function for: string (TemplateContext, string)
        /// </summary>
        private partial class Functionstring_TemplateContext_string : DynamicCustomFunction
        {
            private delegate string InternalDelegate(TemplateContext arg0, string arg1);

            private readonly InternalDelegate _delegate;

            public Functionstring_TemplateContext_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];

                return _delegate(context, arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: TimeSpan (double)
        /// </summary>
        private partial class FunctionTimeSpan_double : DynamicCustomFunction
        {
            private delegate TimeSpan InternalDelegate(double arg0);

            private readonly InternalDelegate _delegate;

            public FunctionTimeSpan_double(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (double)arguments[0];

                return _delegate(arg0);
            }
        }

        /// <summary>
        /// Optimized custom function for: TimeSpan (string)
        /// </summary>
        private partial class FunctionTimeSpan_string : DynamicCustomFunction
        {
            private delegate TimeSpan InternalDelegate(string arg0);

            private readonly InternalDelegate _delegate;

            public FunctionTimeSpan_string(MethodInfo method) : base(method)
            {
                _delegate = (InternalDelegate)method.CreateDelegate(typeof(InternalDelegate));
            }

            public override object Invoke(TemplateContext context, ScriptNode callerContext, ScriptArray arguments, ScriptBlockStatement blockStatement)
            {
                var arg0 = (string)arguments[0];

                return _delegate(arg0);
            }
        }

    }
}

