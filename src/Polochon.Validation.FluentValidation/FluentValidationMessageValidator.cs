using FluentValidation;
using FluentValidation.Results;
using Polochon.Abstractions.CQRS;
using Polochon.Abstractions.Results;

namespace Polochon.Validation.FluentValidation
{
    /// <summary>
    /// Adapts the FluentValidation validators of a message to the kernel's
    /// <see cref="MessageValidator{TRequest, TResponse}"/>: runs every <see cref="IValidator{T}"/>
    /// registered for <typeparamref name="TRequest"/>, in registration order, and hands all their
    /// failures to the kernel, which reports the first one. Registered as an open generic by
    /// <see cref="ServiceRegister.AddPolochonFluentValidation"/>.
    /// </summary>
    /// <typeparam name="TRequest">The type of the request message to validate.</typeparam>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    public sealed class FluentValidationMessageValidator<TRequest, TResponse> : MessageValidator<TRequest, TResponse>
        where TRequest : IMessage<TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> validators;

        /// <summary>
        /// Initializes a new instance of the <see cref="FluentValidationMessageValidator{TRequest, TResponse}"/> class.
        /// </summary>
        /// <param name="validators">The FluentValidation validators registered for <typeparamref name="TRequest"/>.</param>
        public FluentValidationMessageValidator(IEnumerable<IValidator<TRequest>> validators)
        {
            this.validators = validators;
        }

        /// <inheritdoc/>
        protected override async ValueTask<IReadOnlyList<ValidationError>> GetErrorsAsync(TRequest request, CancellationToken cancellationToken)
        {
            var errors = new List<ValidationError>();

            foreach (var validator in validators)
            {
                var result = await validator.ValidateAsync(request, cancellationToken);
                errors.AddRange(result.Errors.Select(ToValidationError));
            }

            return errors;
        }

        private static ValidationError ToValidationError(ValidationFailure failure) => new()
        {
            Code = failure.CustomState as ResultCode ?? ResultCode.ValidationFailed,
            Message = failure.ErrorMessage,
            MemberName = string.IsNullOrEmpty(failure.PropertyName) ? null : failure.PropertyName,
        };
    }
}
