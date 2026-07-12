namespace ReturnLoad.Api.Http;

/// <summary>
/// Opts an action (or an entire controller) out of automatic response-envelope
/// wrapping performed by <see cref="ResponseEnvelopeResultFilter"/>. Use only when a
/// raw body is genuinely required — e.g. a file download or a third-party webhook
/// callback that must match an external schema. Ordinary JSON endpoints should
/// <b>never</b> use this: the uniform envelope is the whole point of the API contract.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public sealed class SkipResponseEnvelopeAttribute : Attribute;
