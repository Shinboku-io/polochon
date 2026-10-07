namespace Polochon.Abstractions.Telemetry
{
    /// <summary>
    /// Names of the telemetry Polochon emits: instrumentation scopes, metrics and tags. Stable, so
    /// exporters can subscribe to them and dashboards or alerts can query them.
    /// </summary>
    public static class PolochonTelemetry
    {
        /// <summary>
        /// Prefix of every module's <see cref="IModuleTelemetry.ActivitySource"/> and
        /// <see cref="IModuleTelemetry.Meter"/> name.
        /// </summary>
        public const string ModuleSourcePrefix = "Polochon.Modules.";

        /// <summary>
        /// Wildcard matching every module's activity source and meter, e.g. for OpenTelemetry's
        /// <c>AddSource</c>/<c>AddMeter</c>.
        /// </summary>
        public const string AllModulesSourceName = ModuleSourcePrefix + "*";

        /// <summary>
        /// Histogram of the duration, in seconds, of every command and query a module handles.
        /// Its count is the number of messages handled.
        /// </summary>
        public const string MessageDurationMetric = "polochon.message.duration";

        /// <summary>
        /// Histogram of the duration, in seconds, of every call made through an
        /// <see cref="ExternalResourceProxy{TResource}"/>. Its count is the number of calls.
        /// </summary>
        public const string DependencyDurationMetric = "polochon.dependency.duration";

        /// <summary>
        /// Tag: the name of the module that emitted the telemetry.
        /// </summary>
        public const string ModuleTag = "polochon.module";

        /// <summary>
        /// Tag: the type name of the command or query handled, e.g. <c>CreateItemCommand</c>.
        /// </summary>
        public const string MessageTypeTag = "polochon.message.type";

        /// <summary>
        /// Tag: whether the message handled is a <c>command</c> or a <c>query</c>.
        /// </summary>
        public const string MessageKindTag = "polochon.message.kind";

        /// <summary>
        /// Tag: the <c>ResultCode.Status</c> of a command returning a <c>CommandResult</c>, e.g. <c>OK</c>.
        /// </summary>
        public const string ResultCodeTag = "polochon.result_code";

        /// <summary>
        /// Tag: the kind of external resource called, e.g. <c>queue</c> or <c>smtp</c>.
        /// </summary>
        public const string DependencyTypeTag = "polochon.dependency.type";

        /// <summary>
        /// Tag: the name of the external resource called, e.g. the queue name.
        /// </summary>
        public const string DependencyNameTag = "polochon.dependency.name";

        /// <summary>
        /// Tag: the operation called on the external resource, e.g. <c>SendMessage</c>.
        /// </summary>
        public const string DependencyOperationTag = "polochon.dependency.operation";

        /// <summary>
        /// Tag (OpenTelemetry semantic convention): why an operation failed - the exception's full
        /// type name, or the failed <c>ResultCode.Status</c>. Absent on success.
        /// </summary>
        public const string ErrorTypeTag = "error.type";

        /// <summary>
        /// Gets the activity source and meter name of the given module.
        /// </summary>
        /// <param name="moduleName">The module name.</param>
        /// <returns><c>Polochon.Modules.{moduleName}</c>.</returns>
        public static string GetModuleSourceName(string moduleName) => ModuleSourcePrefix + moduleName;
    }
}
