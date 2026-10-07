namespace Polochon.Abstractions.Results
{
    /// <summary>
    /// The outcome of a command as reported to its caller: a numeric code and a stable string
    /// status. Zero or positive codes report a success (<see cref="Ok"/> being the generic one),
    /// negative codes name the issue.
    /// </summary>
    /// <remarks>
    /// Each module defines its own codes in a catalog of its own (e.g. <c>InventoryErrors</c>):
    /// positive values for business successes (e.g. <c>ITEM_CREATED</c>), negative values for
    /// failures. The kernel's own failure codes are the negative values declared here.
    /// </remarks>
    public sealed record ResultCode
    {
        /// <summary>The command succeeded.</summary>
        public static readonly ResultCode Ok = new() { Code = 0, Status = "OK" };

        /// <summary>The command failed on an exception nobody anticipated (bug, infrastructure failure...).</summary>
        public static readonly ResultCode UnexpectedError = new() { Code = -1, Status = "UNEXPECTED_ERROR" };

        /// <summary>A validator rejected the command without giving a more specific code.</summary>
        public static readonly ResultCode ValidationFailed = new() { Code = -2, Status = "VALIDATION_FAILED" };

        /// <summary>The numeric code; zero or positive on success, negative on failure.</summary>
        public required int Code { get; init; }

        /// <summary>The stable, machine-readable status, e.g. <c>ITEM_NAME_LENGTH</c>.</summary>
        public required string Status { get; init; }

        /// <summary>Whether this code reports success, i.e. <see cref="Code"/> is zero or positive.</summary>
        public bool IsOk => Code >= Ok.Code;
    }
}
