namespace Pege.Entities
{
    /// <summary>
    /// Базовый класс чанка трансляции.
    /// </summary>
    /// <param name="Data">Содержимое чанка.</param>
    public abstract class Chunk
    {
        public ReadOnlyMemory<byte> Data { get; set; }
    }
}
