﻿

using System.Collections.Generic;
using System.Reflection;
using Unity.Interception.Utilities;

namespace Unity.Interception.PolicyInjection.MatchingRules
{
    /// <summary>
    /// An <see cref="IMatchingRule"/> that matches methods that have any parameters
    /// of the given types.
    /// </summary>
    public class ParameterTypeMatchingRule : IMatchingRule
    {
        private readonly List<ParameterTypeMatchingInfo> _matches;

        /// <summary>
        /// Creates a new <see cref="ParameterTypeMatchingRule"/> that matches if any of
        /// the method parameters match ones in the given collection.
        /// </summary>
        /// <param name="matches">Collection of <see cref="ParameterTypeMatchingInfo"/> that
        /// describes the types to match.</param>
        public ParameterTypeMatchingRule(IEnumerable<ParameterTypeMatchingInfo> matches)
        {
            _matches = new List<ParameterTypeMatchingInfo>(matches);
        }

        /// <summary>
        /// The list of <see cref="ParameterTypeMatchingInfo"/> describing the parameter types to match.
        /// </summary>
        /// <value>The collection of matches.</value>
        public IEnumerable<ParameterTypeMatchingInfo> ParameterMatches => _matches;

        /// <summary>
        /// Check the given member to see if it has any matching parameters.
        /// </summary>
        /// <param name="member">Member to match.</param>
        /// <returns>true if member matches, false if it doesn't.</returns>
        public bool Matches(MethodBase member)
        {
            Guard.ArgumentNotNull(member, "member");

            ParameterInfo[] parametersInfo = member.GetParameters();

            foreach (ParameterTypeMatchingInfo matchInfo in _matches)
            {
                TypeMatchingRule typeRule =
                    new TypeMatchingRule(matchInfo.Match, matchInfo.IgnoreCase);
                foreach (ParameterInfo paramInfo in parametersInfo)
                {
                    if ((!paramInfo.IsOut && !paramInfo.IsReturn()) &&
                        (matchInfo.Kind == ParameterKind.Input ||
                            matchInfo.Kind == ParameterKind.InputOrOutput))
                    {
                        if (typeRule.Matches(paramInfo.ParameterType))
                        {
                            return true;
                        }
                    }

                    if (paramInfo.IsOut &&
                        (matchInfo.Kind == ParameterKind.Output ||
                            matchInfo.Kind == ParameterKind.InputOrOutput))
                    {
                        if (typeRule.Matches(paramInfo.ParameterType.GetElementType()))
                        {
                            return true;
                        }
                    }

                    if (paramInfo.IsReturn() && matchInfo.Kind == ParameterKind.ReturnValue)
                    {
                        if (typeRule.Matches(paramInfo.ParameterType))
                        {
                            return true;
                        }
                    }
                }
                if (matchInfo.Kind == ParameterKind.ReturnValue)
                {
                    MethodInfo method = member as MethodInfo;
                    if (method != null)
                    {
                        if (typeRule.Matches(method.ReturnType))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Describes the type of parameter to match.
    /// </summary>
    public enum ParameterKind
    {
        /// <summary>
        /// Input parameter
        /// </summary>
        Input,

        /// <summary>
        /// Output parameter
        /// </summary>
        Output,
        
        /// <summary>
        /// Input or output parameter
        /// </summary>
        InputOrOutput,
        
        /// <summary>
        /// Method return value
        /// </summary>
        ReturnValue
    }

    /// <summary>
    /// A class that stores information about a single type to match.
    /// </summary>
    public class ParameterTypeMatchingInfo : MatchingInfo
    {
        /// <summary>
        /// Creates a new uninitialized <see cref="ParameterTypeMatchingInfo"/>.
        /// </summary>
        public ParameterTypeMatchingInfo()
        {
        }

        /// <summary>
        /// Creates a new <see cref="ParameterTypeMatchingInfo"/> matching the given kind of parameter.
        /// </summary>
        /// <param name="kind"><see cref="ParameterKind"/> of parameter to match.</param>
        public ParameterTypeMatchingInfo(ParameterKind kind)
        {
            Kind = kind;
        }

        /// <summary>
        /// Creates a new <see cref="ParameterTypeMatchingInfo"/> matching the given parameter
        /// type and kind.
        /// </summary>
        /// <param name="nameToMatch">Parameter <see cref="System.Type"/> name to match.</param>
        /// <param name="kind"><see cref="ParameterKind"/> of parameter to match.</param>
        public ParameterTypeMatchingInfo(string nameToMatch, ParameterKind kind)
            : base(nameToMatch)
        {
            Kind = kind;
        }

        /// <summary>
        /// Creates a new <see cref="ParameterTypeMatchingInfo"/> matching the given parameter
        /// type and kind.
        /// </summary>
        /// <param name="nameToMatch">Parameter <see cref="System.Type"/> name to match.</param>
        /// <param name="ignoreCase">If false, compare type names using case-sensitive comparison.
        /// If true, compare type names using case-insensitive comparison.</param>
        /// <param name="kind"><see cref="ParameterKind"/> of parameter to match.</param>
        public ParameterTypeMatchingInfo(string nameToMatch, bool ignoreCase, ParameterKind kind)
            : base(nameToMatch, ignoreCase)
        {
            Kind = kind;
        }

        /// <summary>
        /// What kind of parameter to match.
        /// </summary>
        /// <value><see cref="ParameterKind"/> indicating which kind of parameters to match.</value>
        public ParameterKind Kind { get; set; }
    }
}
