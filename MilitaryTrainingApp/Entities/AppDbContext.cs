using Microsoft.EntityFrameworkCore;

namespace MilitaryTrainingApp.Entities
{
    public class AppDbContext : DbContext
    {
        public DbSet<Plan> Plans { get; set; } = null!;
        public DbSet<TrainingTarget> TrainingTargets { get; set; } = null!;
        public DbSet<PlanTarget> PlanTargets { get; set; } = null!;
        public DbSet<ProgramNodeType> ProgramNodeTypes { get; set; } = null!;
        public DbSet<TimeNodeType> TimeNodeTypes { get; set; } = null!;
        public DbSet<ProgramNode> ProgramNodes { get; set; } = null!;
        public DbSet<TimeNode> TimeNodes { get; set; } = null!;
        public DbSet<TimeAllocation> TimeAllocations { get; set; } = null!;
        public DbSet<ProgramNodeDecor> ProgramNodeDecors { get; set; } = null!;
        public DbSet<BlackoutDate> BlackoutDates { get; set; } = null!;
        public DbSet<SchedulingPriorityRule> SchedulingPriorityRules { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var connectionString = "Server=localhost;Database=QuanLyHuanLuyen;User=root;Password=;";
                optionsBuilder
                    .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                    .UseSnakeCaseNamingConvention(); // Tự động map PascalCase -> snake_case theo chuẩn schema.sql
            }
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Plan>(entity =>
            {
                entity.ToTable("plan");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(255);
                entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("DRAFT");
            });

            modelBuilder.Entity<TrainingTarget>(entity =>
            {
                entity.ToTable("training_target");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(255);
            });

            modelBuilder.Entity<PlanTarget>(entity =>
            {
                entity.ToTable("plan_target");
                entity.HasIndex(e => new { e.PlanId, e.TargetId }).IsUnique();

                entity.HasOne(e => e.Plan)
                      .WithMany(p => p.PlanTargets)
                      .HasForeignKey(e => e.PlanId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Target)
                      .WithMany(t => t.PlanTargets)
                      .HasForeignKey(e => e.TargetId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ProgramNodeType>(entity =>
            {
                entity.ToTable("program_node_type");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<TimeNodeType>(entity =>
            {
                entity.ToTable("time_node_type");
                entity.HasIndex(e => e.Code).IsUnique();
                entity.Property(e => e.Code).HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(100);
            });

            modelBuilder.Entity<ProgramNode>(entity =>
            {
                entity.ToTable("program_node");
                entity.Property(e => e.Code).HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(255);
                entity.Property(e => e.Capacity).HasColumnType("decimal(8,2)").HasDefaultValue(0.00m);
                entity.Property(e => e.Level).HasDefaultValue(1);
                entity.Property(e => e.TreePath).HasMaxLength(500);

                entity.HasOne(e => e.PlanTarget)
                      .WithMany(pt => pt.ProgramNodes)
                      .HasForeignKey(e => e.PlanTargetId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Parent)
                      .WithMany(p => p.Children)
                      .HasForeignKey(e => e.ParentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.NodeType)
                      .WithMany(nt => nt.ProgramNodes)
                      .HasForeignKey(e => e.NodeTypeId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.PrerequisiteNode)
                      .WithMany()
                      .HasForeignKey(e => e.PrerequisiteNodeId)
                      .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<TimeNode>(entity =>
            {
                entity.ToTable("time_node");
                entity.Property(e => e.Code).HasMaxLength(50);
                entity.Property(e => e.Name).HasMaxLength(255);
                entity.Property(e => e.Level).HasDefaultValue(1);
                entity.Property(e => e.TreePath).HasMaxLength(500);

                entity.HasOne(e => e.Plan)
                      .WithMany(p => p.TimeNodes)
                      .HasForeignKey(e => e.PlanId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.Parent)
                      .WithMany(p => p.Children)
                      .HasForeignKey(e => e.ParentId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.NodeType)
                      .WithMany(nt => nt.TimeNodes)
                      .HasForeignKey(e => e.NodeTypeId)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<TimeAllocation>(entity =>
            {
                entity.ToTable("time_allocation");
                entity.HasIndex(e => new { e.ProgramNodeId, e.TimeNodeId }).IsUnique();
                entity.Property(e => e.AllocatedHours).HasColumnType("decimal(8,2)");

                entity.HasOne(e => e.ProgramNode)
                      .WithMany(pn => pn.TimeAllocations)
                      .HasForeignKey(e => e.ProgramNodeId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(e => e.TimeNode)
                      .WithMany(tn => tn.TimeAllocations)
                      .HasForeignKey(e => e.TimeNodeId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<ProgramNodeDecor>(entity =>
            {
                entity.ToTable("program_node_decor");
                entity.HasKey(e => e.ProgramNodeId);
                entity.Property(e => e.BgColorHex).HasMaxLength(10).HasDefaultValue("#FFFFFF");
                entity.Property(e => e.BorderColorHex).HasMaxLength(10).HasDefaultValue("#0066CC");
                entity.Property(e => e.TextColorHex).HasMaxLength(10).HasDefaultValue("#000000");

                // Quan hệ 1-1 với ProgramNode
                entity.HasOne(e => e.ProgramNode)
                      .WithOne(pn => pn.Decor)
                      .HasForeignKey<ProgramNodeDecor>(e => e.ProgramNodeId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<BlackoutDate>(entity =>
            {
                entity.ToTable("blackout_date");
                entity.Property(e => e.HolidayName).HasMaxLength(255);
                entity.Property(e => e.Description).HasMaxLength(500);

                entity.HasOne(e => e.Plan)
                      .WithMany(p => p.BlackoutDates)
                      .HasForeignKey(e => e.PlanId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SchedulingPriorityRule>(entity =>
            {
                entity.ToTable("scheduling_priority_rule");
                entity.Property(e => e.RuleCode).HasMaxLength(50);
                entity.Property(e => e.RuleName).HasMaxLength(255);
                entity.Property(e => e.PriorityScore).HasDefaultValue(50);
                entity.Property(e => e.IsActive).HasDefaultValue(true);

                entity.HasOne(e => e.Plan)
                      .WithMany(p => p.SchedulingPriorityRules)
                      .HasForeignKey(e => e.PlanId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}