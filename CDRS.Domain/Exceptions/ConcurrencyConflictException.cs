using System;

namespace CDRS.Domain.Exceptions
{
    /// <summary>
    /// Raised when a report was modified by someone else between the moment it
    /// was loaded and the moment the change was saved. Maps to HTTP 409.
    /// Deliberately not a <see cref="DomainException"/> so the two get
    /// different status codes.
    /// </summary>
    public class ConcurrencyConflictException : Exception
    {
        public ConcurrencyConflictException(string message, Exception? inner = null)
            : base(message, inner) { }
    }
}
