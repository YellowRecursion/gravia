namespace Gravia.Domain.Traits.Interfaces;

/// <summary>
/// A reusable characteristic that adds metadata or behavior to its owner in Gravia.
/// </summary>
/// <remarks>
/// <para>
/// Traits are a shared extension mechanism, independent of settings or any particular UI.
/// An owner can carry several traits, each describing an additional characteristic.
/// The owner's context provides the information needed to interpret them, such as the type
/// of a setting's value. Traits do not replace that context.
/// </para>
/// <para>
/// A trait can contain both data and functional behavior. This base interface does not require
/// validation or any other specific operation. Additional capabilities are expressed through
/// separate interfaces, such as <see cref="IValidationTrait{TValue}"/>. The owner invokes only
/// capabilities compatible with its context. A validating trait can reject a candidate value
/// by throwing an exception before the owner applies it.
/// </para>
/// <para>
/// On the frontend, handlers recognize serialized traits by <see cref="Type"/> and apply them
/// in the owner's context. For example, a choice trait supplies values and localized labels
/// from which a selection editor can be built. Traits describe characteristics; the frontend
/// chooses the concrete UI components. Backend validation remains authoritative.
/// </para>
/// <para>
/// An unknown or incompatible trait is ignored by the consumer. The owner continues to work
/// as if that trait were absent; a field must not become disabled or fail to render solely
/// because such a trait is attached. Applicability belongs to each capability or handler,
/// rather than a central table of permitted owner and trait combinations.
/// </para>
/// <para>
/// A trait may itself serve as its transport representation. When a separate DTO is needed,
/// it is kept in the same source file as the trait. Executable behavior stays on the backend;
/// the frontend receives the data needed by its handlers.
/// </para>
/// </remarks>
public interface ITrait
{
    /// <summary>
    /// A stable identifier used by consumers to recognize the trait in its serialized representation.
    /// </summary>
    string Type { get; }
}
