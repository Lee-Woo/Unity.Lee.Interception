

using System.Reflection;
using Unity.Interception.PolicyInjection.Policies;
using Unity.Interception.Utilities;

namespace Unity.Interception.PolicyInjection.MatchingRules
{
    /// <summary>
    /// An implementation of <see cref="IMatchingRule"/> that checks to see if the
    /// member (or type containing that member) have any <see cref="HandlerAttribute"/>s.
    /// </summary>
    public class AttributeDrivenPolicyMatchingRule : IMatchingRule
    {
        #region IMatchingRule Members

        /// <summary>
        /// Checks to see if <paramref name="member"/> matches the rule.
        /// </summary>
        /// <remarks>Returns true if any <see cref="HandlerAttribute"/>s are present on the method
        /// or the type containing that method.</remarks>
        /// <param name="member">Member to check.</param>
        /// <returns>true if member matches, false if not.</returns>
        public bool Matches(MethodBase member)
        {
            return ReflectionHelper.GetAllAttributes<HandlerAttribute>(member, true).Length > 0;
        }

        #endregion
    }
}
