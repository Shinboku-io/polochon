namespace Polochon.Abstractions.Domain
{
    /// <summary>
    /// Base class for value objects: immutable types compared by their components rather than
    /// by identity.
    /// </summary>
    public abstract class ValueObject
    {
        /// <summary>
        /// Component-wise equality between two value objects, tolerant of either side being null.
        /// </summary>
        protected static bool EqualOperator(ValueObject? left, ValueObject? right)
        {
            if (left is null ^ right is null)
            {
                // one of the two is null
                return false;
            }

            // the two are ref equals (and maybe the two are nulls) or standard equal check
            return ReferenceEquals(left, right) || left!.Equals(right);
        }

        /// <summary>
        /// The negation of <see cref="EqualOperator"/>.
        /// </summary>
        protected static bool NotEqualOperator(ValueObject? left, ValueObject? right)
        {
            return !EqualOperator(left, right);
        }

        /// <summary>
        /// The components that determine this value object's equality and hash code, in a stable
        /// order.
        /// </summary>
        protected abstract IEnumerable<object?> GetEqualityComponents();

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            if (obj == null || obj.GetType() != GetType())
            {
                return false;
            }

            var other = (ValueObject)obj;
            return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return GetEqualityComponents()
            .Select(x => x != null ? x.GetHashCode() : 0)
            .Aggregate((x, y) => x ^ y);
        }

        /// <summary>
        /// Component-wise equality between two value objects, tolerant of either side being null.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Blocker Code Smell", "S3875:\"operator==\" should not be overloaded on reference types", Justification = "By desing for value object")]
        public static bool operator ==(ValueObject? one, ValueObject? two)
        {
            return EqualOperator(one, two);
        }

        /// <summary>
        /// The negation of the equality operator above.
        /// </summary>
        public static bool operator !=(ValueObject? one, ValueObject? two)
        {
            return NotEqualOperator(one, two);
        }
    }
}
