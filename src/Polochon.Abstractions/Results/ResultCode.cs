namespace Polochon.Abstractions.Results
{
    /// <summary>
    /// The outcome of a command as reported to its caller: a numeric code and a stable string
    /// status. <see cref="Ok"/> (0) means success; any other value names the issue.
    /// </summary>
    /// <remarks>
    /// Codes below zero are reserved by the kernel. Each module defines its own codes, as positive
    /// values, in a catalog of its own (e.g. <c>InventoryErrors</c>).
    /// </remarks>
    public sealed record ResultCode
    {
        /// <summary>The command succeeded.</summary>
        public static readonly ResultCode Ok = new() { Code = 0, Status = "OK" };

        /// <summary>The command failed on an exception nobody anticipated (bug, infrastructure failure...).</summary>
        public static readonly ResultCode UnexpectedError = new() { Code = -1, Status = "UNEXPECTED_ERROR" };

        /// <summary>A validator rejected the command without giving a more specific code.</summary>
        public static readonly ResultCode ValidationFailed = new() { Code = -2, Status = "VALIDATION_FAILED" };

        /// <summary>The numeric code; 0 on success.</summary>
        public required int Code { get; init; }

        /// <summary>The stable, machine-readable status, e.g. <c>ITEM_NAME_LENGTH</c>.</summary>
        public required string Status { get; init; }

        /// <summary>Whether this code reports success.</summary>
        public bool IsOk => Code == Ok.Code;
    }
}
