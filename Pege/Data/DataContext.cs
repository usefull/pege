using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

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

            base.OnModelCreating(modelBuilder);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ValidateEntities();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ValidateEntities();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        private void ValidateEntities()
        {
            var entities = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
                .Select(e => e.Entity);

            foreach (var entity in entities)
            {
                var validationContext = new ValidationContext(entity);
                // Выбросит ValidationException, если Path (или другие [Required] поля) окажется null
                Validator.ValidateObject(entity, validationContext, validateAllProperties: true);
            }
        }
    }
}
