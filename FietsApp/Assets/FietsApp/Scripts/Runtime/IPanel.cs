namespace FietsApp
{
    /// <summary>
    /// Een paneel dat je kunt sluiten. De knop Terug van <see cref="TouchBar"/> sluit wat open staat, zoals Escape
    /// op het toetsenbord (story 14.2).
    /// </summary>
    public interface IPanel
    {
        bool IsOpen { get; }
        void Close();
    }
}
