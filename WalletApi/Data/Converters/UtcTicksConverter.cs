using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace WalletApi.Data.Converters;

// SQLite'ın DateTimeOffset diye bir tipi yoktur ve bu sütunlara göre ORDER BY yapamaz.
// Tarihleri UTC tick (long) olarak saklıyoruz: sıralama sayısal olduğu için doğru çalışır.
public class UtcTicksConverter : ValueConverter<DateTimeOffset, long>
{
    public UtcTicksConverter()
        : base(
            value => value.UtcTicks,
            ticks => new DateTimeOffset(ticks, TimeSpan.Zero))
    {
    }
}
