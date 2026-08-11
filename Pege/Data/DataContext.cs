using Microsoft.EntityFrameworkCore;

namespace Pege.Data
{
    public class DataContext(DbContextOptions<DataContext> options) : DbContext(options)
    {
        public DbSet<StreamInfo> Streams { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<StreamInfo>().UseTphMappingStrategy();

            modelBuilder.Entity<StreamInfo>()
                .HasDiscriminator<string>("Discriminator")
                .HasValue<FileAudioStreamInfo>("File")
                .HasValue<RelayAudioStreamInfo>("AudioRelay");

            modelBuilder.Entity<StreamInfo>()
               .Property("Discriminator")
               .HasMaxLength(50);

            modelBuilder.Entity<FileAudioStreamInfo>()
                .Property(f => f.Path)
                .HasColumnName("Source")
                .HasMaxLength(500);

            modelBuilder.Entity<RelayAudioStreamInfo>()
                .Property(r => r.Uri)
                .HasColumnName("Source")
                .HasMaxLength(500);

            modelBuilder.Entity<StreamInfo>()
                .Property(s => s.Registered)
                .HasConversion(
                    v => v,
                    v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : null
                );

            modelBuilder.Entity<FileAudioStreamInfo>().HasData(
                new FileAudioStreamInfo
                {
                    Id = "_",
                    Title = "o0o0.online",
                    Country = "Russia",
                    ImplType = "RandomMp3AudioStream",
                    Path = "mp3",
                    Registered = DateTime.Parse("2026-01-01T00:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal),
                    TelegramChannelId = "-1004378303357"
                }
            );
            modelBuilder.Entity<RelayAudioStreamInfo>().HasData(
                new RelayAudioStreamInfo
                {
                    Id = "a",
                    Title = "Arrow Classic Rock",
                    Country = "Netherlands",
                    ImplType = "RelayAudioStream",
                    Uri = "http://stream.gal.io/arrow",
                    Registered = DateTime.Parse("2026-01-01T00:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal)
                }
            );
            modelBuilder.Entity<RelayAudioStreamInfo>().HasData(
                new RelayAudioStreamInfo
                {
                    Id = "hr",
                    Title = "Hard Rock Radio FM",
                    Country = "USA",
                    ImplType = "RelayShoutcastV1AudioStream",
                    Uri = "http://67.249.184.45:8015",
                    Registered = DateTime.Parse("2026-01-01T00:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal)
                }
            );

            base.OnModelCreating(modelBuilder);
        }
    }
}
