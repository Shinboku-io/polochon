using FluentValidation;
using Polochon.Abstractions.Results;

namespace Polochon.Validation.FluentValidation
{
    /// <summary>
    /// FluentValidation extensions attaching a <see cref="ResultCode"/> to a rule.
    /// </summary>
    public static class RuleBuilderExtensions
    {
        /// <summary>
        /// Reports <paramref name="code"/> to the caller when this rule is the first to fail: sets it
        /// as the failure's error code (<see cref="ResultCode.Status"/>) and custom state (read back
        /// by <see cref="FluentValidationMessageValidator{TRequest, TResponse}"/>). A failing rule
        /// without one is reported as <see cref="ResultCode.ValidationFailed"/>.
        /// </summary>
        /// <typeparam name="T">The type being validated.</typeparam>
        /// <typeparam name="TProperty">The type of the validated property.</typeparam>
        /// <param name="rule">The rule to attach the code to.</param>
        /// <param name="code">The code reported when the rule fails.</param>
        public static IRuleBuilderOptions<T, TProperty> WithResultCode<T, TProperty>(this IRuleBuilderOptions<T, TProperty> rule, ResultCode code)
        {
            ArgumentNullException.ThrowIfNull(code);

            return rule.WithErrorCode(code.Status).WithState(_ => code);
        }
    }
}
