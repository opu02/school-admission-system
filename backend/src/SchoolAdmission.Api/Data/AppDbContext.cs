using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>SQL fragment listing the statuses that occupy a seat (used by the partial unique index).</summary>
    public const string SeatOccupyingStatusesSql = "status IN ('HELD', 'CONFIRMED')";

    public DbSet<SchoolClass> Classes => Set<SchoolClass>();

    public DbSet<Hold> Holds => Set<Hold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolClass>(e =>
        {
            e.ToTable("classes", t =>
                t.HasCheckConstraint("ck_classes_total_seats_positive", "total_seats > 0"));

            e.HasKey(x => x.Id).HasName("pk_classes");

            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(SchoolClass.NameMaxLength).IsRequired();
            e.Property(x => x.NormalizedName).HasColumnName("normalized_name").HasMaxLength(SchoolClass.NameMaxLength).IsRequired();
            e.Property(x => x.TotalSeats).HasColumnName("total_seats");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");

            e.HasIndex(x => x.NormalizedName)
                .IsUnique()
                .HasDatabaseName("ux_classes_normalized_name");
        });

        modelBuilder.Entity<Hold>(e =>
        {
            e.ToTable("holds", t =>
                t.HasCheckConstraint("ck_holds_status", "status IN ('HELD', 'CONFIRMED', 'EXPIRED', 'CANCELLED')"));

            e.HasKey(x => x.Id).HasName("pk_holds");

            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(64);
            e.Property(x => x.ClassId).HasColumnName("class_id").HasMaxLength(64).IsRequired();
            e.Property(x => x.StudentId).HasColumnName("student_id").HasMaxLength(17).IsRequired();
            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasConversion(v => v.ToApiString(), v => HoldStatusExtensions.FromApiString(v))
                .IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
            e.Property(x => x.CancelledAt).HasColumnName("cancelled_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasOne(x => x.Class)
                .WithMany(c => c.Holds)
                .HasForeignKey(x => x.ClassId)
                .HasConstraintName("fk_holds_classes_class_id")
                .OnDelete(DeleteBehavior.Restrict);

            // Database-level guarantee: one student can have at most ONE active
            // (HELD or CONFIRMED) application per class, even under races.
            e.HasIndex(x => new { x.ClassId, x.StudentId })
                .IsUnique()
                .HasFilter(SeatOccupyingStatusesSql)
                .HasDatabaseName("ux_holds_active_student_per_class");

            // Seat counting per class.
            e.HasIndex(x => new { x.ClassId, x.Status, x.ExpiresAt })
                .HasDatabaseName("ix_holds_class_id_status_expires_at");

            // Background expiry sweep.
            e.HasIndex(x => new { x.Status, x.ExpiresAt })
                .HasDatabaseName("ix_holds_status_expires_at");

            // "My applications".
            e.HasIndex(x => x.StudentId)
                .HasDatabaseName("ix_holds_student_id");
        });
    }
}
