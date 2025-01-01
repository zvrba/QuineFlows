using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Quine.Schemas.Core
{
    /// <summary>
    /// This method is called by <see cref="TemplateVariableProcessor.Replace(string, TemplateVariableMapper)"/> to replace
    /// parameters with values.
    /// </summary>
    /// <param name="name">
    /// Parameter name to replace.  The name does NOT contain <c>$()</c> delimiters.  For example, if the template contains parameter
    /// named <c>$(MyParameter)</c>, this method will be invoked with <c>MyParameter</c>.
    /// </param>
    /// <returns>
    /// The string value that name maps to, or null if no mapping exists.
    /// </returns>
    /// <exception cref="KeyNotFoundException">If name does cannot be mapped to a value.</exception>
    public delegate string TemplateVariableMapper(string name);

    /// <summary>
    /// Thrown by methods of <see cref="TemplateVariableProcessor"/> on invalid inputs.
    /// </summary>
    public sealed class TemplateFormatException : FormatException {
        internal TemplateFormatException(HRCatalog.QHMessage hMessage, string parameterName = null, Exception inner = null)
            : base(hMessage.Message, inner)
        {
            HResult = hMessage.HResult;
            ParameterName = parameterName;
        }

        /// <summary>
        /// The involved parameter, if any.
        /// </summary>
        public string ParameterName { get; }

        /// <inheritdoc/>
        public override string Message {
            get {
                var ret = base.Message;
                if (!string.IsNullOrEmpty(ParameterName))
                    ret += $"Parameter name: `{ParameterName}`.";
                return ret;
            }
        }
    }

    /// <summary>
    /// Utility methods for handling strings contatining embedded variables of the form <c>$(VariableName)</c>.
    /// </summary>
    public static class TemplateVariableProcessor
    {
        static readonly Regex ParameterRx = new Regex(@"\$\(([a-zA-Z0-9)]+)\)", RegexOptions.Compiled | RegexOptions.Singleline);

        /// <summary>
        /// Replaces variables occurring in <paramref name="input"/> as specified by <paramref name="valueMapper"/>.
        /// Only single-line strings are supported.
        /// </summary>
        /// <param name="input">
        /// String with variables to replace.
        /// </param>
        /// <param name="valueMapper">
        /// A method mapping a variable name to replacement string.
        /// </param>
        /// <returns>
        /// Input string with variables replaced with their respective values.
        /// </returns>
        /// <exception cref="TemplateFormatException">
        /// <paramref name="valueMapper"/> returned null or threw.
        /// </exception>
        public static string Replace(string input, TemplateVariableMapper valueMapper) {
            return ParameterRx.Replace(input, GetReplacement);

            string GetReplacement(Match m) {
                var parameterName = m.Groups[1].Value;
                string ret = null;
                Exception exn = null;
                try {
                    if ((ret = valueMapper(parameterName)) == null)
                        exn = new TemplateFormatException(HRCatalog.QHSchemas.Core.TemplateFormat_MissingValue, parameterName);
                }
                catch (Exception e) {
                    exn = new TemplateFormatException(HRCatalog.QHSchemas.Core.TemplateFormat_MissingValue, parameterName, e);
                }
                return ret ?? throw exn;
            }
        }

        /// <summary>
        /// Utility overload for <see cref="Replace(string, TemplateVariableMapper)"/>.
        /// </summary>
        /// <param name="input">String with variables to replace.</param>
        /// <param name="valueMap">A dictionary providing replacement values for variables.</param>
        /// <exception cref="TemplateFormatException">Input contains a variable not present in <paramref name="valueMap"/>.</exception>
        public static string Replace(string input, Dictionary<string, string> valueMap) =>
            Replace(input, s => valueMap.TryGetValue(s, out var v) ? v : null);

        /// <summary>
        /// Check whether the input string matches the template.  See remarks.
        /// </summary>
        /// <param name="template">Template string with variables to match against.</param>
        /// <param name="input">Input string to match.</param>
        /// <param name="translator">
        /// A method mapping variable name to matching regex.  If null is returned, <c>IsMatch</c> will return null.
        /// To signal errors, the method must throw an exception.  This delegate must return literal string by using
        /// <see cref="Regex.Escape(string)"/>.
        /// </param>
        /// <returns>
        /// Non-null if <paramref name="input"/> matches <paramref name="template"/>.  If template contains any parameters,
        /// the dictionary is populated with parameters as keys and their text as values.  Otherwise, an empty dictionary
        /// is returned, meaning that the input matches the template either literally or that the template is a prefix of input
        /// with <c>'/'</c> in between.
        /// </returns>
        /// <exception cref="TemplateFormatException">
        /// Wraps any exception that <paramref name="translator"/> threw. - OR -
        /// <paramref name="translator"/> returned a string that resulted in an invalid regex - OR - <paramref name="input"/> generates
        /// multiple matches (see remarks) - OR - some parameter occurs multiple times in the template.
        /// </exception>
        /// <remarks>
        /// <para>
        /// This method first converts <paramref name="template"/> to a regex by using <paramref name="translator"/>, as if by calling
        /// <see cref="Replace(string, TemplateVariableMapper)"/>.  The generated regex is then used to match <paramref name="input"/>
        /// and then: if a no matches or a single match are found, null or the match is returned; otherwise (multiple matches),
        /// <see cref="TemplateFormatException"/> is thrown.
        /// </para>
        /// <para>
        /// This method does not cache regular expressions internally; it relies on the static cache built in to <see cref="Regex"/> class.
        /// </para>
        /// </remarks>
        public static Dictionary<string, string> IsMatch(string template, string input, TemplateVariableMapper translator) {
            var pvals = new Dictionary<string, string>();
            var match = ParameterRx.Match(template);
            
            // No parameters in template, template must match input literally or be a path prefix of input.
            if (!match.Success) {
                if (template != input && !input.StartsWith(template + "/"))
                    pvals = null;
                return pvals;
            }

            var pnames = new List<string>(8);
            var sb = new System.Text.StringBuilder(template.Length * 3 / 2);
            int lastMatchEnd = 0;
            do {
                var pname = match.Groups[1].Value;
                if (!GetRegexForParameter(pname, out var psubst))
                    return null;                                // No expansion for parameter.
                if (pnames.Contains(pname))
                    throw new TemplateFormatException(HRCatalog.QHSchemas.Core.TemplateFormat_DuplicateParameter, pname);
                pnames.Add(pname);
                sb.Append(Regex.Escape(template.Substring(lastMatchEnd, match.Index - lastMatchEnd)));
                sb.Append(psubst);                              // NOT escaping, this is supposed to be regex.
                lastMatchEnd = match.Index + match.Length;
                match = match.NextMatch();
            } while (match.Success);
            sb.Append(Regex.Escape(template.Substring(lastMatchEnd)));    // Remaining string after the last match.

            try {
                match = Regex.Match(input, sb.ToString());          // Match and populate dictionary.
            }
            catch (ArgumentException e) {
                throw new TemplateFormatException(HRCatalog.QHSchemas.Core.TemplateFormat_InvalidRegex, inner: e);
            }

            if (!match.Success)
                return null;
            foreach (var pname in pnames)
                pvals[pname] = match.Groups[pname].Value;

            if (match.NextMatch().Success)
                throw new TemplateFormatException(HRCatalog.QHSchemas.Core.TemplateFormat_AmbiguousMatch);

            return pvals;

            bool GetRegexForParameter(string pname, out string ret) {
                try {
                    if ((ret = translator(pname)) == null)
                        return false;
                    ret = string.Format("(?<{0}>{1})", pname, ret);
                    return true;
                }
                catch (Exception e) {
                    throw new TemplateFormatException(HRCatalog.QHSchemas.Core.TemplateFormat_MissingValue, pname, e);
                }
            }
        }
    }
}
