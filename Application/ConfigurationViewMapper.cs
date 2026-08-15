using Riok.Mapperly.Abstractions;

namespace proxifyre_tray
{
    /// <summary>
    /// Mapperly-generated, one-way conversion from the domain model to the view-model the
    /// presenter hands the view. Unlike <see cref="ConfigurationMapper"/> (DTO &lt;-&gt; domain),
    /// this needs no hand-written per-property logic at all - <see cref="AppConfiguration"/> and
    /// <see cref="ConfigurationView"/> (and their nested Proxy types) have identical shapes, so
    /// Mapperly generates the whole thing, including the list loop, on its own. There's no
    /// reverse direction: the view never reconstructs a domain object, it only raises edit
    /// intents (a proxy id plus new values) for the presenter to apply.
    /// </summary>
    [Mapper]
    internal static partial class ConfigurationViewMapper
    {
        public static partial ConfigurationView ToView(this AppConfiguration source);
    }
}
